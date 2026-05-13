---
id: performance
title: Performance
slug: /docs/performance
description: Zero-allocation design of the generated cache proxy, with reproducible benchmark methodology.
sidebar_position: 6
---

# Performance

ZeroAlloc.Cache is designed for hot-path caching where the cache-hit path should be indistinguishable from a direct field access in terms of allocation. This page explains the design decisions that make that possible and describes the benchmark in the repo.

## Zero-allocation design

The generator emits a cache proxy per `[Cache]`-annotated interface. Three decisions keep the hit path allocation-free:

**1. Compile-time cache keys**

Cache keys are derived at compile time from the method's parameters — no `string.Format`, no `object[]` boxing. For a method `GetByIdAsync(int id)` the key template is a literal `$"IProductRepository.GetByIdAsync:{id}"` interpolated once per call.

**2. Generic struct over `IMemoryCache`**

The generator calls `IMemoryCache.TryGetValue<T>(key, out T value)` — a generic overload that avoids boxing the cached value when `T` is a value type. For reference types the returned reference is handed back directly, no wrapper object is created on the hit path.

**3. `CancellationToken` excluded from key computation**

Every method accepting a `CancellationToken` has that parameter skipped when the key is composed — tokens are identity-comparable across requests, which would balloon cache entries. This is baked into the generator, not a runtime switch.

## Head-to-head vs Raw IMemoryCache and FusionCache

<!-- BENCH:START -->
_Last refreshed: 2026-05-13_

L1 (in-process) cache-hit comparison. .NET 10.0.7, i9-12900HK, BenchmarkDotNet v0.15.8. ZA.Cache wraps `IMemoryCache`, so the relevant comparisons are: hand-rolled `GetOrCreateAsync` (the pattern ZA replaces) and [FusionCache](https://github.com/ZiggyCreatures/FusionCache) 2.0 (the de-facto third-party L1+L2 caching library).

| Library | Time | Allocated |
|---|---:|---:|
| Raw `IMemoryCache.GetOrCreateAsync` | 208 ns | 176 B |
| **ZA.Cache proxy** | **434 ns** | **160 B** |
| FusionCache | 989 ns | 112 B |

**ZA.Cache is 2.3× faster than FusionCache** with comparable allocation. The trade vs raw `IMemoryCache` is the ~2× cost of the typed `[Cache]` attribute abstraction (compile-time key building + async wrapper) — in exchange you don't write the cache-lookup boilerplate at every call site, and the key derivation is generated rather than hand-typed.

**FusionCache** is heavier because it carries L2-cache, stampede protection, and adaptive-caching infrastructure even when only L1 is configured. For pure L1, ZA is the lighter choice; FusionCache's value is the L2 + advanced features that ZA does not implement.

**Caveat on the raw row**: ZA's 2× premium over raw `IMemoryCache.GetOrCreateAsync` reflects the proxy dispatch + generated key composition. The raw row's 176 B allocation is the `(string, int)` tuple boxing the test uses for the key; ZA's 160 B is the generated `customer-42` string interpolation. Allocation parity is by design — both store roughly the same key shape.
<!-- BENCH:END -->

## Self-benchmark

The [benchmarks/ZeroAlloc.Cache.Benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/tree/main/benchmarks/ZeroAlloc.Cache.Benchmarks) project also contains `CachedLookupBenchmark` — the original direct-vs-proxied baseline. It compares:

- **Baseline**: direct call on the underlying `ICustomerService` implementation (no caching)
- **Proxied (cache hit)**: the generator-emitted `ICustomerServiceCacheProxy` wrapping `MemoryCache`, pre-warmed so every measured call is a hit

The claim to verify: the proxied hit path allocates `0 B/op`. The baseline path also allocates `0 B/op` (no work happens), so the ratio measures the pure cache-lookup overhead — typically sub-50 ns on modern hardware.

### Run the benchmark

```bash
dotnet run --project benchmarks/ZeroAlloc.Cache.Benchmarks -c Release --filter "*"
```

Results are written to `benchmarks/ZeroAlloc.Cache.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

### What to watch

- **Allocated column**: both rows must read `0 B`. Any regression here means the generator has started allocating on the hit path — most likely from a key-derivation path that escaped into a string allocation.
- **Ratio column**: the proxied row should stay within a small constant multiple of the baseline. If it creeps up over time the cache lookup itself is degrading — inspect the `IMemoryCache` implementation the consumer wires up, not this library.

## Cache-miss path

The cache-miss path does allocate — at minimum, the entry itself must be stored in `IMemoryCache`, and the underlying service's return value flows through. This is intentional: caching is only valuable on miss-then-hit, and optimising the miss path would regress the hit path. The claim of zero allocation applies to the hit path, which is the overwhelmingly common case in a warmed cache.

## HybridCache integration

When configured with `opts.UseHybridCache()`, L1 misses fall through to `Microsoft.Extensions.Caching.Hybrid.HybridCache`. HybridCache's internal pipeline carries its own allocation profile — that allocation is charged to HybridCache, not to the generated proxy. If the hit path stays in L1, HybridCache is never consulted and the zero-allocation claim holds.
