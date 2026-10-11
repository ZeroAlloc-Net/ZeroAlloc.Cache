using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

// HybridCache methods, resolved through the generated DI extension, which registers
// AddHybridCache. A string return and a value-type return: HybridCache serializes the int on a
// miss with the serializer registered for it, PriceSerializer, which ZC0011 asks for under AOT.
public interface IPriceService
{
    [Cache(TtlMs = 60_000, UseHybridCache = true)]
    ValueTask<string> GetLabelAsync(int productId, CancellationToken ct);

    [Cache(TtlMs = 60_000, UseHybridCache = true)]
    ValueTask<int> GetPriceAsync(int productId, CancellationToken ct);
}
