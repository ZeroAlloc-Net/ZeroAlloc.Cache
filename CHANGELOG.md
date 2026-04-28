# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.0

### Breaking changes

- **`Meter` name renamed** from `"zeroalloc.cache"` to `"ZeroAlloc.Cache"` for
  ecosystem consistency with the other ZeroAlloc telemetry packages. Subscribers
  must update their `AddMeter(...)` calls:
  ```diff
  -services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("zeroalloc.cache"));
  +services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("ZeroAlloc.Cache"));
  ```
  No error or warning is raised at runtime; the only symptom is that metrics
  silently stop being delivered to the subscriber.

### Added

- `cache.lookup` span emitted per cached method via
  `ActivitySource("ZeroAlloc.Cache")`. Tags: `cache.method`
  (`"Interface.Method"` compile-time constant), `cache.tier` (`"L1"` or
  `"L2"`), and `cache.hit` (`true`/`false`, L1 path only — `HybridCache`
  hides per-call hit/miss state, so the tag is omitted on the L2 path).
- `cache.lookup_duration_ms` `Histogram<double>` on the existing Meter,
  tagged with `cache.method`. Recorded on every exit path (hit, miss, L2,
  exception).
- Existing counters (`cache.hits`, `cache.misses`, `cache.evictions`,
  `cache.hybrid_calls`) — emitted by the source generator with no extra
  package dependency, using only BCL `System.Diagnostics.Metrics`.

### Fixed

- Generated DI extension method for `[Cache(UseHybridCache = true)]` interfaces
  now emits `using Microsoft.Extensions.Caching.Hybrid;` so the
  `services.AddHybridCache()` extension method resolves in consumers that
  don't already pull that namespace transitively.
