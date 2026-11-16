# AGENTS.md: ZeroAlloc.Cache

ZeroAlloc.Cache is a Roslyn source generator plus a small runtime library. `[Cache]` on an interface
makes the generator emit a caching proxy for `Task<T>` and `ValueTask<T>` methods, backed by
`IMemoryCache` or `HybridCache`. Two NuGet packages ship: `ZeroAlloc.Cache` (runtime, bundles the
generator) and `ZeroAlloc.Cache.Generator` (standalone generator, kept for compatibility).

## Layout

- `src/ZeroAlloc.Cache.Generator` targets `netstandard2.0` and is the only place generated code is
  written (`CacheWriter.cs`). It is packed under `analyzers/dotnet/cs`, not `lib/`.
- `src/ZeroAlloc.Cache` multi-targets `net8.0` to `net11.0`. The `net8.0` build has no
  `Microsoft.Extensions.Caching.Hybrid` reference, so HybridCache code paths do not exist there.
- `samples/ZeroAlloc.Cache.AotSmoke` is a NativeAOT app, not a demo. It is the trim and AOT gate.
- `tests/ZeroAlloc.Cache.Generator.Tests/Snapshots/*.verified.cs` are Verify snapshots of generated
  code. A generator output change fails these tests; review the `.received.cs` diff and accept it on
  purpose, never to make a test pass.
- `benchmarks/` compiles with the solution but only the Benchmarks workflow runs it.

## Build and test

SDK: `global.json` pins 11.0.100 (`rollForward: latestMinor`). Test projects target `net10.0` and
`net11.0`, so both the .NET 10 and the .NET 11 runtime must be installed; the `net8.0` and `net9.0`
library builds are compiled but never run by the tests. `dotnet tool restore` is needed first for
GitVersion.

CI job `build` (`.github/workflows/ci.yml`):

```bash
dotnet tool restore
dotnet restore
SEMVER=$(dotnet gitversion /output json /showvariable SemVer)
dotnet build -c Release -p:Version=$SEMVER --no-restore
dotnet test -c Release --no-build --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
dotnet pack src/ZeroAlloc.Cache/ZeroAlloc.Cache.csproj -c Release -p:Version=$SEMVER --no-build -o ./artifacts
dotnet pack src/ZeroAlloc.Cache.Generator/ZeroAlloc.Cache.Generator.csproj -c Release -p:Version=$SEMVER --no-build -o ./artifacts
```

The same job runs a coverage gate (`MarcelRoozekrans/ci-baseline/actions/coverage-gate`) on
`TestResults/**/coverage.cobertura.xml` after the test step: line coverage of production code must stay at
or above the floor. The commands above collect coverage but do not check the floor locally.

CI job `aot-smoke` (needs `clang` and `zlib1g-dev`):

```bash
dotnet restore samples/ZeroAlloc.Cache.AotSmoke/ZeroAlloc.Cache.AotSmoke.csproj
dotnet publish samples/ZeroAlloc.Cache.AotSmoke/ZeroAlloc.Cache.AotSmoke.csproj -r linux-x64 -c Release -o ./aot-out
timeout -k 5 30 ./aot-out/ZeroAlloc.Cache.AotSmoke
```

CI publishes with `2>&1 | tee publish.log` and then fails on any `IL` warning from outside
`Microsoft.Extensions.Caching.Hybrid`. Run the filter on `publish.log` to reproduce it:

```bash
grep -E 'warning IL[0-9]{4}' publish.log \
  | grep -vE 'Microsoft\.Extensions\.Caching\.Hybrid\.dll : warning IL(2104|3053):' \
  | grep -vE 'warning IL2026: Microsoft\.Extensions\.Caching\.Hybrid\.'
```

Any output is a failure.

Fast inner loop (verified, runs in about a minute, not a CI job on its own):

```bash
dotnet test tests/ZeroAlloc.Cache.Generator.Tests -c Release -f net10.0
```

## Conventions and gotchas

- Warnings are errors, and `RS0016` and `RS0017` too. A public API change needs a matching entry in
  `src/ZeroAlloc.Cache/PublicAPI.Unshipped.txt`. A new or changed `ZC` diagnostic needs
  `src/ZeroAlloc.Cache.Generator/AnalyzerReleases.Unshipped.md` and a page in `docs/diagnostics/`.
  Never edit the `*.Shipped.*` files; the release PR moves entries there.
- Packing needs `-p:Version` on the build step as well as the pack step (`ZAVERSION001`,
  `ZAVERSION002` in `Directory.Build.props`). `--no-build` with an unversioned build fails.
- A cache hit on `ValueTask<T>` with the default `MemoryCache` must allocate 0 B on .NET 9 and later.
  `HitAllocationTests` enforces it, and the generator emits a span-key lookup for it that the `net8.0`
  `Microsoft.Extensions.Caching.Memory` lacks, so `net8.0` keeps the string-key lookup. Do not add
  boxing, closures or string building to the hit path of generated code.
- `HitAllocationTests` and the telemetry tests share the xunit collection
  `cache-telemetry-non-parallel`, because meter and activity listeners make every call allocate. Keep
  a new allocation or telemetry test in that collection.
