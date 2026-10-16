# ZeroAlloc.Cache — Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | Install, annotate, register, and inject |
| [Attribute Reference](attributes.md) | All `[Cache]` properties and override rules |
| [Telemetry](telemetry.md) | Metrics, tracing spans and OpenTelemetry setup |
| [Performance](performance.md) | Benchmarks and every case where a hit still allocates |

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| [ZC0001](diagnostics/ZC0001.md) | Warning | `Sliding = true` combined with `UseHybridCache = true` — sliding TTL is silently ignored by the distributed (L2) tier |
| [ZC0002](diagnostics/ZC0002.md) | Warning | A cache key parameter is a reference type (excluding `string`) — `ToString()` may not produce a stable unique key |
| [ZC0003](diagnostics/ZC0003.md) | Error | `UseHybridCache = true` without a reference to `Microsoft.Extensions.Caching.Hybrid` — no proxy is generated for the interface |
| [ZC0004](diagnostics/ZC0004.md) | Warning | Bounded methods on one interface set different `MaxEntries` values — they share one size-limited cache sized by the first bounded method |
| [ZC0005](diagnostics/ZC0005.md) | Warning | `[Cache]` on a method that does not return `Task<T>` or `ValueTask<T>` — caching is not applied |
| [ZC0006](diagnostics/ZC0006.md) | Warning | A nested interface whose containing type is not `partial` — no proxy is generated for it |
| [ZC0007](diagnostics/ZC0007.md) | Warning | A generic interface, or one nested in a generic type — no proxy is generated for it |
| [ZC0008](diagnostics/ZC0008.md) | Warning | A private or protected nested interface, which the namespace-level `Add…Cache` method cannot name — no proxy is generated for it |
| [ZC0009](diagnostics/ZC0009.md) | Error | A file-local interface — no proxy is generated for it |
| [ZC0010](diagnostics/ZC0010.md) | Error | An interface whose name differs only in case from another's — only the first is generated |
| [ZC0011](diagnostics/ZC0011.md) | Warning | In a NativeAOT or trimmed project, a `HybridCache` method caches a type other than `string` or `byte[]` and no `IHybridCacheSerializer<T>` exists for it — `HybridCache`'s default serializer throws on the first miss |
