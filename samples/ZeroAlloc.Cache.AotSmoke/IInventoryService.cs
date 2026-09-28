using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

// A bounded method next to an unbounded one on the same interface, resolved through the
// generated DI extension. Exercises the container-lifetime bounded cache holder under AOT.
// Both return reference types: a value-type cache hit hangs under NativeAOT, see #182.
public interface IInventoryService
{
    [Cache(TtlMs = 60_000, MaxEntries = 100)]
    ValueTask<string> GetStockAsync(int productId, CancellationToken ct);

    [Cache(TtlMs = 60_000)]
    ValueTask<string> GetWarehouseAsync(int productId, CancellationToken ct);
}
