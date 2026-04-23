using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

public sealed class CustomerService : ICustomerService
{
    public int CallCount { get; private set; }

    public ValueTask<string> GetNameAsync(int customerId, CancellationToken ct)
    {
        CallCount++;
        return ValueTask.FromResult($"customer-{customerId}");
    }
}
