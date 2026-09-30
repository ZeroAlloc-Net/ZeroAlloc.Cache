using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Tests;

public sealed class KeyTextServiceImpl : IKeyTextService
{
    public int CallCount { get; private set; }

    private string Next() => $"inner-{++CallCount}";

    public ValueTask<string> ByValuesAsync(int a, long b, Guid c, DateTime d, double e, CancellationToken ct) => ValueTask.FromResult(Next());

    public ValueTask<string> ByKindAsync(KeyKind kind, CancellationToken ct) => ValueTask.FromResult(Next());

    public ValueTask<string> ByNullableAsync(int? id, CancellationToken ct) => ValueTask.FromResult(Next());

    public ValueTask<string?> ByTextAsync(string? text, CancellationToken ct) => ValueTask.FromResult<string?>(Next());
}
