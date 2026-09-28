using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

public sealed class DiagnosticTests
{
    [Fact]
    public async Task ZC0001_SlidingTrue_HybridCache_EmitsWarning()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, Sliding = true, UseHybridCache = true)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0001 = diags.Where(d => string.Equals(d.Id, "ZC0001", System.StringComparison.Ordinal)).ToList();
        zc0001.Should().HaveCount(1);
        zc0001[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        zc0001[0].GetMessage().Should().Contain("GetAsync");
    }

    [Fact]
    public async Task ZC0001_NotEmitted_WhenIMemoryCache()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, Sliding = true)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0001", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZC0002_ReferenceTypeParam_EmitsWarning()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { } // reference type
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(ProductFilter filter, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0002 = diags.Where(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal)).ToList();
        zc0002.Should().HaveCount(1);
        var d0 = zc0002.First();
        d0.Severity.Should().Be(DiagnosticSeverity.Warning);
        d0.GetMessage().Should().Contain("filter").And.Contain("GetAsync");
    }

    [Fact]
    public async Task ZC0002_NotEmitted_ForString()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZC0002_NotEmitted_ForValueTypes()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(int id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal));
    }

    // The ZC0003 to ZC0005 sources below mirror the examples in docs/diagnostics, so each page's
    // triggering example and its fix are both compiled.

    private const string ProductSource = """
        namespace T;
        public sealed class Product { public int Id { get; init; } }
        """;

    private const string HybridProductRepository = ProductSource + """

        [ZeroAlloc.Cache.Cache(TtlMs = 60_000, UseHybridCache = true)]
        public interface IProductRepository
        {
            System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
        }
        """;

    [Fact]
    public async Task ZC0003_UseHybridCache_WithoutHybridCacheAssembly_EmitsError()
    {
        var diags = await TestHelper.GetDiagnostics(HybridProductRepository, referenceHybridCache: false);
        var zc0003 = diags.Where(d => string.Equals(d.Id, "ZC0003", StringComparison.Ordinal)).ToList();
        zc0003.Should().HaveCount(1);
        zc0003[0].Severity.Should().Be(DiagnosticSeverity.Error);
        zc0003[0].GetMessage().Should().Contain("Microsoft.Extensions.Caching.Hybrid");

        // The error suppresses generation for the interface: no proxy and no DI extension, so the
        // consumer sees ZC0003 rather than CS1061 on the generated AddHybridCache call.
        TestHelper.GetGeneratedFileNames(HybridProductRepository, referenceHybridCache: false)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ZC0003_NotEmitted_WhenHybridCacheAssemblyReferenced()
    {
        var diags = await TestHelper.GetDiagnostics(HybridProductRepository);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0003", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0003_NotEmitted_WithoutUseHybridCache_EvenWithoutHybridCacheAssembly()
    {
        const string source = ProductSource + """

            [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
            public interface IProductRepository
            {
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source, referenceHybridCache: false);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0003", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0004_DifferentMaxEntries_EmitsWarning()
    {
        const string source = ProductSource + """

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 500)]
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);

                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 10_000)]
                System.Threading.Tasks.ValueTask<Product?> GetBySkuAsync(string sku, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0004 = diags.Where(d => string.Equals(d.Id, "ZC0004", StringComparison.Ordinal)).ToList();
        zc0004.Should().HaveCount(1);
        zc0004[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        zc0004[0].GetMessage().Should().Be(
            "Interface 'IProductRepository': bounded methods specify different MaxEntries values. "
            + "The bounded methods of an interface share one size-limited cache; "
            + "the first bounded method's MaxEntries (500) sets its SizeLimit.");
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    // Only bounded methods share the size-limited cache; a hybrid method's MaxEntries is not a
    // competing limit.
    [Fact]
    public async Task ZC0004_NotEmitted_ForHybridMethodMaxEntries()
    {
        const string source = ProductSource + """

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 100, UseHybridCache = true)]
                System.Threading.Tasks.ValueTask<Product?> FindAsync(string query, System.Threading.CancellationToken ct);

                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 500)]
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0004", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0004_NotEmitted_WhenMaxEntriesAgree()
    {
        const string source = ProductSource + """

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 10_000)]
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);

                [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 10_000)]
                System.Threading.Tasks.ValueTask<Product?> GetBySkuAsync(string sku, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0004", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0004_NotEmitted_ForInterfaceLevelMaxEntries()
    {
        const string source = ProductSource + """

            [ZeroAlloc.Cache.Cache(TtlMs = 60_000, MaxEntries = 10_000)]
            public interface IProductRepository
            {
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
                System.Threading.Tasks.ValueTask<Product?> GetBySkuAsync(string sku, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0004", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("Product?", "GetById(int id)")]
    [InlineData("System.Threading.Tasks.Task", "RefreshAsync(System.Threading.CancellationToken ct)")]
    [InlineData("System.Threading.Tasks.ValueTask", "RefreshAsync(System.Threading.CancellationToken ct)")]
    [InlineData("void", "Refresh()")]
    [InlineData("System.Collections.Generic.List<Product>", "GetAll()")]
    [InlineData("int?", "Count()")]
    [InlineData("System.Collections.Generic.IAsyncEnumerable<Product>", "StreamAll()")]
    public async Task ZC0005_MethodCacheOnNonGenericReturn_EmitsWarning(string returnType, string signature)
    {
        var source = ProductSource + $$"""

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
                {{returnType}} {{signature}};
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0005 = diags.Where(d => string.Equals(d.Id, "ZC0005", StringComparison.Ordinal)).ToList();
        zc0005.Should().HaveCount(1);
        zc0005[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        zc0005[0].GetMessage().Should().Contain("Only Task<T> and ValueTask<T>");
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0005_NotEmitted_ForValueTaskOfT()
    {
        const string source = ProductSource + """

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);

                // No [Cache] on this method: it is forwarded to the inner implementation.
                System.Threading.Tasks.Task RefreshAsync(System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0005", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0005_NotEmitted_ForTaskOfT()
    {
        const string source = ProductSource + """

            public interface IProductRepository
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
                System.Threading.Tasks.Task<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0005", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    // Before the return-type check was narrowed to Task<T> and ValueTask<T>, any generic return
    // counted as cacheable, so an interface-level [Cache] over a synchronous List<T> method
    // produced a proxy that failed with CS1983. Such methods are now forwarded like void ones.
    [Fact]
    public async Task InterfaceLevelCache_SyncGenericReturn_IsForwarded_AndCompiles()
    {
        const string source = ProductSource + """

            [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
            public interface IProductRepository
            {
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);
                System.Collections.Generic.List<Product> GetAll();
                int? Count();
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0005", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ZC0005_NotEmitted_ForNonGenericReturn_UnderInterfaceLevelCache()
    {
        const string source = ProductSource + """

            [ZeroAlloc.Cache.Cache(TtlMs = 60_000)]
            public interface IProductRepository
            {
                System.Threading.Tasks.ValueTask<Product?> GetByIdAsync(int id, System.Threading.CancellationToken ct);

                // An interface-level [Cache] forwards the methods it cannot cache without a warning.
                System.Threading.Tasks.Task RefreshAsync(System.Threading.CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0005", StringComparison.Ordinal));
        diags.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
    }
}
