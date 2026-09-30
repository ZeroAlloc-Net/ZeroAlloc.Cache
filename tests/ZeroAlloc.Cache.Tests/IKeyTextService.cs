using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// #185: key parameters of many kinds, so tests can prove the span key lookup formats exactly the
/// text the string key has always had, and that a hit with span-formattable keys allocates nothing.
/// </summary>
[Cache(TtlMs = 60_000)]
public interface IKeyTextService
{
    ValueTask<string> ByValuesAsync(int a, long b, Guid c, DateTime d, double e, CancellationToken ct);

    ValueTask<string> ByKindAsync(KeyKind kind, CancellationToken ct);

    ValueTask<string> ByNullableAsync(int? id, CancellationToken ct);

    ValueTask<string?> ByTextAsync(string? text, CancellationToken ct);
}
