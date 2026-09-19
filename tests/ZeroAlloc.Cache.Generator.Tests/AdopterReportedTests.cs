using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Regression tests for #120, #121 and #122 — all reported by the same adopter on the same day.
///
/// Every pre-existing test in this suite returns <c>ValueTask&lt;string&gt;</c>. That monoculture
/// is why #122 shipped: the generated hit path returns <c>__cached!</c>, and the null-forgiving
/// operator silences a reference-type warning but cannot unwrap a <see cref="System.Nullable{T}"/>.
/// </summary>
public class AdopterReportedTests
{
    private static async Task AssertCompilesCleanlyAsync(string source)
    {
        var diagnostics = await TestHelper.GetDiagnostics(source).ConfigureAwait(false);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(
            errors.Count == 0,
            "Generated code did not compile:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    // #122: ValueTask<int> — the hit path emits `return __cached!;` where __cached is `int?`,
    // producing CS0266. Affects every value-type return, not only the `int X()` in the report.
    [Fact]
    public async Task ValueTypeReturn_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IThing
            {
                [Cache(TtlMs = 10000)]
                ValueTask<int> CountAsync();
            }
            """);
    }

    [Fact]
    public async Task NullableValueTypeReturn_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IThing
            {
                [Cache(TtlMs = 10000)]
                ValueTask<int?> CountAsync();
            }
            """);
    }

    [Fact]
    public async Task ValueTypeStructReturn_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public readonly record struct Money(decimal Amount);

            public interface IThing
            {
                [Cache(TtlMs = 10000)]
                ValueTask<Money> TotalAsync();
            }
            """);
    }

    // Guards the shape every existing test already covers, so a fix for the value-type path
    // cannot silently regress reference types.
    [Fact]
    public async Task ReferenceTypeReturn_StillGeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IThing
            {
                [Cache(TtlMs = 10000)]
                ValueTask<string> NameAsync();
            }
            """);
    }

    // #120: the DI extension is emitted `public` regardless of the interface's accessibility,
    // so an internal interface produces CS0051 — inconsistent accessibility.
    [Fact]
    public async Task InternalInterface_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            internal interface IThing
            {
                [Cache(TtlMs = 10000)]
                ValueTask<string> NameAsync();
            }
            """);
    }

    // #121: a sync method carrying [Cache] is classified passthrough, so the attribute does
    // nothing and the inner method runs on every call. Silence is the defect — the adopter had
    // no way to tell caching was never applied.
    [Fact]
    public async Task SyncMethodWithCacheAttribute_IsNotSilentlyIgnored()
    {
        var diagnostics = await TestHelper.GetDiagnostics("""
            using ZeroAlloc.Cache;

            public interface IThing
            {
                [Cache(TtlMs = 10000)]
                int Count();
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZC0005", System.StringComparison.Ordinal));
    }
}
