using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.Benchmarks;

// Measures the generator-emitted cache proxy on a CACHE HIT, the hot path after the first request
// has warmed the entry.
//
// The claim: with a plain MemoryCache on .NET 9 or later, a hit on a ValueTask<T> method allocates
// 0 B/op. The key text is formatted into a stack buffer and looked up with
// MemoryCache.TryGetValue(ReadOnlySpan<char>, out object?), so no key string is built, and a value
// type is unboxed from the entry, which does not allocate. See #185.
//
// Where a hit still allocates is listed in docs/performance.md. The "other IMemoryCache" row shows
// the largest case: any IMemoryCache that is not exactly MemoryCache gets the string key lookup,
// which builds the key string on every hit. The same applies on net8.0.
//
// The benchmarks return the proxy's ValueTask directly. An async wrapper method would allocate its
// own Task<T> and hide the proxy's allocation behind it.
//
// Baseline: calling the inner service directly, no caching.
[MemoryDiagnoser]
[SimpleJob]
public class CachedLookupBenchmark
{
    private ICustomerService _direct = null!;
    private ICustomerService _proxied = null!;
    private ICustomerService _proxiedOverOtherCache = null!;
    private MemoryCache _cache = null!;
    private MemoryCache _otherCacheInner = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _otherCacheInner = new MemoryCache(new MemoryCacheOptions());
        _direct = new CustomerService();
        _proxied = new ICustomerServiceCacheProxy(_direct, _cache);
        _proxiedOverOtherCache = new ICustomerServiceCacheProxy(_direct, new ForwardingMemoryCache(_otherCacheInner));

        // Warm the cache for the single key we measure.
        _ = await _proxied.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
        _ = await _proxied.GetScoreAsync(42, CancellationToken.None).ConfigureAwait(false);
        _ = await _proxiedOverOtherCache.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
        _otherCacheInner.Dispose();
    }

    [Benchmark(Baseline = true, Description = "direct (no cache)")]
    public ValueTask<string> Direct()
        => _direct.GetNameAsync(42, CancellationToken.None);

    [Benchmark(Description = "proxied (cache hit)")]
    public ValueTask<string> Proxied()
        => _proxied.GetNameAsync(42, CancellationToken.None);

    // A value-type return unboxes the cached entry on a hit, see #182.
    [Benchmark(Description = "proxied value type (cache hit)")]
    public ValueTask<int> ProxiedValueType()
        => _proxied.GetScoreAsync(42, CancellationToken.None);

    [Benchmark(Description = "proxied, other IMemoryCache (cache hit, string key)")]
    public ValueTask<string> ProxiedOverOtherCache()
        => _proxiedOverOtherCache.GetNameAsync(42, CancellationToken.None);
}

[Cache(TtlMs = 60_000)]
public interface ICustomerService
{
    ValueTask<string> GetNameAsync(int customerId, CancellationToken ct);

    ValueTask<int> GetScoreAsync(int customerId, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    // Built once, so the direct baseline measures the call and not a string per call.
    private static readonly string Name = "customer-42";

    public ValueTask<string> GetNameAsync(int customerId, CancellationToken ct)
        => ValueTask.FromResult(Name);

    public ValueTask<int> GetScoreAsync(int customerId, CancellationToken ct)
        => ValueTask.FromResult(customerId * 10);
}
