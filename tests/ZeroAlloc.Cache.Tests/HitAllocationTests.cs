using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #185, the allocation gate: a cache hit on a plain <see cref="MemoryCache"/> allocates nothing
/// for a <c>ValueTask&lt;T&gt;</c> method whose key parameters are strings or span-formattable
/// values. The docs claim it, and these tests fail the build when a change breaks it.
/// </summary>
/// <remarks>
/// Serialised with the telemetry tests, whose activity and meter listeners make every call
/// allocate. Each case measures the whole proxy call with <see cref="AllocationGate"/>, which
/// settles the heap, warms up, and fails on any allocation: its per-call figure rounds up.
/// <para>
/// Tiered compilation runs a method unoptimised first. That code boxes value-type key parameters
/// while formatting the key, 104 B per <c>ByValuesAsync</c> hit, and the gate's two warmup calls do
/// not reach optimised code. So each case repeats the gate's measurement until a window reads zero
/// or <see cref="Limit"/> passes, then asserts with the gate. A real allocation never reads zero.
/// </para>
/// </remarks>
[Collection("cache-telemetry-non-parallel")]
public sealed class HitAllocationTests : IDisposable
{
    private const int Iterations = 1_000;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void ReferenceTypeReturn_HitAllocatesNothing()
    {
        var proxy = new ITestServiceCacheProxy(new TestServiceImpl(), _cache);

        AssertHitAllocatesNothing(() => proxy.GetAsync("abc", CancellationToken.None), "result-abc", "GetAsync");
    }

    [Fact]
    public void ValueTypeReturns_HitAllocatesNothing()
    {
        var proxy = new IValueTypeServiceCacheProxy(new ValueTypeServiceImpl(), _cache);

        AssertHitAllocatesNothing(() => proxy.CountAsync("abc", CancellationToken.None), 1, "CountAsync");
        AssertHitAllocatesNothing(() => proxy.MaybeCountAsync("abc", CancellationToken.None), 1, "MaybeCountAsync");
        AssertHitAllocatesNothing(() => proxy.MaybeTotalAsync("abc", CancellationToken.None), new Money(1), "MaybeTotalAsync");
    }

    [Fact]
    public void SpanFormattableKeyParameters_HitAllocatesNothing()
    {
        var proxy = new IKeyTextServiceCacheProxy(new KeyTextServiceImpl(), _cache);
        var guid = Guid.NewGuid();
        var date = DateTime.UtcNow;

        AssertHitAllocatesNothing(() => proxy.ByValuesAsync(7, 8L, guid, date, 1.5, CancellationToken.None), "inner-1", "ByValuesAsync");
        AssertHitAllocatesNothing(() => proxy.ByKindAsync(KeyKind.Second, CancellationToken.None), "inner-2", "ByKindAsync");
    }

    [Fact]
    public void BoundedCache_HitAllocatesNothing()
    {
        using var holder = new IBoundedServiceBoundedCache();
        var proxy = new IBoundedServiceCacheProxy(new BoundedServiceImpl(new CallLog()), holder);

        AssertHitAllocatesNothing(() => proxy.GetAsync("abc", CancellationToken.None), "bounded-abc", "bounded GetAsync");
    }

    // AddMemoryCache registers a plain MemoryCache, so a proxy resolved from DI takes the span lookup.
    [Fact]
    public void ResolvedFromDependencyInjection_HitAllocatesNothing()
    {
        var services = new ServiceCollection();
        services.AddTestServiceCache<TestServiceImpl>();
        using var provider = services.BuildServiceProvider();
        var proxy = provider.GetRequiredService<ITestService>();

        AssertHitAllocatesNothing(() => proxy.GetAsync("abc", CancellationToken.None), "result-abc", "DI-resolved GetAsync");
    }

    // Proves the gate sees an allocation: any other IMemoryCache takes the string key lookup,
    // which allocates the key string on every hit.
    [Fact]
    public void OtherIMemoryCache_HitAllocatesTheKeyString()
    {
        using var decorator = new DecoratingMemoryCache(new MemoryCache(new MemoryCacheOptions()));
        var proxy = new ITestServiceCacheProxy(new TestServiceImpl(), decorator);
        Complete(proxy.GetAsync("abc", CancellationToken.None));

        AllocationGate.MeasureBytesPerCallValueTask(Iterations, () => proxy.GetAsync("abc", CancellationToken.None))
            .Should().BeGreaterThan(0);
    }

    private static void AssertHitAllocatesNothing<T>(Func<ValueTask<T>> hit, T expected, string label)
    {
        Complete(hit()); // the miss that stores the entry
        Complete(hit()).Should().Be(expected, "the second call must be a hit on the stored entry");

        var deadline = Stopwatch.GetTimestamp() + (long)(Limit.TotalSeconds * Stopwatch.Frequency);
        while (AllocationGate.MeasureBytesPerCallValueTask(Iterations, hit) != 0 && Stopwatch.GetTimestamp() < deadline)
        {
            // Unoptimised code still runs; measure again once the JIT has tiered it up.
        }

        AllocationGate.AssertBudgetValueTask(0, Iterations, hit, label);
    }

    // Every call here completes synchronously; the inner services return completed ValueTasks.
    private static T Complete<T>(ValueTask<T> pending)
    {
        if (!pending.IsCompletedSuccessfully)
            throw new InvalidOperationException("The call did not complete synchronously.");
        return pending.Result;
    }
}
