using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public sealed class MixedServiceImpl(CallLog log) : IMixedService
{
    public const int MaxEntries = 3;

    public ValueTask<string> GetBoundedAsync(string id, CancellationToken ct)
    {
        log.RecordBounded();
        return ValueTask.FromResult($"bounded-{id}");
    }

    public ValueTask<string> GetUnboundedAsync(string id, CancellationToken ct)
    {
        log.RecordUnbounded();
        return ValueTask.FromResult($"unbounded-{id}");
    }
}
