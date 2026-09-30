using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// Interfaces that would share cache keys get keys qualified with their namespace, so they never
/// read each other's entries from the shared cache. #199
/// </summary>
public sealed class SameNamedInterfaceTests
{
    [Fact]
    public async Task SameNamedInterfacesInDifferentNamespaces_CacheSeparately()
    {
        var services = new ServiceCollection();
        Warehouse.CacheServiceCollectionExtensions.AddLookupCache<Warehouse.Lookup>(services);
        Billing.CacheServiceCollectionExtensions.AddLookupCache<Billing.Lookup>(services);
        await using var provider = services.BuildServiceProvider();

        var warehouse = provider.GetRequiredService<Warehouse.ILookup>();
        var billing = provider.GetRequiredService<Billing.ILookup>();

        (await warehouse.GetAsync(1, CancellationToken.None)).Should().Be("warehouse-1");
        // Keyed by the interface name alone, ILookup.GetAsync:1, this would return warehouse-1.
        (await billing.GetAsync(1, CancellationToken.None)).Should().Be("billing-1");

        var cache = provider.GetRequiredService<IMemoryCache>();
        cache.TryGetValue("ZeroAlloc.Cache.Tests.Warehouse.ILookup.GetAsync:1", out object? stored).Should().BeTrue();
        stored.Should().Be("warehouse-1");
    }
}
