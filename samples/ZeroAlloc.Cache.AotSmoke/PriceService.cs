using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

public sealed class PriceService(PriceCallLog log) : IPriceService
{
    public ValueTask<string> GetLabelAsync(int productId, CancellationToken ct)
    {
        log.CountLabelCall();
        return ValueTask.FromResult($"label-{productId}");
    }

    public ValueTask<int> GetPriceAsync(int productId, CancellationToken ct)
    {
        log.CountPriceCall();
        return ValueTask.FromResult(productId * 100);
    }
}
