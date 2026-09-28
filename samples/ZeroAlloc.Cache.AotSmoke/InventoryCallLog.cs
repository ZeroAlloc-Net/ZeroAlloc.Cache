namespace ZeroAlloc.Cache.AotSmoke;

// Singleton, so calls are counted across the transient inner instances DI creates per resolution.
public sealed class InventoryCallLog
{
    public int StockCalls { get; set; }
    public int WarehouseCalls { get; set; }
}
