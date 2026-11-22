using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Cache;

// Uses the DI extension and proxy the bundled generator emits. If the compiler skips the generator,
// AddCustomerServiceCache does not exist and this does not build.
var services = new ServiceCollection();
services.AddMemoryCache();
services.AddCustomerServiceCache<CustomerServiceImpl>();
using var provider = services.BuildServiceProvider();

var service = provider.GetRequiredService<ICustomerService>();
var first = await service.GetNameAsync(7, CancellationToken.None);
var second = await service.GetNameAsync(7, CancellationToken.None);

if (first != "customer-7" || second != "customer-7" || CustomerServiceImpl.Calls != 1)
{
    Console.Error.WriteLine($"SDK 10 consumer: the generated cache proxy misbehaved, inner calls {CustomerServiceImpl.Calls}.");
    return 1;
}

Console.WriteLine("SDK 10 consumer: generator loaded and the generated cache proxy caches.");
return 0;

[Cache(TtlMs = 60_000)]
public interface ICustomerService
{
    ValueTask<string> GetNameAsync(int id, CancellationToken ct);
}

public sealed class CustomerServiceImpl : ICustomerService
{
    public static int Calls { get; private set; }

    public ValueTask<string> GetNameAsync(int id, CancellationToken ct)
    {
        Calls++;
        return ValueTask.FromResult($"customer-{id}");
    }
}
