using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.AotSmoke;

// Id 0 returns null from the nullable methods, so a cached null is exercised too.
public sealed class ValueService : IValueService
{
    public int CountCalls { get; private set; }
    public int TotalCalls { get; private set; }
    public int CoordinatesCalls { get; private set; }
    public int TemperatureCalls { get; private set; }

    public ValueTask<int> GetCountAsync(int id, CancellationToken ct)
    {
        CountCalls++;
        return ValueTask.FromResult(id * 10);
    }

    public Task<long?> GetOptionalTotalAsync(int id, CancellationToken ct)
    {
        TotalCalls++;
        return Task.FromResult(id == 0 ? null : (long?)(id * 100L));
    }

    public ValueTask<Coordinates> GetCoordinatesAsync(int id, CancellationToken ct)
    {
        CoordinatesCalls++;
        return ValueTask.FromResult(new Coordinates(id, -id));
    }

    public ValueTask<Temperature?> GetTemperatureAsync(int id, CancellationToken ct)
    {
        TemperatureCalls++;
        return ValueTask.FromResult(id == 0 ? null : (Temperature?)new Temperature(id));
    }
}
