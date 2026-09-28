using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

public sealed class InventoryService(InventoryCallLog log) : IInventoryService
{
    public ValueTask<string> GetStockAsync(int productId, CancellationToken ct)
    {
        log.StockCalls++;
        return ValueTask.FromResult($"stock-{productId}");
    }

    public ValueTask<string> GetWarehouseAsync(int productId, CancellationToken ct)
    {
        log.WarehouseCalls++;
        return ValueTask.FromResult($"warehouse-{productId}");
    }
}
