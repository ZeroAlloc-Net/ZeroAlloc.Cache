using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Generator.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public void InterfaceLevel_IMemoryCache_SingleMethod()
    {
        var source = """
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
        TestHelper.Verify(source);
    }

    [Fact]
    public void InterfaceLevel_WithPassthrough_GeneratesProxy()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
                ValueTask SaveAsync(string data, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void MethodLevel_Override_ShadowsInterfaceLevel()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetByIdAsync(string id, CancellationToken ct);
                [Cache(TtlMs = 5_000)]
                ValueTask<string> GetBySlugAsync(string slug, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void UseHybridCache_GeneratesHybridProxy()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, UseHybridCache = true)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void UseHybridCache_NoParams_GeneratesHybridProxy()
    {
        const string source = """
            using ZeroAlloc.Cache;
            namespace T;
            [Cache(TtlMs = 1000, UseHybridCache = true)]
            public interface IMyService
            {
                System.Threading.Tasks.ValueTask<string> GetAsync(System.Threading.CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void Sliding_IMemoryCache_UsesMemoryCacheEntryOptions()
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
        TestHelper.Verify(source);
    }

    [Fact]
    public void MaxEntries_UsesIsolatedMemoryCache()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, MaxEntries = 500)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void MaxEntries_WithHybridCache_MixedMethods()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, MaxEntries = 500)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
                [Cache(TtlMs = 10_000, UseHybridCache = true)]
                ValueTask<string> FindAsync(string query, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void Sliding_WithMaxEntries_UsesSlidingExpirationAndSize()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, Sliding = true, MaxEntries = 100)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void GlobalNamespace_GeneratesProxy()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            [Cache(TtlMs = 5_000)]
            public interface IGlobalService
            {
                ValueTask<string> GetAsync(int id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }

    [Fact]
    public void PurePassthrough_AllNonGenericReturns_GeneratesProxy()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 5_000)]
            public interface IMyService
            {
                ValueTask SaveAsync(string data, CancellationToken ct);
                ValueTask DeleteAsync(int id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }
}
