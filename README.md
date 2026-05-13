# ZeroAlloc.Cache

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Cache.svg)](https://www.nuget.org/packages/ZeroAlloc.Cache)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Source-generated zero-allocation caching proxy from an annotated interface.

Add `[Cache]` to an interface and a Roslyn source generator emits a proxy class that transparently intercepts every method call, returning a cached result on hit with **no heap allocation on the cache-hit path**. Backed by `IMemoryCache` by default, with optional `HybridCache` (L1 + L2) opt-in per method. AOT-safe.

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
builder.Services.AddIProductRepositoryCache<ProductRepositoryImpl>();
```

Inject `IProductRepository` anywhere — caching is transparent to the caller.

```csharp
public class ProductsController(IProductRepository repo)
{
    public async Task<Product?> Get(int id, CancellationToken ct)
        => await repo.GetByIdAsync(id, ct); // cache hit = zero allocation
}
```

---

## Performance

L1 (in-process) cache-hit comparison. .NET 10.0.7, i9-12900HK, BenchmarkDotNet v0.15.8.

| Library | Time | Allocated |
|---|---:|---:|
| Raw `IMemoryCache.GetOrCreateAsync` | 208 ns | 176 B |
| **ZA.Cache proxy** | **434 ns** | **160 B** |
| FusionCache | 989 ns | 112 B |

ZA.Cache is **2.3× faster than FusionCache** with comparable allocation. The ~2× premium over hand-rolled `IMemoryCache.GetOrCreateAsync` is the cost of the typed `[Cache]` attribute abstraction (generated key building + async wrapper) — in exchange you don't write the lookup boilerplate at every call site. FusionCache's overhead comes from carrying L2-cache and stampede-protection infrastructure even when only L1 is configured.

Full methodology + design analysis: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md).

## Features

| Feature | Notes |
|---------|-------|
| Zero allocation on cache hit | Key is built at compile time; no boxing, no string interpolation at runtime |
| `IMemoryCache` (default) | In-process L1 cache; no extra dependencies |
| `HybridCache` (opt-in) | L1 + L2 distributed cache via `Microsoft.Extensions.Caching.Hybrid` |
| Method-level override | Any `[Cache]` on a method shadows the interface-level config for that method |
| `MaxEntries` | Isolates the method in its own `MemoryCache` instance with a `SizeLimit` |
| Compile-time key | Cache key expression is emitted by the generator — zero key-building overhead on hit |
| AOT / trimmer safe | Generated proxy is concrete; no reflection at runtime |
| DI integration | Generated `Add{IService}Cache<TImpl>()` extension registers everything |

---

## Cache behavior

| Scenario | Behavior |
|----------|----------|
| **Miss** | Inner implementation is called; result is stored in cache with the configured TTL; result is returned |
| **Hit** | Cached value is returned directly; inner implementation is never invoked; no heap allocation |

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

---

## Documentation

Full docs live in [`docs/`](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/index.md):

- [Getting Started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/getting-started.md)
- [Attribute Reference](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/attributes.md)
- Diagnostics: [ZC0001](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0001.md) · [ZC0002](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/diagnostics/ZC0002.md)

---

## License

MIT