- The generator project must stay `netstandard2.0` with `EnforceExtendedAnalyzerRules`. Its models
  must be value-equatable (`EquatableArray`) so the incremental pipeline caches; do not put `ISymbol`
  or `Compilation` into a model.
- The AotSmoke `WarningsNotAsErrors` for `IL2104`, `IL3053` and `IL2026` is a known upstream
  workaround (`Microsoft.Extensions.Caching.Hybrid`, issue #224). Do not widen it to other assemblies.
- Coverage is gated in CI (`coverage-gate` action) and `codecov.yml` keeps a project drop check.

<!-- org-core:start sha256=c42b83cb821d67a5547aafe02bbf9d3f36a7d19379a5d378bc38d452b7db8451 (generated from docker-nuc scripts/agents-md/org-core-zeroalloc.md; do not edit here) -->
## Org rules

These rules hold in every ZeroAlloc-Net repo. Each library makes two promises: it works under
NativeAOT and trimming, and its hot path allocates nothing. The 2026-10-11 sweep found 16 of 28 repos
allocating on the hot path, most with docs claiming 0 B.

### Hot path

The operation a consumer runs repeatedly: `Send`, a cache hit, `Validate`, `Map`, `Serialize`, a
resilience call, a state transition, an append, a worker poll, including the idle poll. Startup and
registration are not hot path. For a source generator, the hot path is the code it emits: judge the
snapshots and the generated output.

### Allocation traps the sweep found

| Pattern | Instead |
|---|---|
| `async ValueTask` that only awaits and returns | `return new ValueTask<T>(inner.XAsync(ct));` |
| `Task.WhenAll` over tasks that already completed | check `IsCompletedSuccessfully`, await only what is pending |
| `TagList` or log `params object[]` built before `Enabled`/`IsEnabled` | check first, or `LoggerMessage.Define` |
| event args allocated before checking for subscribers | copy the handler to a local, check, then allocate |
| `new Serializer()` / `new Utf8JsonWriter()` per call | cache stateless instances, reuse writers with `Reset` |
| `GetString()` / `ToString()` round-trips in JSON converters | parse and format through spans |
| `ToArray()` / `ToList()` copies between layers | pass `ReadOnlyMemory<T>` or spans, copy once at most |
| LINQ in emitted code | emit `for` loops |
| `yield` iterator behind a public `GetEnumerator` | public struct enumerator, from the first release |
| `Enum.IsDefined(typeof(E), v)`, `decimal.GetBits(v)` | `Enum.IsDefined<E>`, `v.Scale` |
| configuration parsed or read per call | parse once and cache |
| an `AsyncLocal` write per call | pass state explicitly |
| a capturing lambda or method-group delegate per call | cache the delegate in a field, or a static lambda |
| a per-key object that is never removed | remove on release; otherwise it is a leak |

Inherent allocations are fine when documented: the consumer's own result, an exception on a failure
path, a one-time cache fill.

### Tests and benchmarks

- Every hot path has an allocation gate test (`AllocationGate` from ZeroAlloc.TestHelpers) that measures
  the whole public call, including any state setup.
- Budgets are exact. If a path allocates, assert that number and comment what it is.
- Test the generated code, not a hand-written stand-in, and cover every shape: each attribute, nested
  collections, and the asynchronous case with a fake that returns pending tasks.
- Benchmarks run in CI through the shared `benchmark-smoke.yml` and target the library's LTS
  frameworks. The benchmark body allocates nothing besides the library itself. Run locally with
  `--inProcess`.
- Publish exact allocations and same-run ratios against a `Baseline`, never absolute timings. Run with
  `--launchCount 3`, pin CPUs with `taskset`, give the spread, and label every table.

### NativeAOT and trimming

- Packable runtime projects, and projects consumers publish, set `IsAotCompatible=true` on modern
  target frameworks. Analyzers and source generators target `netstandard2.0`.
- No reflection or runtime code generation on a supported path. An opt-in reflection fallback carries
  honest `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. Every suppression must be true;
  remove dead ones.
- The AOT smoke sample publishes with `TreatWarningsAsErrors=true`. If a third-party warning really
  blocks it, exclude only that code with `WarningsNotAsErrors` and name the assembly.
- The smoke runs the hot path of every package in the repo. A package without a smoke has unknown AOT
  status. Adapters run against real services in CI service containers; publish-only is a last resort.
  App and template smokes make authenticated requests.

### Docs and shipping

- Docs match measurements. After a hot-path change, re-measure and update `docs/performance.md` and the
  README. Never write "0 B" without a test that asserts it.
- New public API is `feat:`, even for a performance change. Public API changes are additive: never add
  an optional parameter to a shipped signature. A breaking end state goes in a `next-major` issue.
- A behaviour change gets a `## Behaviour change` section in the PR body.
- Conventional Commits with release-please. Subjects start lowercase, body lines stay within 100
  characters, and a body never contains nested parentheses: release-please drops the whole commit.
- Local packs use `9.9.9-local.<sha>`. After a release, verify the package on NuGet.
- Fix review findings in the PR; file an issue only for what cannot go in it.

Full rules, template wiring lessons and the reasons behind each rule: https://github.com/ZeroAlloc-Net/.github/blob/main/AGENTS.md
<!-- org-core:end -->
