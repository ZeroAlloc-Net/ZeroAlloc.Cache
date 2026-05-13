using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using ZeroAlloc.Cache;
using ZiggyCreatures.Caching.Fusion;

namespace ZeroAlloc.Cache.Benchmarks;

// Compares ZA.Cache's source-generated proxy against the two cited
// alternatives for in-process L1 caching in .NET:
//
//   1. Raw IMemoryCache.GetOrCreateAsync (the hand-rolled pattern)
//   2. FusionCache GetOrSetAsync (the de-facto third-party L1+L2 library)
//
// All three measure the cache-HIT path — the value is already present
// from a warm-up call in [GlobalSetup]. The miss path is dominated by
// the underlying factory call, which is identical across all three.
//
// FusionCache's L2/distributed-cache and stampede-protection features
// are not exercised here; this is the L1-only fair comparison. The
// FusionCache row includes overhead from those features being available
// even if unused, which is the realistic production cost.
[MemoryDiagnoser]
[SimpleJob]
public class CacheLibrariesBenchmark
{
    private MemoryCache _msCache = null!;
    private IFusionCache _fusion = null!;
    private ICustomerService _zaProxy = null!;
    private ICustomerService _zaInner = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _msCache = new MemoryCache(new MemoryCacheOptions());
        _zaInner = new CustomerService();
        _zaProxy = new ICustomerServiceCacheProxy(_zaInner, _msCache);

        _fusion = new FusionCache(new FusionCacheOptions
        {
            DefaultEntryOptions = new FusionCacheEntryOptions
            {
                Duration = System.TimeSpan.FromMinutes(1),
            },
        });

        // Warm everything for key 42 so all rows hit the cache.
        _ = await _zaProxy.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
        _ = await _msCache.GetOrCreateAsync(("customer", 42), e =>
        {
            e.AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(1);
            return Task.FromResult($"customer-42");
        }).ConfigureAwait(false);
        _ = await _fusion.GetOrSetAsync<string>(
            "customer-42",
            (_, _) => Task.FromResult("customer-42"))
            .ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _msCache.Dispose();
        _fusion.Dispose();
    }

    // --- Raw MS IMemoryCache GetOrCreateAsync (cache hit) ---

    [Benchmark(Baseline = true, Description = "Raw IMemoryCache: cache hit")]
    [BenchmarkCategory("Hit")]
    public async Task<string?> Raw_MemoryCache_Hit()
        => await _msCache.GetOrCreateAsync(("customer", 42), e =>
        {
            // Factory only runs on miss; we warmed in Setup so this is dead code.
            return Task.FromResult($"customer-42");
        }).ConfigureAwait(false);

    // --- FusionCache GetOrSetAsync (cache hit) ---

    [Benchmark(Description = "FusionCache: cache hit")]
    [BenchmarkCategory("Hit")]
    public async Task<string> Fusion_Hit()
        => await _fusion.GetOrSetAsync<string>(
            "customer-42",
            (_, _) => Task.FromResult("customer-42"))
            .ConfigureAwait(false);

    // --- ZA proxy (cache hit) ---

    [Benchmark(Description = "ZA.Cache proxy: cache hit")]
    [BenchmarkCategory("Hit")]
    public async Task<string> Za_Hit()
        => await _zaProxy.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
}
