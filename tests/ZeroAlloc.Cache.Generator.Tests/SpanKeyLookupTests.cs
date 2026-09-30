using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// #185: on a cache hit the generated proxy formats the key into a stack buffer and looks it up
/// with <c>MemoryCache.TryGetValue(ReadOnlySpan&lt;char&gt;, out object?)</c>, so the hit allocates no
/// key string. The key text stays exactly what it was, because users read and evict entries by
/// it. The generator only takes that path when the API exists; net8.0 keeps the string lookup.
/// </summary>
public class SpanKeyLookupTests
{
    private const string SharedAndBounded = """
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Cache;

        // Global namespace, so the C# 9 case compiles: a namespace is emitted file-scoped.
        public interface IThing
        {
            [Cache(TtlMs = 10000)]
            ValueTask<string> GetAsync(int id, string region, CancellationToken ct);

            [Cache(TtlMs = 10000, MaxEntries = 10)]
            ValueTask<int> CountAsync(long id, CancellationToken ct);

            // No key parameters: a constant key, looked up by string on every path.
            [Cache(TtlMs = 10000, MaxEntries = 10)]
            ValueTask<int> TotalAsync(CancellationToken ct);
        }
        """;

    private static string Generate(string source, bool net8MemoryCache = false) =>
        string.Join("\n", TestHelper.GetGeneratedSources(source, net8MemoryCache));

    [Fact]
    public void Net9Plus_SharedCache_LooksUpBySpanOnlyWhenTheCacheIsExactlyMemoryCache()
    {
        var generated = Generate(SharedAndBounded);

        generated.Should().Contain("private readonly global::Microsoft.Extensions.Caching.Memory.MemoryCache? _memoryCache;");
        generated.Should().Contain("_memoryCache = cache.GetType() == typeof(global::Microsoft.Extensions.Caching.Memory.MemoryCache)");
        generated.Should().Contain("? __TryGetBySpanKey(_memoryCache, id, region, out __boxed)");
        generated.Should().Contain(": _cache.TryGetValue($\"IThing.GetAsync:{id}:{region}\", out __boxed);");
    }

    [Fact]
    public void Net9Plus_BoundedCache_AlwaysLooksUpBySpan()
    {
        var generated = Generate(SharedAndBounded);

        generated.Should().Contain("bool __found = __TryGetBySpanKey(_boundedCache, id, out __boxed);");
    }

    [Fact]
    public void Net9Plus_EveryKeyInterpolationIsTheStoredKeyText()
    {
        var generated = Generate(SharedAndBounded);

        // The stored key, the span lookup, its heap fallback and the non-MemoryCache lookup all
        // interpolate exactly the text the string key has always had.
        var interpolations = Regex.Matches(generated, "\\$\"IThing\\.[A-Za-z]+[^\"]*\"", RegexOptions.None, System.TimeSpan.FromSeconds(5))
            .Select(m => m.Value)
            .Distinct(System.StringComparer.Ordinal)
            .ToList();
        interpolations.Should().BeEquivalentTo(
            "$\"IThing.GetAsync:{id}:{region}\"",
            "$\"IThing.CountAsync:{id}\"",
            "$\"IThing.TotalAsync\"");

        generated.Should().Contain("var __key = $\"IThing.GetAsync:{id}:{region}\";");
        generated.Should().Contain("global::System.MemoryExtensions.TryWrite(__buffer, $\"IThing.GetAsync:{id}:{region}\", out int __length)");
        generated.Should().Contain(": __cache.TryGetValue((object)$\"IThing.GetAsync:{id}:{region}\", out __value);");
    }

