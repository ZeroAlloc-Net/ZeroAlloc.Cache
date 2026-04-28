using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.Tests;

[Cache(TtlMs = 60_000, UseHybridCache = true)]
public interface IHybridTestService
{
    ValueTask<string> GetAsync(string id, CancellationToken ct);
}
