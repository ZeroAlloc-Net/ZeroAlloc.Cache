using ZeroAlloc.Cache;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

[Cache(TtlMs = 60_000)]
public interface ITestService
{
    ValueTask<string> GetAsync(string id, CancellationToken ct);
    ValueTask SaveAsync(string data, CancellationToken ct);
}