    [Fact]
    public void Net9Plus_KeyStringIsBuiltOnlyOnTheMissPath()
    {
        var generated = Generate(SharedAndBounded);
        var getAsync = generated.Substring(
            generated.IndexOf("GetAsync(int id", System.StringComparison.Ordinal),
            generated.IndexOf("CountAsync(long id", System.StringComparison.Ordinal)
                - generated.IndexOf("GetAsync(int id", System.StringComparison.Ordinal));

        getAsync.IndexOf("var __key =", System.StringComparison.Ordinal)
            .Should().BeGreaterThan(getAsync.IndexOf("await _inner.GetAsync(", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Net8_KeepsTheStringKeyLookup()
    {
        var generated = Generate(SharedAndBounded, net8MemoryCache: true);

        generated.Should().NotContain("__TryGetBySpanKey");
        generated.Should().NotContain("_memoryCache");
        generated.Should().NotContain("TryWrite");
        generated.Should().Contain("var __key = $\"IThing.GetAsync:{id}:{region}\";");
        generated.Should().Contain("if (_cache.TryGetValue(__key, out object? __boxed)");
        generated.Should().Contain("if (_boundedCache.TryGetValue((object)__key, out object? __boxed)");
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp9, false)]
    [InlineData(LanguageVersion.CSharp10, true)]
    [InlineData(LanguageVersion.Latest, true)]
    public void SpanLookup_NeedsCSharp10InterpolatedStringHandlers(LanguageVersion languageVersion, bool expectSpanLookup)
    {
        var (compilation, trees) = TestHelper.Compile(SharedAndBounded, languageVersion: languageVersion);
        var generated = string.Join("\n", trees.Select(t => t.ToString()));

        generated.Contains("__TryGetBySpanKey", System.StringComparison.Ordinal).Should().Be(expectSpanLookup);
        compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .Should().BeEmpty();
    }

    [Fact]
    public void NoKeyParameters_KeepsTheConstantKey()
    {
        // A key without parameters is a constant string, which a hit never allocates.
        var generated = Generate("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            [Cache(TtlMs = 10000)]
            public interface IThing
            {
                ValueTask<string> GetAllAsync();
            }
            """);

        generated.Should().NotContain("__TryGetBySpanKey");
        generated.Should().NotContain("_memoryCache");
        generated.Should().Contain("var __key = $\"IThing.GetAllAsync\";");
    }

    [Fact]
    public void HybridCache_KeepsStringKeys()
    {
        // HybridCache.GetOrCreateAsync takes a string key only.
        var generated = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            [Cache(TtlMs = 10000, UseHybridCache = true)]
            public interface IThing
            {
                ValueTask<string> GetAsync(int id, CancellationToken ct);
            }
            """);

        generated.Should().NotContain("__TryGetBySpanKey");
        generated.Should().NotContain("_memoryCache");
        generated.Should().Contain("$\"IThing.GetAsync:{id}\",");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyParametersOfEveryKind_CompileWithoutErrorsOrGeneratedWarnings(bool net8MemoryCache)
    {
        // Parameter names that the local function could collide with are included on purpose.
        var diagnostics = await TestHelper.GetDiagnostics("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public enum Kind { A, B }
            public readonly struct Plain { public override string ToString() => "plain"; }
            public sealed record Filter(string Name);

            [Cache(TtlMs = 10000)]
            public interface IThing
            {
                ValueTask<string> ByValuesAsync(int a, long b, Guid c, DateTime d, decimal e, double f, Kind g, int? h, CancellationToken ct);
                ValueTask<string?> ByReferencesAsync(string? name, Filter filter, Plain plain);
                ValueTask<int> CollidingNamesAsync(string cache, int value, int buffer, int length);
                [Cache(TtlMs = 10000, MaxEntries = 5)]
                ValueTask<int?> BoundedAsync(string __ignored, int id);
            }
            """, net8MemoryCache: net8MemoryCache);

        diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error
                || (d.Severity == DiagnosticSeverity.Warning
                    && d.Location.SourceTree?.FilePath.EndsWith(".g.cs", System.StringComparison.Ordinal) == true))
            .Select(d => d.ToString())
            .Should().BeEmpty();
    }
}
