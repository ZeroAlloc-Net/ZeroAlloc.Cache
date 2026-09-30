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

    // A net8.0 consumer has no MemoryCache.TryGetValue(ReadOnlySpan<char>, ...), so the proxy keeps
    // the string key lookup, and one shared and one bounded method pin its full shape. #185
    [Fact]
    public void Net8MemoryCache_KeepsStringKeyLookup()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public interface IMyService
            {
                [Cache(TtlMs = 30_000)]
                ValueTask<string> GetAsync(string id, CancellationToken ct);

                [Cache(TtlMs = 30_000, MaxEntries = 100)]
                ValueTask<int> CountAsync(int id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source, net8MemoryCache: true);
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
    public void MaxEntries_WithUnboundedMethod_UsesBothCaches()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public interface IMyService
            {
                [Cache(TtlMs = 30_000, MaxEntries = 500)]
                ValueTask<string> GetAsync(string id, CancellationToken ct);
                [Cache(TtlMs = 10_000)]
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

    // #182: the hit path tests the stored object against the concrete type for every return
    // kind, never through the generic CacheExtensions.TryGetValue<TItem>.
    [Fact]
    public void ValueAndNullableReturns_HitPathTestsConcreteType()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public readonly record struct Money(decimal Amount);
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<int> CountAsync(int id, CancellationToken ct);
                Task<int?> MaybeCountAsync(int id, CancellationToken ct);
                ValueTask<Money> TotalAsync(int id, CancellationToken ct);
                ValueTask<Money?> MaybeTotalAsync(int id, CancellationToken ct);
                ValueTask<string?> MaybeNameAsync(int id, CancellationToken ct);
                [Cache(TtlMs = 30_000, MaxEntries = 100)]
                ValueTask<int> BoundedCountAsync(int id, CancellationToken ct);
            }
            """;
        TestHelper.Verify(source);
    }
}
