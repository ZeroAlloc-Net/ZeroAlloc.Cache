---
id: performance
title: Performance
slug: /docs/performance
description: Zero-allocation design of the generated cache proxy, where a hit still allocates, and reproducible benchmark methodology.
sidebar_position: 6
---

# Performance

ZeroAlloc.Cache is designed for hot-path caching, where a cache hit should not allocate. On .NET 9 and later, a hit on a `ValueTask<T>` method backed by the default `MemoryCache` allocates nothing. This page explains how, lists every case where a hit still allocates, and describes the benchmarks and the test that guard the claim.

## How a cache hit avoids allocation

The generator emits a cache proxy per `[Cache]`-annotated interface. Four decisions keep the hit path allocation-free.

**1. The key is looked up as a span, not built as a string**

Every entry is stored under the string key `{Interface}.{Method}:{arg1}:{arg2}`, for example `IProductRepository.GetByIdAsync:42`. For an interface nested in another type, `{Interface}` starts with the containing types, as in `Catalog.IProductLookup.GetByIdAsync:42`. On a hit the proxy does not build that string. It formats the same text into a 256-character stack buffer with `MemoryExtensions.TryWrite` and looks it up with `MemoryCache.TryGetValue(ReadOnlySpan<char>, out object?)`, which finds the entry stored under the equal string. The formatting is the same as the string key's, current culture included, so the stored key text is unchanged and entries can still be read or removed by it.

The generator emits the span lookup only when the API exists: `MemoryCache.TryGetValue(ReadOnlySpan<char>, ...)` ships in the net9.0 and later builds of Microsoft.Extensions.Caching.Memory 9.0+. At run time the proxy uses it only when the injected `IMemoryCache` is exactly `MemoryCache`, which is what `AddMemoryCache()` registers. A subclass or decorator may re-implement the lookup, so it keeps the string key lookup. The size-limited cache of `MaxEntries` methods is always a `MemoryCache`.

**2. The entry is read as `object` and tested against the concrete type**

