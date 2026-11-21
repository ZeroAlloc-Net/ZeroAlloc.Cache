using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        // A plain struct, not a record struct, so the C# 9 cases compile.
        public readonly struct Money { public Money(decimal amount) => Amount = amount; public decimal Amount { get; } }

        public interface IThing
        {
            {{attribute}}
            {{returnType}} GetAsync(int id, CancellationToken ct);
        }
        """;

    private static string Generate(string returnType, string attribute, LanguageVersion languageVersion) =>
        string.Join("\n", TestHelper.GetGeneratedSources(Source(returnType, attribute), languageVersion));

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
        // Below C# 10: the string key lookup.
        var stringKey = Generate(returnType, Unbounded, LanguageVersion.CSharp9);
        stringKey.Should().Contain($"_cache.TryGetValue(__key, out object? __boxed) && {condition})");
        stringKey.Should().Contain(hitReturn);

        // C# 10 and later: the span key lookup, #185, keeps the same concrete type test.
        var spanKey = Generate(returnType, Unbounded, LanguageVersion.Latest);
        spanKey.Should().Contain($"if (__found && {condition})");
        spanKey.Should().Contain(hitReturn);
    }

    [Theory]
    [InlineData("ValueTask<int>", Unbounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<int?>", Unbounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<Money>", Unbounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<Money?>", Unbounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<string?>", Unbounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<int>", Bounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<int?>", Bounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<Money?>", Bounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<string?>", Bounded, LanguageVersion.Latest)]
    [InlineData("ValueTask<int>", Unbounded, LanguageVersion.CSharp9)]
    [InlineData("ValueTask<int?>", Unbounded, LanguageVersion.CSharp9)]
    [InlineData("ValueTask<Money?>", Bounded, LanguageVersion.CSharp9)]
    public void HitPath_NeverCallsTheGenericTryGetValue(string returnType, string attribute, LanguageVersion languageVersion)
    {
        var (compilation, generatedTrees) = TestHelper.Compile(Source(returnType, attribute), languageVersion);

        var calls = generatedTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Select(call => compilation.GetSemanticModel(tree).GetSymbolInfo(call).Symbol as IMethodSymbol))
            .Where(method => method is { Name: "TryGetValue" })
            .ToList();

        // Only the non-generic TryGetValue(object, out object?) and, from C# 10 on, the non-generic
        // MemoryCache.TryGetValue(ReadOnlySpan<char>, out object?) may be called.
        calls.Should().NotBeEmpty();
        calls.Should().AllSatisfy(method =>
        {
            method!.IsGenericMethod.Should().BeFalse();
            method.Parameters[1].Type.SpecialType.Should().Be(SpecialType.System_Object);
        });
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
        foreach (var languageVersion in LanguageVersions)
            await AssertCompilesCleanly(Source(returnType, attribute), languageVersion);
    }

    // The span key lookup from C# 10 on, and the string key lookup below it.
    private static readonly LanguageVersion[] LanguageVersions = { LanguageVersion.Latest, LanguageVersion.CSharp9 };

    private static async Task AssertCompilesCleanly(string source, LanguageVersion languageVersion)
    {
        var diagnostics = await TestHelper.GetDiagnostics(source, languageVersion: languageVersion).ConfigureAwait(false);

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
        const string source = """
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
            """;

        foreach (var languageVersion in LanguageVersions)
            await AssertCompilesCleanly(source, languageVersion);
    }
}
