using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public class TestServiceImpl : ITestService
{
    public int CallCount { get; private set; }

    public ValueTask<string> GetAsync(string id, CancellationToken ct)
    {
        CallCount++;
        return ValueTask.FromResult($"result-{id}");
    }

    public ValueTask SaveAsync(string data, CancellationToken ct) => ValueTask.CompletedTask;
}
