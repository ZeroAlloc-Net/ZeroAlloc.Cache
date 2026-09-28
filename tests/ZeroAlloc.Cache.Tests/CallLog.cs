using System.Threading;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// The call counter is a DI singleton so it survives across the transient inner instances the
/// container creates for each proxy resolution.
/// </summary>
public sealed class CallLog
{
    private int _bounded;
    private int _unbounded;

    public int Bounded => Volatile.Read(ref _bounded);
    public int Unbounded => Volatile.Read(ref _unbounded);

    public void RecordBounded() => Interlocked.Increment(ref _bounded);
    public void RecordUnbounded() => Interlocked.Increment(ref _unbounded);
}
