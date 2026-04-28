using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public class HybridTestServiceImpl : IHybridTestService
{
    public int CallCount { get; private set; }

    public ValueTask<string> GetAsync(string id, CancellationToken ct)
    {
        CallCount++;
        return ValueTask.FromResult($"hybrid-{id}");
    }
}
