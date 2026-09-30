using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests.Warehouse;

public sealed class Lookup : ILookup
{
    public ValueTask<string> GetAsync(int id, CancellationToken ct) => ValueTask.FromResult($"warehouse-{id}");
}
