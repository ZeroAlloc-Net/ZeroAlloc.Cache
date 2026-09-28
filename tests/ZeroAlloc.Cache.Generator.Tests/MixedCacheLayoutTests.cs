using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// #180: an interface mixing a <c>MaxEntries &gt; 0</c> method with a non-hybrid
/// <c>MaxEntries = 0</c> method produced a DI extension that called a proxy constructor which did
/// not exist, CS1729. Every combination of the three cache kinds must compile.
/// </summary>
public class MixedCacheLayoutTests
{
    private static async Task AssertCompilesCleanlyAsync(string source)
    {
        var diagnostics = await TestHelper.GetDiagnostics(source).ConfigureAwait(false);
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(
            errors.Count == 0,
            "Generated code did not compile:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public async Task BoundedAndUnbounded_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 500)]
                ValueTask<string?> GetByIdAsync(int id, CancellationToken ct);

                [Cache(TtlMs = 60_000)]
                ValueTask<string?> GetFeaturedAsync(CancellationToken ct);
            }
            """);
    }

    [Fact]
    public async Task BoundedUnboundedAndHybrid_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 500)]
                ValueTask<string?> GetByIdAsync(int id, CancellationToken ct);

                [Cache(TtlMs = 60_000)]
                ValueTask<string?> GetFeaturedAsync(CancellationToken ct);

                [Cache(TtlMs = 60_000, UseHybridCache = true)]
                ValueTask<string?> FindAsync(string query, CancellationToken ct);
            }
            """);
    }

    [Fact]
    public async Task BoundedAndHybrid_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 500)]
                ValueTask<string?> GetByIdAsync(int id, CancellationToken ct);

                [Cache(TtlMs = 60_000, UseHybridCache = true)]
                ValueTask<string?> FindAsync(string query, CancellationToken ct);
            }
            """);
    }

    // HybridCache ignores MaxEntries, so no bounded cache holder is emitted or registered for it.
    [Fact]
    public async Task HybridWithMaxEntries_GeneratesCompilableCodeWithoutBoundedCache()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 500, UseHybridCache = true)]
                ValueTask<string?> FindAsync(string query, CancellationToken ct);
            }
            """;
        await AssertCompilesCleanlyAsync(source);

        TestHelper.GetGeneratedSources(source).Should().ContainSingle()
            .Which.Should().NotContain("SizeLimit", "no method uses the size-limited cache");
    }

    // Hybrid methods do not use the bounded cache, so a hybrid method's MaxEntries must not set
    // its SizeLimit, even when it is declared first.
    [Fact]
    public void BoundedCacheSizeLimit_ComesFromFirstNonHybridBoundedMethod()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            public interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 100, UseHybridCache = true)]
                ValueTask<string?> FindAsync(string query, CancellationToken ct);

                [Cache(TtlMs = 60_000, MaxEntries = 500)]
                ValueTask<string?> GetByIdAsync(int id, CancellationToken ct);
            }
            """;

        var generated = TestHelper.GetGeneratedSources(source).Should().ContainSingle().Subject;
        generated.Should().Contain("SizeLimit = 500").And.NotContain("SizeLimit = 100");
    }

    [Fact]
    public async Task InternalInterface_BoundedAndUnbounded_GeneratesCompilableCode()
    {
        await AssertCompilesCleanlyAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;

            namespace App;

            internal interface IProductRepository
            {
                [Cache(TtlMs = 60_000, MaxEntries = 500)]
                ValueTask<string?> GetByIdAsync(int id, CancellationToken ct);

                [Cache(TtlMs = 60_000)]
                ValueTask<string?> GetFeaturedAsync(CancellationToken ct);
            }
            """);
    }
}
