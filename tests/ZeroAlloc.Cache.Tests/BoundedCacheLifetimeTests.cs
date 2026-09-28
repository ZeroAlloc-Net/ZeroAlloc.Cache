using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #180: the isolated <c>MaxEntries</c> cache used to be a field of each proxy instance, and the
/// proxy is registered transient, so every resolution started with an empty cache. The cache now
/// lives as long as the container that registered it.
/// </summary>
public sealed class BoundedCacheLifetimeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallLog>();
        services.AddBoundedServiceCache<BoundedServiceImpl>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SecondResolvedProxy_HitsEntryCachedByFirst()
    {
        await using var provider = BuildProvider();
        var log = provider.GetRequiredService<CallLog>();
        var first = provider.GetRequiredService<IBoundedService>();
        var second = provider.GetRequiredService<IBoundedService>();
        first.Should().NotBeSameAs(second, "the proxy is transient; each resolution is a new instance");

        (await first.GetAsync("a", CancellationToken.None)).Should().Be("bounded-a");
        (await second.GetAsync("a", CancellationToken.None)).Should().Be("bounded-a");

        log.Bounded.Should().Be(1, "the second proxy must hit the entry the first proxy cached");
    }

    [Fact]
    public async Task MaxEntries_IsEnforcedAcrossProxyInstances()
    {
        await using var provider = BuildProvider();
        var log = provider.GetRequiredService<CallLog>();
        const int proxies = 5;
        const int keysPerProxy = 2;

        // Populate: every proxy caches its own keys. Together they write more entries than the
        // limit allows.
        var resolved = new IBoundedService[proxies];
        for (int p = 0; p < proxies; p++)
        {
            resolved[p] = provider.GetRequiredService<IBoundedService>();
            for (int k = 0; k < keysPerProxy; k++)
                await resolved[p].GetAsync($"{p}-{k}", CancellationToken.None);
        }
        log.Bounded.Should().Be(proxies * keysPerProxy);

        // Re-read each key once. A hit can only come from an entry written during the populate
        // pass, and a shared bounded cache holds at most MaxEntries of those.
        for (int p = 0; p < proxies; p++)
        {
            for (int k = 0; k < keysPerProxy; k++)
                await resolved[p].GetAsync($"{p}-{k}", CancellationToken.None);
        }
        var hits = (2 * proxies * keysPerProxy) - log.Bounded;

        hits.Should().BeGreaterThan(0, "entries within the limit must be cached");
        hits.Should().BeLessThanOrEqualTo(
            BoundedServiceImpl.MaxEntries,
            "all proxy instances share one cache whose SizeLimit is MaxEntries");
    }

    [Fact]
    public async Task DisposingContainer_DisposesIsolatedCache()
    {
        var provider = BuildProvider();
        var proxy = provider.GetRequiredService<IBoundedService>();
        await proxy.GetAsync("a", CancellationToken.None);

        await provider.DisposeAsync();

        var afterDispose = () => proxy.GetAsync("a", CancellationToken.None).AsTask();
        await afterDispose.Should().ThrowAsync<ObjectDisposedException>(
            "the isolated cache is owned by the container and disposed with it");
    }

    [Fact]
    public async Task SeparateContainers_DoNotShareIsolatedCache()
    {
        await using var one = BuildProvider();
        await using var two = BuildProvider();

        await one.GetRequiredService<IBoundedService>().GetAsync("a", CancellationToken.None);
        await two.GetRequiredService<IBoundedService>().GetAsync("a", CancellationToken.None);

        one.GetRequiredService<CallLog>().Bounded.Should().Be(1);
        two.GetRequiredService<CallLog>().Bounded.Should().Be(
            1, "a second container must start with its own empty cache, not a process-wide one");
    }
}
