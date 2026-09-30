using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// Nested [Cache] interfaces with the same name get their own proxies, and their cache keys name
/// their containing types, so they never read each other's entries from the shared cache. #194
/// </summary>
public sealed class NestedInterfaceTests
{
    [Fact]
    public async Task SameNamedNestedInterfaces_CacheSeparately()
    {
        var products = new Catalog.Products.Lookup();
        var services = new ServiceCollection()
            .AddCatalog_Products_LookupCache<Catalog.Products.Lookup>()
            .AddCatalog_Orders_LookupCache<Catalog.Orders.Lookup>()
            .AddCatalog_Customers_LookupCache<Catalog.Customers.Lookup>();
        // Replaces the transient registration of the inner implementation, so every proxy wraps
        // the one instance whose calls are counted.
        services.AddTransient(_ => products);
        await using var provider = services.BuildServiceProvider();

        var productLookup = provider.GetRequiredService<Catalog.Products.ILookup>();
        var orderLookup = provider.GetRequiredService<Catalog.Orders.ILookup>();
        var customerLookup = provider.GetRequiredService<Catalog.Customers.ILookup>();

        (await productLookup.GetAsync(1, CancellationToken.None)).Should().Be("product-1");
        // Same method and argument, same shared IMemoryCache. Keyed by the interface name alone,
        // ILookup.GetAsync:1, this would return product-1.
        (await customerLookup.GetAsync(1, CancellationToken.None)).Should().Be("customer-1");
        (await orderLookup.GetAsync(1, CancellationToken.None)).Should().Be("order-1");
        (await productLookup.GetAsync(1, CancellationToken.None)).Should().Be("product-1");

        products.Calls.Should().Be(1, "the second call is a hit on the entry the first call stored");
    }
}
