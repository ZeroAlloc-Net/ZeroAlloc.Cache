using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// #182: under NativeAOT, the generic <c>CacheExtensions.TryGetValue&lt;TItem&gt;</c> with a
/// <c>Nullable&lt;T&gt;</c> type argument never returns, an upstream runtime bug. The generated
/// hit path therefore reads the entry as object and tests it against the concrete type. These
/// tests pin that shape for every return kind so it is not simplified back.
/// </summary>
public class CacheHitShapeTests
{
    private const string Unbounded = "[Cache(TtlMs = 10000)]";
    private const string Bounded = "[Cache(TtlMs = 10000, MaxEntries = 10)]";

    private static string Source(string returnType, string attribute) => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Cache;

        public readonly record struct Money(decimal Amount);

        public interface IThing
        {
            {{attribute}}
            {{returnType}} GetAsync(int id, CancellationToken ct);
        }
        """;

    private static string Generate(string returnType, string attribute) =>
        string.Join("\n", TestHelper.GetGeneratedSources(Source(returnType, attribute)));

    [Theory]
    [InlineData("ValueTask<int>", "__boxed is int __cached", "return __cached;")]
    [InlineData("Task<int>", "__boxed is int __cached", "return __cached;")]
    [InlineData("ValueTask<Money>", "__boxed is global::Money __cached", "return __cached;")]
    [InlineData("ValueTask<int?>", "__boxed is null or int", "return __boxed is null ? default(int?) : (int)__boxed;")]
    [InlineData("Task<int?>", "__boxed is null or int", "return __boxed is null ? default(int?) : (int)__boxed;")]
    [InlineData("ValueTask<Money?>", "__boxed is null or global::Money", "return __boxed is null ? default(global::Money?) : (global::Money)__boxed;")]
    [InlineData("ValueTask<string>", "__boxed is null or string", "return (string)__boxed!;")]
    [InlineData("ValueTask<string?>", "__boxed is null or string", "return (string?)__boxed;")]
    public void HitPath_TestsTheConcreteType(string returnType, string condition, string hitReturn)
    {
        var generated = Generate(returnType, Unbounded);

        generated.Should().Contain($"_cache.TryGetValue(__key, out object? __boxed) && {condition})");
        generated.Should().Contain(hitReturn);
    }

    [Theory]
    [InlineData("ValueTask<int>", Unbounded)]
    [InlineData("ValueTask<int?>", Unbounded)]
    [InlineData("ValueTask<Money>", Unbounded)]
    [InlineData("ValueTask<Money?>", Unbounded)]
    [InlineData("ValueTask<string?>", Unbounded)]
    [InlineData("ValueTask<int>", Bounded)]
    [InlineData("ValueTask<int?>", Bounded)]
    [InlineData("ValueTask<Money?>", Bounded)]
    [InlineData("ValueTask<string?>", Bounded)]
    public void HitPath_NeverCallsTheGenericTryGetValue(string returnType, string attribute)
    {
        var calls = Generate(returnType, attribute)
            .Split('\n')
            .Where(l => l.Contains(".TryGetValue(", System.StringComparison.Ordinal))
            .ToList();

        // Only the non-generic IMemoryCache.TryGetValue(object, out object?) may be called.
        calls.Should().ContainSingle();
        calls[0].Should().Contain("TryGetValue(__key, out object? __boxed)");
    }

    [Theory]
    [InlineData("ValueTask<int>", Unbounded)]
    [InlineData("Task<int?>", Unbounded)]
    [InlineData("ValueTask<Money>", Unbounded)]
    [InlineData("ValueTask<Money?>", Unbounded)]
    [InlineData("ValueTask<string>", Unbounded)]
    [InlineData("ValueTask<string?>", Unbounded)]
    [InlineData("ValueTask<int>", Bounded)]
    [InlineData("Task<int?>", Bounded)]
    [InlineData("ValueTask<Money?>", Bounded)]
    [InlineData("ValueTask<string?>", Bounded)]
    public async Task HitPath_CompilesWithoutErrorsOrGeneratedWarnings(string returnType, string attribute)
    {
        var diagnostics = await TestHelper.GetDiagnostics(Source(returnType, attribute));

        // Errors anywhere, and warnings inside the generated proxy, which consumers build with
        // warnings as errors. The proxy is emitted under #nullable enable.
        var problems = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error
                || (d.Severity == DiagnosticSeverity.Warning
                    && d.Location.SourceTree?.FilePath.EndsWith(".g.cs", System.StringComparison.Ordinal) == true))
            .Select(d => d.ToString())
            .ToList();
        problems.Should().BeEmpty();
    }

    // The emitted signatures keep nullable reference annotations on parameters and on
    // passthrough returns too, or the proxy no longer matches the interface it implements.
    [Fact]
    public async Task NullableReferenceParameterAndPassthrough_CompileWithoutGeneratedWarnings()
    {
        var diagnostics = await TestHelper.GetDiagnostics("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            [Cache(TtlMs = 10000)]
            public interface IThing
            {
                ValueTask<string?> FindAsync(string? query, CancellationToken ct);
                ValueTask<string?> SaveAsync(string? data);
                string? Describe(object? value);
            }
            """);

        var problems = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error
                || (d.Severity == DiagnosticSeverity.Warning
                    && d.Location.SourceTree?.FilePath.EndsWith(".g.cs", System.StringComparison.Ordinal) == true))
            .Select(d => d.ToString())
            .ToList();
        problems.Should().BeEmpty();
    }
}
