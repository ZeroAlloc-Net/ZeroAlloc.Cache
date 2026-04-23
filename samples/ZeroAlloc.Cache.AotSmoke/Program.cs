using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using ZeroAlloc.Cache.AotSmoke;

// Exercise the generator-emitted ICustomerServiceCacheProxy under PublishAot=true.
// The proxy must:
//   1. Call the inner service on cache miss
//   2. Return the cached value on cache hit without re-entering the inner service
//   3. Key correctly so different inputs are independent cache entries

using var cache = new MemoryCache(new MemoryCacheOptions());
var impl = new CustomerService();
var proxy = new ICustomerServiceCacheProxy(impl, cache);

// Cache miss — inner called
var first = await proxy.GetNameAsync(42, CancellationToken.None);
if (!string.Equals(first, "customer-42", StringComparison.Ordinal))
    return Fail($"First call expected 'customer-42', got '{first}'");
if (impl.CallCount != 1)
    return Fail($"After first call, CallCount expected 1, got {impl.CallCount}");

// Cache hit — inner NOT called
var second = await proxy.GetNameAsync(42, CancellationToken.None);
if (!string.Equals(second, "customer-42", StringComparison.Ordinal))
    return Fail($"Second call expected 'customer-42', got '{second}'");
if (impl.CallCount != 1)
    return Fail($"After cache hit, CallCount expected still 1, got {impl.CallCount}");

// Different key → cache miss
var other = await proxy.GetNameAsync(99, CancellationToken.None);
if (!string.Equals(other, "customer-99", StringComparison.Ordinal))
    return Fail($"Different-key call expected 'customer-99', got '{other}'");
if (impl.CallCount != 2)
    return Fail($"After different key, CallCount expected 2, got {impl.CallCount}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
