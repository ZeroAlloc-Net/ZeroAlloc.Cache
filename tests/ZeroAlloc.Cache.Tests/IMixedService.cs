using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #180: one bounded method next to one unbounded, non-hybrid method. The bounded method uses the
/// isolated size-limited cache; the unbounded one uses the shared DI <c>IMemoryCache</c>.
/// </summary>
public interface IMixedService
{
    [Cache(TtlMs = 60_000, MaxEntries = MixedServiceImpl.MaxEntries)]
    ValueTask<string> GetBoundedAsync(string id, CancellationToken ct);

    [Cache(TtlMs = 60_000)]
    ValueTask<string> GetUnboundedAsync(string id, CancellationToken ct);
}
