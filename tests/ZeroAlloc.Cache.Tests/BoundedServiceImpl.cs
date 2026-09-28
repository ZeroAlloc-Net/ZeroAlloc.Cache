using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public sealed class BoundedServiceImpl(CallLog log) : IBoundedService
{
    public const int MaxEntries = 3;

    public ValueTask<string> GetAsync(string id, CancellationToken ct)
    {
        log.RecordBounded();
        return ValueTask.FromResult($"bounded-{id}");
    }
}
