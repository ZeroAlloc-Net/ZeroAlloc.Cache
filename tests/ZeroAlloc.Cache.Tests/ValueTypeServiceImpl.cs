using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// Returns a different value on every call. The proxy owns its own instance, so call counts are
/// not observable from the test; a changing result is, and it distinguishes a real cache hit from
/// a passthrough that simply re-invokes the inner method — the doubt raised in #121.
/// </summary>
public sealed class ValueTypeServiceImpl : IValueTypeService
{
    private int _count;
    private int _maybe;
    private decimal _total;

    public ValueTask<int> CountAsync(string id, CancellationToken ct)
        => ValueTask.FromResult(++_count);

    public ValueTask<int?> MaybeCountAsync(string id, CancellationToken ct)
        => ValueTask.FromResult<int?>(++_maybe);

    public ValueTask<Money> TotalAsync(string id, CancellationToken ct)
        => ValueTask.FromResult(new Money(++_total));
}
