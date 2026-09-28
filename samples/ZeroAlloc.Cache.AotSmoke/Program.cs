using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Cache.AotSmoke;

// Exercise the generator-emitted ICustomerServiceCacheProxy under PublishAot=true.
// The proxy must:
//   1. Call the inner service on cache miss
//   2. Return the cached value on cache hit without re-entering the inner service
//   3. Key correctly so different inputs are independent cache entries

using var cache = new MemoryCache(new MemoryCacheOptions());
var impl = new CustomerService();
var proxy = new ICustomerServiceCacheProxy(impl, cache);

var first = await proxy.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
if (!string.Equals(first, "customer-42", StringComparison.Ordinal))
    return Fail($"First call expected 'customer-42', got '{first}'");
if (impl.CallCount != 1)
    return Fail($"After first call, CallCount expected 1, got {impl.CallCount}");

var second = await proxy.GetNameAsync(42, CancellationToken.None).ConfigureAwait(false);
if (!string.Equals(second, "customer-42", StringComparison.Ordinal))
    return Fail($"Second call expected 'customer-42', got '{second}'");
if (impl.CallCount != 1)
    return Fail($"After cache hit, CallCount expected still 1, got {impl.CallCount}");

var other = await proxy.GetNameAsync(99, CancellationToken.None).ConfigureAwait(false);
if (!string.Equals(other, "customer-99", StringComparison.Ordinal))
    return Fail($"Different-key call expected 'customer-99', got '{other}'");
if (impl.CallCount != 2)
    return Fail($"After different key, CallCount expected 2, got {impl.CallCount}");

// A mixed bounded and unbounded interface through the generated DI extension. Two separately
// resolved proxies must share the bounded cache, which lives as long as the container.
var services = new ServiceCollection();
services.AddSingleton<InventoryCallLog>();
services.AddInventoryServiceCache<InventoryService>();
var provider = services.BuildServiceProvider();
await using (provider.ConfigureAwait(false))
{
    var log = provider.GetRequiredService<InventoryCallLog>();
    var inventoryA = provider.GetRequiredService<IInventoryService>();
    var inventoryB = provider.GetRequiredService<IInventoryService>();

    var s1 = await inventoryA.GetStockAsync(7, CancellationToken.None).ConfigureAwait(false);
    var s2 = await inventoryB.GetStockAsync(7, CancellationToken.None).ConfigureAwait(false);
    if (!string.Equals(s1, "stock-7", StringComparison.Ordinal) || !string.Equals(s2, s1, StringComparison.Ordinal))
        return Fail($"Bounded method returned '{s1}' and '{s2}'");
    if (log.StockCalls != 1)
        return Fail($"Bounded method: second proxy expected a cache hit, StockCalls is {log.StockCalls}");

    var w1 = await inventoryA.GetWarehouseAsync(7, CancellationToken.None).ConfigureAwait(false);
    var w2 = await inventoryB.GetWarehouseAsync(7, CancellationToken.None).ConfigureAwait(false);
    if (!string.Equals(w1, "warehouse-7", StringComparison.Ordinal) || !string.Equals(w2, w1, StringComparison.Ordinal))
        return Fail($"Unbounded method returned '{w1}' and '{w2}'");
    if (log.WarehouseCalls != 1)
        return Fail($"Unbounded method: expected a cache hit, WarehouseCalls is {log.WarehouseCalls}");
}

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