The proxy calls the non-generic `TryGetValue(key, out object?)` and tests the entry with a type pattern, such as `__boxed is int __cached`. Unboxing a value type does not allocate, and for a reference type the stored reference is returned as it is. The generic `TryGetValue<TItem>` is avoided on purpose: with a `Nullable<T>` type argument it never returns under NativeAOT, see [#182](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/182).

**3. A `ValueTask<T>` hit completes synchronously**

The proxy method is `async`, but a hit returns before the first `await`, so a `ValueTask<T>` completes synchronously and no task object is created.

**4. `CancellationToken` is excluded from the key**

Every method accepting a `CancellationToken` has that parameter skipped when the key is composed. Tokens differ per request, which would make every call a miss. This is baked into the generator, not a runtime switch.

## Where a hit still allocates

A hit allocates in these cases. `CachedLookupBenchmark` and the `HitAllocationTests` gate measure the allocation-free path; the sizes below are measured on .NET 10 x64.

| Case | What a hit allocates |
|---|---|
| The method returns `Task<T>` | The `Task<T>`, 72 B for a `Task<string>`, except for the few results the runtime caches, such as `null` and `false`. Return `ValueTask<T>` instead. |
| The consumer targets net8.0 or netstandard2.0 | The key string, because Microsoft.Extensions.Caching.Memory has no span lookup there: 88 B for `ICustomerService.GetNameAsync:42`, more for longer keys. |
| The `IMemoryCache` is not exactly `MemoryCache` | The key string, as above: the proxy cannot bypass a decorator's or subclass's lookup. |
| The method uses `UseHybridCache = true` | The key string, because `HybridCache` takes only string keys, plus whatever `HybridCache` allocates itself. |
| A key parameter is a nullable value type, such as `int?` | The boxed value, 24 B for an `int?`, because string interpolation boxes a `Nullable<T>`. |
| A key parameter is a reference type other than `string`, or a value type that does not implement `ISpanFormattable` | The string its `ToString()` returns. Such reference types also raise [ZC0002](diagnostics/ZC0002.md). |
| The key is longer than 256 characters | The key string: the lookup falls back to it when the stack buffer is too small. |
| A listener is attached to the `ZeroAlloc.Cache` `ActivitySource` or `Meter` | The `Activity` and the measurement state the listener asks for. Without a listener, telemetry allocates nothing. |
| The method has not been optimised by the JIT yet | Shortly after start-up, unoptimised code boxes value-type key parameters while formatting the key. This stops once tiered compilation optimises the method, and never happens under NativeAOT. |

`string`, `int`, `long`, `Guid`, `DateTime`, `double`, `decimal`, enums and other `ISpanFormattable` value types format into the stack buffer without allocating. A method without key parameters has a constant key string, which never allocates on any path.

## Head-to-head vs Raw IMemoryCache and FusionCache

<!-- BENCH:START -->
_Last refreshed: 2026-09-30_

L1 (in-process) cache-hit comparison. .NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8. ZA.Cache wraps `IMemoryCache`, so the relevant comparisons are: hand-rolled `GetOrCreateAsync` (the pattern ZA replaces) and [FusionCache](https://github.com/ZiggyCreatures/FusionCache) 2.9 (the de-facto third-party L1+L2 caching library). Each row returns the library's own task type directly, so no benchmark wrapper allocation is counted.

| Library | Time | Allocated |
|---|---:|---:|
| Raw `IMemoryCache.GetOrCreateAsync` | 157 ns | 104 B |
| **ZA.Cache proxy** | **198 ns** | **0 B** |
| FusionCache | 1,270 ns | 88 B |

**ZA.Cache is about 6× faster than FusionCache and the only row that allocates nothing.** The trade vs raw `IMemoryCache` is about 1.3× the time, the cost of the typed `[Cache]` abstraction: proxy dispatch, key formatting and telemetry. In exchange you don't write the cache-lookup boilerplate at every call site, and the key derivation is generated rather than hand-typed.

**FusionCache** is heavier because it carries L2-cache, stampede protection, and adaptive-caching infrastructure even when only L1 is configured. For pure L1, ZA is the lighter choice; FusionCache's value is the L2 + advanced features that ZA does not implement.

**Caveat on the raw row**: its 104 B is the `Task<string>` that `GetOrCreateAsync` returns plus the boxed `(string, int)` tuple the test uses as its key. ZA's hit returns a synchronously completed `ValueTask<string>` and looks the key up as a span, so it allocates nothing.
<!-- BENCH:END -->

## Self-benchmark

The [benchmarks/ZeroAlloc.Cache.Benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/tree/main/benchmarks/ZeroAlloc.Cache.Benchmarks) project also contains `CachedLookupBenchmark`, the direct-vs-proxied baseline. It compares:

- **Baseline**: a direct call on the underlying `ICustomerService` implementation, no caching.
- **Proxied (cache hit)**: the generated `ICustomerServiceCacheProxy` over a plain `MemoryCache`, pre-warmed so every measured call is a hit, for a `ValueTask<string>` and a `ValueTask<int>` method.
- **Proxied, other IMemoryCache**: the same proxy over an `IMemoryCache` decorator, which takes the string key lookup.

| Method | Time | Allocated |
|---|---:|---:|
| direct (no cache) | 0.8 ns | 0 B |
| proxied (cache hit) | 195 ns | 0 B |
| proxied value type (cache hit) | 203 ns | 0 B |
| proxied, other IMemoryCache (cache hit, string key) | 240 ns | 88 B |

.NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8. Every benchmark returns the proxy's `ValueTask` directly, because an async wrapper method would allocate a `Task<T>` of its own.

### Run the benchmark

```bash
dotnet run --project benchmarks/ZeroAlloc.Cache.Benchmarks -c Release -- --filter "*"
```

Results are written to `BenchmarkDotNet.Artifacts/results/`.

### What to watch

- **Allocated column**: the direct and both proxied cache-hit rows must read `0 B`. The other-IMemoryCache row allocates the key string by design.
- **The allocation gate**: `HitAllocationTests` in `tests/ZeroAlloc.Cache.Tests` runs with the test suite and fails the build when a hit on a plain `MemoryCache` allocates, for reference and value-type returns, span-formattable key parameters, the `MaxEntries` cache and a proxy resolved from DI.
- **Time column**: the proxied rows should stay within a small constant of each other. Most of the time is the `MemoryCache` lookup and the telemetry calls, not the key.

## Cache-miss path

The cache-miss path does allocate: it builds the key string, stores the entry in `IMemoryCache`, and the underlying service's return value flows through. This is intentional. Caching is only valuable on miss-then-hit, and the claim of zero allocation applies to the hit path, which is the overwhelmingly common case in a warmed cache.

## HybridCache integration

A method with `UseHybridCache = true` calls `HybridCache.GetOrCreateAsync` on every call, hit or miss. `HybridCache` takes only string keys, so the proxy builds the key string each time, and `HybridCache`'s own pipeline carries its own allocation profile. The zero-allocation claim does not apply to `HybridCache` methods.
