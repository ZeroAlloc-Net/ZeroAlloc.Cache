# ZeroAlloc.Cache

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Cache.svg)](https://www.nuget.org/packages/ZeroAlloc.Cache)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Source-generated zero-allocation caching proxy from an annotated interface.

Add `[Cache]` to an interface and a Roslyn source generator emits a proxy class that transparently intercepts every method call, returning a cached result on hit with **no heap allocation on the cache-hit path** for `ValueTask<T>` methods on the default `MemoryCache` on .NET 9 and later. The few cases that still allocate are [listed](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md#where-a-hit-still-allocates). Backed by `IMemoryCache` by default, with optional `HybridCache` (L1 + L2) opt-in per method. AOT-safe.

---

## Quick start

```bash
dotnet add package ZeroAlloc.Cache
```

```csharp
[Cache(TtlMs = 60_000)]
public interface IProductRepository
{
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);

    [Cache(TtlMs = 300_000, MaxEntries = 1_000)]
    ValueTask<IReadOnlyList<Product>> SearchAsync(string query, CancellationToken ct);
}

// Register — one line wires everything
builder.Services.AddProductRepositoryCache<ProductRepositoryImpl>();
```

Inject `IProductRepository` anywhere — caching is transparent to the caller.

```csharp
public class ProductsController(IProductRepository repo)
{
    public async Task<Product?> Get(int id, CancellationToken ct)
        => await repo.GetByIdAsync(id, ct); // the proxy's cache hit allocates nothing
}
```

---

## Performance

L1 (in-process) cache-hit comparison. .NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8.

| Library | Time | Allocated |
|---|---:|---:|
| Raw `IMemoryCache.GetOrCreateAsync` | 157 ns | 104 B |
| **ZA.Cache proxy** | **198 ns** | **0 B** |
| FusionCache | 1,270 ns | 88 B |

ZA.Cache is **about 6× faster than FusionCache and allocates nothing on the hit**. The ~1.3× premium over hand-rolled `IMemoryCache.GetOrCreateAsync` is the cost of the typed `[Cache]` attribute abstraction (proxy dispatch, key formatting, telemetry) — in exchange you don't write the lookup boilerplate at every call site. FusionCache's overhead comes from carrying L2-cache and stampede-protection infrastructure even when only L1 is configured.

Full methodology + design analysis: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md).

## Features

| Feature | Notes |
|---------|-------|
| Zero allocation on cache hit | On .NET 9+ with `MemoryCache`, a hit formats the key into a stack buffer and looks it up as a span, so a `ValueTask<T>` hit allocates nothing. [Where a hit still allocates](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md#where-a-hit-still-allocates) |
| `IMemoryCache` (default) | In-process L1 cache; no extra dependencies |
| `HybridCache` (opt-in) | L1 + L2 distributed cache via `Microsoft.Extensions.Caching.Hybrid` |
| Method-level override | Any `[Cache]` on a method shadows the interface-level config for that method |
| `MaxEntries` | Moves the method to an isolated `MemoryCache` with a `SizeLimit`, shared by all bounded methods of the interface for the lifetime of the container |
| Compile-time key | The key expression is emitted by the generator. The key text is `Interface.Method:arg1:arg2`, the same on every path, so an entry can be read or removed by it, see [Cache keys](#cache-keys) |
| AOT / trimmer safe | Generated proxy is concrete; no reflection at runtime |
| DI integration | Generated `Add{Service}Cache<TImpl>()` extension registers everything, e.g. `AddProductRepositoryCache` for `IProductRepository` |

---

## Cache behavior

| Scenario | Behavior |
|----------|----------|
| **Miss** | Inner implementation is called; result is stored in cache with the configured TTL; result is returned |
| **Hit** | Cached value is returned directly; inner implementation is never invoked; no heap allocation, [with exceptions](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md#where-a-hit-still-allocates) |

### Cache keys

Each entry is stored under the string `{Interface}.{Method}:{arg1}:{arg2}`, built from every parameter except the `CancellationToken`, each formatted with the current culture. A method without key parameters uses `{Interface}.{Method}`. To evict an entry from the shared `IMemoryCache`, remove it by that string:

```csharp
cache.Remove($"IProductRepository.GetByIdAsync:{id}");
```

`HybridCache` methods use the same key text. For an interface nested in another type, `{Interface}` starts with the containing types, as in `Catalog.IProductLookup.GetByIdAsync:42`.

---

## Telemetry

Each cached method emits both metrics (via `Meter("ZeroAlloc.Cache")`) and a tracing span (via `ActivitySource("ZeroAlloc.Cache")`) — no extra package required, plain BCL `System.Diagnostics`.

> **Breaking change in 2.0:** `Meter` name renamed from `"zeroalloc.cache"` to `"ZeroAlloc.Cache"` for ecosystem consistency with the other ZeroAlloc telemetry packages. Subscribers must update — calls to `AddMeter("zeroalloc.cache")` will silently stop receiving metrics:
>
> ```diff
> -services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("zeroalloc.cache"));
> +services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("ZeroAlloc.Cache"));
> ```

**Metrics.** Counters tagged with `method` (the cached method name): `cache.hits`, `cache.misses`, `cache.evictions`, `cache.hybrid_calls` (factory invocations on the HybridCache path). The `cache.lookup_duration_ms` histogram records per-lookup latency tagged with `cache.method`.

**Tracing.** Each cached method emits a `cache.lookup` span tagged with:

| Tag | Value | Notes |
|-----|-------|-------|
| `cache.method` | `"Interface.Method"` | Compile-time constant per emitted method |
| `cache.tier` | `"L1"` or `"L2"` | `L1` = in-process `MemoryCache`; `L2` = `HybridCache` |
| `cache.hit` | `true` / `false` | L1 only — `HybridCache` hides per-call hit/miss state, so this tag is omitted on the L2 path |

Subscribe via OpenTelemetry:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("ZeroAlloc.Cache"))
    .WithTracing(t => t.AddSource("ZeroAlloc.Cache"));
```

---

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| [ZC0001](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0001.md) | Warning | `Sliding = true` combined with `UseHybridCache = true` — sliding TTL is silently ignored by the distributed (L2) tier |
| [ZC0002](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0002.md) | Warning | A cache key parameter is a reference type (excluding `string`) — `ToString()` may not produce a stable unique key |
| [ZC0003](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0003.md) | Error | `UseHybridCache = true` without a reference to `Microsoft.Extensions.Caching.Hybrid` — no proxy is generated for the interface |
| [ZC0004](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0004.md) | Warning | Bounded methods on one interface set different `MaxEntries` values — they share one size-limited cache sized by the first bounded method |
| [ZC0005](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0005.md) | Warning | `[Cache]` on a method that does not return `Task<T>` or `ValueTask<T>` — caching is not applied |
| [ZC0006](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0006.md) | Warning | A nested interface whose containing type is not `partial` — no proxy is generated for it |
| [ZC0007](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0007.md) | Warning | A generic interface, or one nested in a generic type — no proxy is generated for it |
| [ZC0008](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0008.md) | Warning | A private or protected nested interface, which the namespace-level `Add…Cache` method cannot name — no proxy is generated for it |
| [ZC0009](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0009.md) | Error | A file-local interface — no proxy is generated for it |
| [ZC0010](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0010.md) | Error | An interface whose name differs only in case from another's — only the first is generated |

---

## Documentation

Full docs live in [`docs/`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/index.md):

- [Getting Started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/getting-started.md)
- [Attribute Reference](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/attributes.md)
- Diagnostics: [ZC0001](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0001.md) · [ZC0002](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0002.md) · [ZC0003](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0003.md) · [ZC0004](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0004.md) · [ZC0005](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0005.md) · [ZC0006](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0006.md) · [ZC0007](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0007.md) · [ZC0008](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0008.md) · [ZC0009](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0009.md) · [ZC0010](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0010.md)

---

## License

MIT
