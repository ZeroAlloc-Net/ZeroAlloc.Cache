using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ZeroAlloc.Cache.Tests;

[Collection("cache-telemetry-non-parallel")]
public sealed class TelemetryTests
{
    private const string ActivitySourceName = "ZeroAlloc.Cache";
    private const string MeterName = "ZeroAlloc.Cache";

    [Fact]
    public async Task Lookup_OnHit_StartsActivity_WithCacheHitTrueTag()
    {
        using var listener = new TestActivityListener(ActivitySourceName);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var impl = new TestServiceImpl();
        var proxy = new ITestServiceCacheProxy(impl, cache);

        // Pre-populate (also produces a "miss" activity).
        await proxy.GetAsync("k", CancellationToken.None);
        var afterMiss = listener.StoppedActivities.Count;

        await proxy.GetAsync("k", CancellationToken.None);

        listener.StoppedActivities.Count.Should().BeGreaterThan(afterMiss);
        var hitActivity = listener.StoppedActivities.Last();
        hitActivity.OperationName.Should().Be("cache.lookup");
        hitActivity.GetTagItem("cache.method").Should().Be("ITestService.GetAsync");
        hitActivity.GetTagItem("cache.tier").Should().Be("L1");
        hitActivity.GetTagItem("cache.hit").Should().Be(true);
    }

    [Fact]
    public async Task Lookup_OnMiss_StartsActivity_WithCacheHitFalseTag()
    {
        using var listener = new TestActivityListener(ActivitySourceName);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var impl = new TestServiceImpl();
        var proxy = new ITestServiceCacheProxy(impl, cache);

        await proxy.GetAsync("cold", CancellationToken.None);

        listener.StoppedActivities.Should().HaveCount(1);
        var activity = listener.StoppedActivities[0];
        activity.OperationName.Should().Be("cache.lookup");
        activity.GetTagItem("cache.method").Should().Be("ITestService.GetAsync");
        activity.GetTagItem("cache.tier").Should().Be("L1");
        activity.GetTagItem("cache.hit").Should().Be(false);
    }

    [Fact]
    public async Task Lookup_RecordsLookupDurationHistogram()
    {
        var recorded = new List<(double Value, Dictionary<string, object?> Tags)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, ml) =>
            {
                if (string.Equals(instrument.Meter.Name, MeterName, StringComparison.Ordinal)
                    && string.Equals(instrument.Name, "cache.lookup_duration_ms", StringComparison.Ordinal))
                {
                    ml.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
        {
            var tagDict = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
            // HLQ013 suggests foreach but EPS06 then complains about hidden struct copies
            // when reading Key/Value from a non-readonly KeyValuePair via a ref readonly enumerator.
            // Indexed access reads via locals, which avoids both warnings.
#pragma warning disable HLQ013
            for (var i = 0; i < tags.Length; i++)
#pragma warning restore HLQ013
            {
                var key = tags[i].Key;
                var val = tags[i].Value;
                tagDict[key] = val;
            }
            recorded.Add((value, tagDict));
        });
        meterListener.Start();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var impl = new TestServiceImpl();
        var proxy = new ITestServiceCacheProxy(impl, cache);

        await proxy.GetAsync("any", CancellationToken.None);

        recorded.Should().NotBeEmpty();
        recorded[0].Value.Should().BeGreaterOrEqualTo(0.0);
        recorded[0].Tags.Should().ContainKey("cache.method")
            .WhoseValue.Should().Be("ITestService.GetAsync");
    }

    [Fact]
    public async Task Lookup_HybridCachePath_TagsCacheTierL2_AndOmitsCacheHit()
    {
        using var listener = new TestActivityListener(ActivitySourceName);

        var services = new ServiceCollection();
        services.AddHybridCache();
        services.AddTransient<HybridTestServiceImpl>();
        services.AddTransient<IHybridTestService>(sp =>
            new IHybridTestServiceCacheProxy(
                sp.GetRequiredService<HybridTestServiceImpl>(),
                sp.GetRequiredService<HybridCache>()));
        await using var provider = services.BuildServiceProvider();
        var proxy = provider.GetRequiredService<IHybridTestService>();

        await proxy.GetAsync("h", CancellationToken.None);

        listener.StoppedActivities.Should().NotBeEmpty();
        var activity = listener.StoppedActivities.Last();
        activity.OperationName.Should().Be("cache.lookup");
        activity.GetTagItem("cache.method").Should().Be("IHybridTestService.GetAsync");
        activity.GetTagItem("cache.tier").Should().Be("L2");
        activity.GetTagItem("cache.hit").Should().BeNull();
    }
}
