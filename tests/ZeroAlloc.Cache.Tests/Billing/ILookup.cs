using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests.Billing;

/// <summary>Shares its name and method with the other ILookup of SameNamedInterfaceTests. #199</summary>
[Cache(TtlMs = 60_000)]
public interface ILookup
{
    ValueTask<string> GetAsync(int id, CancellationToken ct);
}
