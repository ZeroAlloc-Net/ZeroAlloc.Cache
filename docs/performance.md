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

## Benchmark

The [benchmarks/ZeroAlloc.Cache.Benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/tree/main/benchmarks/ZeroAlloc.Cache.Benchmarks) project contains a single representative measurement: `CachedLookupBenchmark`. It compares:

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
