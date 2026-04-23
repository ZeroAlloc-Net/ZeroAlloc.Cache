using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.AotSmoke;

[Cache(TtlMs = 60_000)]
public interface ICustomerService
{
    ValueTask<string> GetNameAsync(int customerId, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    public int CallCount { get; private set; }

    public ValueTask<string> GetNameAsync(int customerId, CancellationToken ct)
    {
        CallCount++;
        return ValueTask.FromResult($"customer-{customerId}");
    }
}
