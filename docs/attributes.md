# Attribute Reference

## `[Cache]`

```csharp
[Cache(TtlMs = 60_000, Sliding = false, MaxEntries = 0, UseHybridCache = false)]
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TtlMs` | `int` | required | Cache entry lifetime in milliseconds |
| `Sliding` | `bool` | `false` | When `true`, the TTL is reset on each access (sliding expiration). Ignored by the L2 tier when `UseHybridCache = true` — see [ZC0001](diagnostics/ZC0001.md) |
| `MaxEntries` | `int` | `0` (unlimited) | When greater than zero, the method gets its own isolated `MemoryCache` instance with this value as its `SizeLimit`. Useful for bounding memory on high-cardinality methods |
| `UseHybridCache` | `bool` | `false` | When `true`, uses `Microsoft.Extensions.Caching.Hybrid` instead of `IMemoryCache`. Requires `HybridCache` to be registered in DI |

---

## Interface-level vs method-level overrides

`[Cache]` can be placed on the interface, on individual methods, or on both.

**Interface-level only** — all methods use the same policy:

```csharp
[Cache(TtlMs = 60_000)]
public interface IProductRepository
{
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);
    ValueTask<IReadOnlyList<Product>> ListAllAsync(CancellationToken ct);
}
```

**Method-level override** — the method attribute shadows the interface attribute entirely for that method. The properties are not merged:

```csharp
[Cache(TtlMs = 60_000)]
public interface IProductRepository
{
    // uses interface-level: 60 s, IMemoryCache, unlimited entries
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);

    // overrides entirely: 5 min, bounded cache, sliding
    [Cache(TtlMs = 300_000, MaxEntries = 500, Sliding = true)]
    ValueTask<IReadOnlyList<Product>> SearchAsync(string query, CancellationToken ct);
}
```

**Method-level only (no interface attribute)** — only annotated methods are cached; unannotated methods are forwarded directly to the inner implementation:

```csharp
public interface IProductRepository
{
    [Cache(TtlMs = 60_000)]
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);

    // not cached — forwarded directly to inner implementation
    ValueTask SaveAsync(Product product, CancellationToken ct);
}
```

> Method-level attributes **shadow** interface-level ones — they are not additive. If you set `TtlMs` at the interface and only specify `MaxEntries` at the method level, `TtlMs` is not inherited; you must repeat it.
