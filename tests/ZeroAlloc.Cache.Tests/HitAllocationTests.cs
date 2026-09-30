using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #185, the allocation gate: a cache hit on a plain <see cref="MemoryCache"/> allocates nothing
/// for a <c>ValueTask&lt;T&gt;</c> method whose key parameters are strings or span-formattable
/// values. The docs claim it, and these tests fail the build when a change breaks it.
/// </summary>
/// <remarks>
/// Serialised with the telemetry tests, whose activity and meter listeners make every call
/// allocate. Tiered compilation runs a method unoptimised first, and that code boxes value types
/// while formatting the key, so each case repeats a window of hits until one reads zero, and fails
/// only if none does within the time limit. A real allocation never reads zero.
/// </remarks>
[Collection("cache-telemetry-non-parallel")]
public sealed class HitAllocationTests : IDisposable
{
    private const int Window = 1_000;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void ReferenceTypeReturn_HitAllocatesNothing()
    {
        var proxy = new ITestServiceCacheProxy(new TestServiceImpl(), _cache);

        SteadyStateAllocation(() => proxy.GetAsync("abc", CancellationToken.None), "result-abc").Should().Be(0);
    }

    [Fact]
    public void ValueTypeReturns_HitAllocatesNothing()
    {
        var proxy = new IValueTypeServiceCacheProxy(new ValueTypeServiceImpl(), _cache);

        SteadyStateAllocation(() => proxy.CountAsync("abc", CancellationToken.None), 1).Should().Be(0);
        SteadyStateAllocation(() => proxy.MaybeCountAsync("abc", CancellationToken.None), 1).Should().Be(0);
        SteadyStateAllocation(() => proxy.MaybeTotalAsync("abc", CancellationToken.None), new Money(1)).Should().Be(0);
    }

    [Fact]
    public void SpanFormattableKeyParameters_HitAllocatesNothing()
    {
        var proxy = new IKeyTextServiceCacheProxy(new KeyTextServiceImpl(), _cache);
        var guid = Guid.NewGuid();
        var date = DateTime.UtcNow;

        SteadyStateAllocation(() => proxy.ByValuesAsync(7, 8L, guid, date, 1.5, CancellationToken.None), "inner-1").Should().Be(0);
        SteadyStateAllocation(() => proxy.ByKindAsync(KeyKind.Second, CancellationToken.None), "inner-2").Should().Be(0);
    }

    [Fact]
    public void BoundedCache_HitAllocatesNothing()
    {
        using var holder = new IBoundedServiceBoundedCache();
        var proxy = new IBoundedServiceCacheProxy(new BoundedServiceImpl(new CallLog()), holder);

        SteadyStateAllocation(() => proxy.GetAsync("abc", CancellationToken.None), "bounded-abc").Should().Be(0);
    }

    // AddMemoryCache registers a plain MemoryCache, so a proxy resolved from DI takes the span lookup.
    [Fact]
    public void ResolvedFromDependencyInjection_HitAllocatesNothing()
    {
        var services = new ServiceCollection();
        services.AddTestServiceCache<TestServiceImpl>();
        using var provider = services.BuildServiceProvider();
        var proxy = provider.GetRequiredService<ITestService>();

        SteadyStateAllocation(() => proxy.GetAsync("abc", CancellationToken.None), "result-abc").Should().Be(0);
    }

    // Proves the gate sees an allocation: any other IMemoryCache takes the string key lookup,
    // which allocates the key string on every hit.
    [Fact]
    public void OtherIMemoryCache_HitAllocatesTheKeyString()
    {
        using var decorator = new DecoratingMemoryCache(new MemoryCache(new MemoryCacheOptions()));
        var proxy = new ITestServiceCacheProxy(new TestServiceImpl(), decorator);
        Complete(proxy.GetAsync("abc", CancellationToken.None));

        MeasureWindow(() => proxy.GetAsync("abc", CancellationToken.None)).Should().BeGreaterThan(0);
    }

    // Bytes allocated over one window of hits, once a window reads zero or the limit passes.
    private static long SteadyStateAllocation<T>(Func<ValueTask<T>> hit, T expected)
    {
        Complete(hit()); // the miss that stores the entry
        Complete(hit()).Should().Be(expected, "the second call must be a hit on the stored entry");

        var deadline = Stopwatch.GetTimestamp() + (long)(Limit.TotalSeconds * Stopwatch.Frequency);
        long bytes;
        do
        {
            bytes = MeasureWindow(hit);
        }
        while (bytes != 0 && Stopwatch.GetTimestamp() < deadline);
        return bytes;
    }

    private static long MeasureWindow<T>(Func<ValueTask<T>> hit)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Window; i++)
            _ = Complete(hit());
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Every call here completes synchronously; the inner services return completed ValueTasks.
    private static T Complete<T>(ValueTask<T> pending)
    {
        if (!pending.IsCompletedSuccessfully)
            throw new InvalidOperationException("The call did not complete synchronously.");
        return pending.Result;
    }
}
