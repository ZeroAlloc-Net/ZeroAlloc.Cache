using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Cache;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// Value-type returns, for #122. Every other service in this suite returns
/// <c>ValueTask&lt;string&gt;</c>, and that reference-type monoculture is why the broken
/// <c>return __cached!;</c> hit path shipped — the null-forgiving operator hides the problem
/// for a class and cannot solve it for a struct.
/// </summary>
[Cache(TtlMs = 60_000)]
public interface IValueTypeService
{
    ValueTask<int> CountAsync(string id, CancellationToken ct);

    ValueTask<int?> MaybeCountAsync(string id, CancellationToken ct);

    ValueTask<Money> TotalAsync(string id, CancellationToken ct);
}
