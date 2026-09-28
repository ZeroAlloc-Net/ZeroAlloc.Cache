using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

/// <summary>Bounded-only interface: every cached method uses the isolated size-limited cache.</summary>
[Cache(TtlMs = 60_000, MaxEntries = BoundedServiceImpl.MaxEntries)]
public interface IBoundedService
{
    ValueTask<string> GetAsync(string id, CancellationToken ct);
}
