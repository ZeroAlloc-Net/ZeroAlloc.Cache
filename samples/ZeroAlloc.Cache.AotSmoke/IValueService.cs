using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.AotSmoke;

// Value-type returns: a hit on any of these hung under NativeAOT, see #182. Each nullable
// return uses a different underlying type from every non-nullable one on purpose. Once
// Nullable<T> is constructed anywhere in the image the hang for that T no longer
// reproduces, so a Task<int?> here would mask a regression of ValueTask<int>.
[Cache(TtlMs = 60_000)]
public interface IValueService
{
    ValueTask<int> GetCountAsync(int id, CancellationToken ct);

    Task<long?> GetOptionalTotalAsync(int id, CancellationToken ct);

    ValueTask<Coordinates> GetCoordinatesAsync(int id, CancellationToken ct);

    ValueTask<Temperature?> GetTemperatureAsync(int id, CancellationToken ct);
}
