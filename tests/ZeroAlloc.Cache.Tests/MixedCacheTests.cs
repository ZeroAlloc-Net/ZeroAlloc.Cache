using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #180: an interface mixing a <c>MaxEntries &gt; 0</c> method with a non-hybrid
/// <c>MaxEntries = 0</c> method did not compile. The bounded method uses the isolated cache; the
/// unbounded one uses the shared DI <see cref="IMemoryCache"/>, as it would on its own.
/// </summary>
public sealed class MixedCacheTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallLog>();
        services.AddMixedServiceCache<MixedServiceImpl>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task BothMethods_AreCached()
    {
        await using var provider = BuildProvider();
        var log = provider.GetRequiredService<CallLog>();
        var proxy = provider.GetRequiredService<IMixedService>();

        (await proxy.GetBoundedAsync("a", CancellationToken.None)).Should().Be("bounded-a");
        (await proxy.GetBoundedAsync("a", CancellationToken.None)).Should().Be("bounded-a");
        (await proxy.GetUnboundedAsync("a", CancellationToken.None)).Should().Be("unbounded-a");
        (await proxy.GetUnboundedAsync("a", CancellationToken.None)).Should().Be("unbounded-a");

        log.Bounded.Should().Be(1);
        log.Unbounded.Should().Be(1);
    }

    [Fact]
    public async Task UnboundedMethod_UsesSharedMemoryCache()
    {
        await using var provider = BuildProvider();
        var proxy = provider.GetRequiredService<IMixedService>();

        await proxy.GetUnboundedAsync("a", CancellationToken.None);

        var shared = provider.GetRequiredService<IMemoryCache>();
        shared.TryGetValue("IMixedService.GetUnboundedAsync:a", out string? value).Should().BeTrue();
        value.Should().Be("unbounded-a");
    }

    [Fact]
    public async Task BoundedMethod_DoesNotWriteToSharedMemoryCache()
    {
        await using var provider = BuildProvider();
        var proxy = provider.GetRequiredService<IMixedService>();

        await proxy.GetBoundedAsync("a", CancellationToken.None);

        var shared = provider.GetRequiredService<IMemoryCache>();
        shared.TryGetValue("IMixedService.GetBoundedAsync:a", out string? _).Should().BeFalse();
    }

    [Fact]
    public async Task UnboundedEntries_DoNotCountAgainstBoundedSizeLimit()
    {
        await using var provider = BuildProvider();
        var log = provider.GetRequiredService<CallLog>();
        var proxy = provider.GetRequiredService<IMixedService>();

        // Far more unbounded entries than the bounded limit.
        for (int i = 0; i < MixedServiceImpl.MaxEntries * 10; i++)
            await proxy.GetUnboundedAsync($"u{i}", CancellationToken.None);

        // Fill the bounded cache exactly to its limit, then read it back.
        for (int i = 0; i < MixedServiceImpl.MaxEntries; i++)
            await proxy.GetBoundedAsync($"b{i}", CancellationToken.None);
        for (int i = 0; i < MixedServiceImpl.MaxEntries; i++)
            await proxy.GetBoundedAsync($"b{i}", CancellationToken.None);

        log.Bounded.Should().Be(
            MixedServiceImpl.MaxEntries,
            "every bounded entry fits: the unbounded entries live in the shared cache, not the bounded one");
    }
}
