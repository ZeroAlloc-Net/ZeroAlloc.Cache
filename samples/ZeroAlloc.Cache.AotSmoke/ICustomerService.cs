using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.AotSmoke;

[Cache(TtlMs = 60_000)]
public interface ICustomerService
{
    ValueTask<string> GetNameAsync(int customerId, CancellationToken ct);
}
