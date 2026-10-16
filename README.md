# ZeroAlloc.Cache

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Cache.svg)](https://www.nuget.org/packages/ZeroAlloc.Cache)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Put `[Cache]` on an interface and a source generator writes the caching proxy for you: no lookup boilerplate, no reflection, and a cache hit that allocates nothing.

## Why ZeroAlloc.Cache

- **0 B per cache hit.** A `ValueTask<T>` hit on the default `MemoryCache` on .NET 9 and later allocates nothing, and a test fails the build if it ever does. The few cases that still allocate are [listed](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md#where-a-hit-still-allocates).
- **About 6× faster than FusionCache** on an in-process cache hit, measured in the same run.
- **Works under NativeAOT and trimming.** The proxy is plain generated code, published and run as a NativeAOT app in CI.
- **Mistakes surface at compile time.** Unstable key parameters, a missing `HybridCache` package or a missing AOT serializer are compiler diagnostics, not production surprises.

## Install

```bash
dotnet add package ZeroAlloc.Cache
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Cache;

var services = new ServiceCollection();
services.AddProductCatalogCache<ProductCatalog>(); // generated: registers the caching proxy

var catalog = services.BuildServiceProvider().GetRequiredService<IProductCatalog>();
Console.WriteLine(await catalog.GetNameAsync(42, CancellationToken.None)); // miss: calls ProductCatalog
Console.WriteLine(await catalog.GetNameAsync(42, CancellationToken.None)); // hit: no call, no allocation

[Cache(TtlMs = 60_000)]
public interface IProductCatalog
{
    ValueTask<string> GetNameAsync(int id, CancellationToken ct);
}

public sealed class ProductCatalog : IProductCatalog
{
    public ValueTask<string> GetNameAsync(int id, CancellationToken ct)
    {
        Console.WriteLine($"Loading product {id}...");
        return ValueTask.FromResult($"Product {id}");
    }
}
```

`Loading product 42...` prints once. Callers keep injecting `IProductCatalog`; caching is invisible to them.

## What you get

- **Transparent caching** for any `Task<T>` or `ValueTask<T>` method, configured once on the interface and overridden per method.
- **Generated cache keys** from the method's parameters, with `CancellationToken` left out. The key text is predictable, so you can evict an entry by it.
- **Absolute or sliding expiry**, and `MaxEntries` to bound memory on high-cardinality methods.
- **`IMemoryCache` by default**, with `HybridCache` as an L1 + L2 distributed tier per method: `UseHybridCache = true`.
- **One-line DI registration** through a generated `Add{Service}Cache<TImpl>()` extension.
- **Built-in metrics and tracing** on the BCL `Meter` and `ActivitySource`, ready for OpenTelemetry: [Telemetry](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/telemetry.md).
- **Compile-time diagnostics** ZC0001–ZC0011: [the full list](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/index.md#diagnostics).

## Performance

In-process cache hit. .NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8.

| Library | Time | Allocated |
|---|---:|---:|
| Raw `IMemoryCache.GetOrCreateAsync` | 157 ns | 104 B |
| **ZA.Cache proxy** | **198 ns** | **0 B** |
| FusionCache | 1,270 ns | 88 B |

ZA.Cache is about 6× faster than FusionCache and the only row that allocates nothing. Against hand-written `IMemoryCache` code it costs about 1.3× the time, for proxy dispatch, key formatting and telemetry, in exchange for no lookup code at any call site. Methodology and the full numbers: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/performance.md).

## Learn more

- Documentation: [cache.zeroalloc.net](https://cache.zeroalloc.net)
- [Getting started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/getting-started.md), including `HybridCache` under NativeAOT, and the [attribute reference](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/docs/attributes.md)
- Related packages: [ZeroAlloc.Resilience](https://github.com/ZeroAlloc-Net/ZeroAlloc.Resilience) for retry and circuit-breaker proxies, [ZeroAlloc.Telemetry](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry) for tracing and metrics proxies
- Contributing: issues and pull requests are welcome on [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues)
- License: [MIT](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/blob/main/LICENSE)
