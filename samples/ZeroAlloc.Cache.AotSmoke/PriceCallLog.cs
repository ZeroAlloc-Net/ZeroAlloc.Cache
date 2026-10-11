using System.Threading;

namespace ZeroAlloc.Cache.AotSmoke;

// Singleton, so calls are counted across the transient inner instances DI creates per resolution.
// HybridCache may run the factory on another thread, hence the interlocked counts.
public sealed class PriceCallLog
{
    private int _labelCalls;
    private int _priceCalls;

    public int LabelCalls => Volatile.Read(ref _labelCalls);
    public int PriceCalls => Volatile.Read(ref _priceCalls);

    public void CountLabelCall() => Interlocked.Increment(ref _labelCalls);
    public void CountPriceCall() => Interlocked.Increment(ref _priceCalls);
}
