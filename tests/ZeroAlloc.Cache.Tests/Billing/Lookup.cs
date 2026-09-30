using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests.Billing;

public sealed class Lookup : ILookup
{
    public ValueTask<string> GetAsync(int id, CancellationToken ct) => ValueTask.FromResult($"billing-{id}");
}
