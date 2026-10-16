# Telemetry

Each cached method emits both metrics, via `Meter("ZeroAlloc.Cache")`, and a tracing span, via `ActivitySource("ZeroAlloc.Cache")`. No extra package is required: it is plain BCL `System.Diagnostics`. Without a listener attached, telemetry allocates nothing; see [Performance](performance.md#where-a-hit-still-allocates).

## Metrics

Counters tagged with `method`, the cached method name: `cache.hits`, `cache.misses`, `cache.evictions` and `cache.hybrid_calls`, the factory invocations on the `HybridCache` path. The `cache.lookup_duration_ms` histogram records per-lookup latency tagged with `cache.method`.

## Tracing

Each cached method emits a `cache.lookup` span tagged with:

| Tag | Value | Notes |
|-----|-------|-------|
| `cache.method` | `"Interface.Method"` | Compile-time constant per emitted method |
| `cache.tier` | `"L1"` or `"L2"` | `L1` = in-process `MemoryCache`; `L2` = `HybridCache` |
| `cache.hit` | `true` / `false` | L1 only. `HybridCache` hides per-call hit/miss state, so this tag is omitted on the L2 path |

## Subscribe with OpenTelemetry

```csharp
services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("ZeroAlloc.Cache"))
    .WithTracing(t => t.AddSource("ZeroAlloc.Cache"));
```
