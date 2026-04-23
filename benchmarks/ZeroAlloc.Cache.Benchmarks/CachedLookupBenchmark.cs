using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.Benchmarks;

// Measures the overhead of the generator-emitted cache proxy on a CACHE HIT —
// the hot path after the first request has warmed the entry. The claim: the
// key struct is generated at compile time, no object[] boxing, no string.Format
// allocation — so the hit path allocates 0 B/op.
//
// Baseline: calling the inner service directly (no caching). The ratio column
// surfaces how cheap the cache hit is vs. the underlying service call.
[MemoryDiagnoser]
[SimpleJob]
public class CachedLookupBenchmark
{
    private ICustomerService _direct = null!;
    private ICustomerService _proxied = null!;
    private MemoryCache _cache = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _direct = new CustomerService();
        _proxied = new ICustomerServiceCacheProxy(_direct, _cache);
        // Warm the cache for the single key we measure.
        _ = await _proxied.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _cache.Dispose();

    [Benchmark(Baseline = true, Description = "direct (no cache)")]
    public async Task<string> Direct()
        => await _direct.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);

    [Benchmark(Description = "proxied (cache hit)")]
    public async Task<string> Proxied()
        => await _proxied.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
}

[Cache(TtlMs = 60_000)]
public interface ICustomerService
{
    ValueTask<string> GetNameAsync(int customerId, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    public ValueTask<string> GetNameAsync(int customerId, CancellationToken ct)
        => ValueTask.FromResult($"customer-{customerId}");
}
