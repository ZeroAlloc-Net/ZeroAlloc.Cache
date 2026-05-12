# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.5](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v1.0.4...v1.0.5) (2026-05-12)


### Bug Fixes

* **readme:** absolute GitHub URLs so nuget.org links resolve ([#47](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/47)) ([e0b89d2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/e0b89d225455ec34698f604722999327072443cb))

## [1.0.4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v1.0.3...v1.0.4) (2026-05-10)


### Chores

* **deps:** update dependency meziantou.analyzer to 3.0.77 ([#44](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/44)) ([8929533](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/89295333b764a245fc0c5f6737d1d90513d9472e))
* **deps:** update microsoft.extensions to v10 ([#45](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/45)) ([227e1c8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/227e1c83aebb9f2fd2e8d21f81d69097fe5a323c))

## [1.0.3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v1.0.2...v1.0.3) (2026-05-08)


### Chores

* **deps:** update github actions ([#41](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/41)) ([4beb6f4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/4beb6f46c75f8d314c89445b0a1324b38a180e75))
* **deps:** update microsoft.codeanalysis to v5 ([#42](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/42)) ([96250c1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/96250c11e135675a1b8c96ac15f0a317a8d430e4))

## [1.0.2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v1.0.1...v1.0.2) (2026-05-07)


### CI

* add publish-from-manifest rescue workflow ([#39](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/39)) ([2dc7ef4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/2dc7ef43c6de47e031c4ba3cf71fcf7393535ae6))

## [1.0.1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v1.0.0...v1.0.1) (2026-05-06)


### CI

* **release-please:** use RELEASE_PLEASE_TOKEN to trigger downstream CI ([#38](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/38)) ([7e7d8d3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/7e7d8d303a934b4fe0e0cd2f19c378c53908d48b))


### Chores

* **deps:** update dependency xunit.runner.visualstudio to v3 ([#36](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/36)) ([0c8f777](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/0c8f777ca5b01016987e225f369f1d2d082159b8))
* **deps:** update microsoft.extensions ([#35](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/35)) ([c25848d](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/c25848d1bbf24087cdff1e5c6b0f29116dc66183))

## [1.0.0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/compare/v0.0.1...v1.0.0) (2026-05-04)


### ⚠ BREAKING CHANGES

* **cache:** Meter name renamed from "zeroalloc.cache" to "ZeroAlloc.Cache" for ecosystem consistency with the other ZeroAlloc telemetry packages. Subscribers calling AddMeter("zeroalloc.cache") must update to AddMeter("ZeroAlloc.Cache"); they will silently stop receiving metrics otherwise (no error or warning at runtime).

### Features

* add CacheAttribute with TtlMs, Sliding, MaxEntries, UseHybridCache ([32c97ed](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/32c97ed110ff8593e88b345715c3d398424a30a1))
* add generator scaffold (CacheGenerator, CacheModel, CacheWriter, CacheDiagnostics stubs) ([b54a70a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/b54a70a71652d7670718e9d35d618c4c5b782ce0))
* bundle source generator into ZeroAlloc.Cache package ([1496c04](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/1496c04e7d5c0cc84399baae0a1505ee59ff14c4))
* bundle source generator into ZeroAlloc.Cache package ([f353660](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/f353660754e5d00a21076e4dcfcc47e10c029950))
* **cache:** emit cache.lookup span + lookup_duration_ms + rename Meter to ZeroAlloc.Cache ([#12](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/12)) ([#18](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/18)) ([fa1356c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/fa1356c9c02c8d1482541f882adba18d3640d836))
* **generator:** emit HybridCache proxy with static options + state tuple factory ([3afc576](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/3afc576e7d7ab970dfc1467df2d77728e1951888))
* **generator:** emit IMemoryCache proxy + DI extension; lock first snapshot ([7cb5eae](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/7cb5eae33310c77a11b5231d54eba40adb0f4dd4))
* **generator:** implement TryParse — build CacheModel from [Cache] interface ([db9b1a5](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/db9b1a5bb497b69d9498cb1f41313ea2942e4724))
* **generator:** MaxEntries — isolated MemoryCache with SizeLimit ([ca6017c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/ca6017ccdec88e522bdae703da192bb5071f9048))
* **generator:** sliding expiration + ZC0001 diagnostic for HybridCache + sliding ([fa772d2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/fa772d21bb60962efff814b383cf289e0a95a5ed))
* lock public API surface (PublicApiAnalyzers + api-compat gate) ([#25](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/25)) ([5bace21](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/5bace21072856e7057eee001388e2f290369650c))


### Bug Fixes

* correct package versions, add Version/Description, suppress RS2008 in generator ([aa12efb](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/aa12efbc0fce86ba0e5a479e57339f6614eef76e))
* defensive TtlMs guard, IsolatedCacheMaxEntries comment, sliding+maxentries snapshot ([c267dc4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/c267dc4dd16e3a0d37257ebac5009177de14c9b6))
* explicit tuple labels and zero-param HybridCache snapshot (Task 8) ([7a07e28](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/7a07e28bf9c6977d99132e52d9bff39431060d2d))
* **generator:** add IsAsync to PassthroughMethodModel, ct check, error guard in RegisterSourceOutput ([8bc9dfa](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/8bc9dfad2bc52b9b278c12032b7cea33afe5be9a))
* **generator:** always treat non-generic-return methods as passthrough to prevent broken emit ([a640267](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a640267cbd55b7b2ae91749f4ed3b9582291def5))
* **generator:** annotate TImpl with [DynamicallyAccessedMembers] on Add{Service}Cache (closes [#7](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/7)) ([#8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/8)) ([7c49b9f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/7c49b9f6a41e8e78e70cc57820a02b159afbcdc1))
* **generator:** remove unused IsAsync field from PassthroughMethodModel ([57a7523](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/57a752347d6f9273515dfc9144c786ed46527ae6))
* **generator:** TtlMs=0 config, InterfaceFqn for generics, IsAsync detection, HLQ012 pragma scope, early-exit guard ([2159673](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/2159673a31c9672b17ceaf7a377c47d48e3465ed))
* ImmutableArray equality for incremental caching, ZC0003 for HybridCache on net8 ([05f1c58](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/05f1c58b29734255786f351112b3c3d230d20d3b))
* pack generator DLL under analyzers/dotnet/cs ([2a30867](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/2a30867c3873fe9d104b7f2ecf255f4d4bbc055a))
* pack generator DLL under analyzers/dotnet/cs ([15e9678](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/15e9678dba7235fe94438da51ccd258de4d67839))
* pure-passthrough DI extension no longer emits spurious HybridCache dependency ([92d346e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/92d346e48e2b0a6c780977d673f9a419d01e269f))
* **release-please:** grant write permissions and use plural output name ([#33](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/33)) ([10776f1](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/10776f11729f3f601dddbde057487a1d16bd2733))
* **release-please:** repair broken plumbing (config, manifest, workflow) ([#29](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/29)) ([56fd67f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/56fd67fb2df99e41a2927714b391eda530179857))
* remove dead NonCtArgumentList, ZC0004 for mixed MaxEntries, fix TrimStart bug, global-ns and pure-passthrough snapshots ([ba6d44a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/ba6d44ad5d306a8bb744d6c89e36455cdb69387a))
* remove LINQ from CacheWriter, combine param loops, use CT param name in lambda (Task 8) ([c47d489](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/c47d489e0be4b79c778bc417bc2d6bf84fa5da73))
* **tests:** revert Single() to First() — HLQ005 prohibits Single() ([18cd265](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/18cd26533c55ea6323191fa7c171739a4fba0e08))
* **tests:** use Single() instead of First() in AttributeUsage assertion ([9ba7c6f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/9ba7c6f13b2b6ec0a0dbd398c6950887af0e81e6))
* update snapshots for new using directives, improve passthrough test assertion ([9977742](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/997774279beca77d5d77c4e0d1e238922aa40062))


### Performance

* add BenchmarkDotNet project with cached-hit vs direct lookup ([#9](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/9)) ([d3efddf](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/d3efddfb1a4fc57271ad72b207e065372042bfdb))


### Documentation

* add GitHub Sponsors badge to README ([5c8168f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/5c8168f9124dc9ce99b1edb59fffd39f693bacf6))
* add GitHub Sponsors badge to README ([faab6cb](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/faab6cbc973bfeb7e7b3a38d0efef3096f770afa))
* add performance page covering cache-proxy design and benchmark ([#10](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/10)) ([0c5f210](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/0c5f210d6f73acb490ce65c66ccc1b81166faee8))
* fix transient vs singleton in getting-started ([0053080](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/0053080b5ca20d0ff0deb0518a5f4a73d3dfef2b))
* README, getting-started, attributes, ZC0001/ZC0002 diagnostics ([627269f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/627269fd7e21540a022082559d922e99c3898546))
* **readme:** standardize 5-badge set ([c038c76](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/c038c768995dddb4a09335e4b7e26892a6f02b4a))
* **readme:** standardize 5-badge set (NuGet/Build/License/AOT/Sponsors) ([a5ef920](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a5ef9209ef07cd7a6d554ea72615d1a0575d5415))


### Tests

* **generator:** lock passthrough and method-level override snapshots ([352f325](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/352f325ffcd923c1012e33c4ccd20ea7ba15b635))
* **generator:** snapshot for hybrid+isolated MaxEntries combination ([79e5ab4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/79e5ab4f135b88f37f78764f5241ad14f6076c92))
* **generator:** ZC0002 reference type key parameter diagnostic tests ([bf26757](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/bf2675742b03f8c546f02e2c19763c15e7a12b96))
* IMemoryCache proxy runtime behavior — miss, hit, key format, passthrough ([817f909](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/817f9098ae1116a0c74dd7749f27cd8717f9aa3c))


### CI

* add AOT publish smoke test (item 1 of [#5](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/5)) ([#6](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/6)) ([52da2af](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/52da2af71fe983ef1e15ddb223be8ccb77f4e0a2))
* add CI workflow ([8a39e9f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/8a39e9f852c23cb2c17db4e4e826bf2a0f595005))
* add release workflow ([2ef0397](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/2ef03971f73310f2971fe184e6470337e80be078))
* add release-please workflow ([4e0e0c7](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/4e0e0c7a9b06d3c47fd48d7964caffdaa052d6af))
* add website trigger workflow ([3cac515](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/3cac5154cee6693afa9fbc2e4653a727c7c28daa))
* bump release-please-action to [@v5](https://github.com/v5) ([#30](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/30)) ([a561575](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a5615753821c2d78d75194dbb859354b4b458e21))
* **release-please:** add 'deps' to changelog-sections ([a659c7c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a659c7c04e8b4f9a252ba0f27012038d7cc7ae68))
* **release-please:** add 'deps' to changelog-sections ([de7042e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/de7042e58f6ba1b8c9c71bf18e6ff91e9a096202))
* remove auto-publish to nuget on every main push ([#28](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/28)) ([1f2e051](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/1f2e051081d3ebbbe733aab6b7d8c59e9ddb0cd3))


### Chores

* add commitlint config ([b9c87a2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/b9c87a22671ed577f5bbc7251ec824c565e692bf))
* add GitVersion config ([e3dec62](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/e3dec62dd81f7f4d918cd7f1da9a90239eff4414))
* add gitversion tool manifest ([aaedc6e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/aaedc6ecf22b26363d9f152accd2dceb9a43e534))
* add missing NuGet package icon ([92f8f82](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/92f8f82e755c765e15400af4265c56802fc133cb))
* add MIT LICENSE ([96f7f66](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/96f7f661971d8e33b04e3535bf569e3d8175a5f8))
* add release-please config ([62ad93c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/62ad93c877d253f43001e12cd0796f60eb9a2b33))
* add release-please manifest seeded at 1.0.0 ([7c6ff6a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/7c6ff6a2b7168e387c8c800d6909612606fb69c7))
* Configure Renovate ([004def8](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/004def886aac1c42eacddc7a8a78bb4d73cd2ae8))
* **deps:** update dependency benchmarkdotnet to 0.15.8 ([#21](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/21)) ([8ffc0fa](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/8ffc0fa92b034e61507b97a74f94a6fcfd1a95f6))
* **deps:** update dependency dotnet-sdk to v10.0.203 ([94d7fb4](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/94d7fb451b83a4749115ce0ee453e32c724cc568))
* **deps:** update dependency dotnet-sdk to v10.0.203 ([9435d6a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/9435d6ae388cd5989f0fc0c32f16212ce3b13f56))
* **deps:** update dependency fluentassertions to 6.12.2 ([#2](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/2)) ([0c31e43](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/0c31e43a4e547e6dac7c5c24e499cd671d2a9fb2))
* **deps:** update dependency fluentassertions to v8 ([bc05d77](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/bc05d774a6076946e7b61a080e889e7a94458ca1))
* **deps:** update dependency fluentassertions to v8 ([81ce2db](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/81ce2db9117aa9da07b24c9864d9d01c73ca92f3))
* **deps:** update dependency gitversion.tool to v6.7.0 ([3c731ad](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/3c731add9c352d93027b6cdae111f99cd8c71c43))
* **deps:** update dependency gitversion.tool to v6.7.0 ([9e4744c](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/9e4744c226996f82a40d3f7e7e9ba27be68b03ea))
* **deps:** update dependency meziantou.analyzer to 3.0.50 ([#3](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/3)) ([95388f7](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/95388f7f3626bc6426d5392cb13f867d9902965c))
* **deps:** update dependency microsoft.net.test.sdk to 17.14.1 ([4432295](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/4432295952435e61830cc1c7a14c8fa1d8175411))
* **deps:** update dependency microsoft.net.test.sdk to 17.14.1 ([42b1d3f](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/42b1d3fc70e5384cfc9b20c067352c4c7c8ca1ca))
* **deps:** update dependency microsoft.net.test.sdk to v18 ([a430c7a](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a430c7aa9fb55f9d50b921b80e6e07b9d85d8e6e))
* **deps:** update dependency microsoft.net.test.sdk to v18 ([ef08f7e](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/ef08f7eafa06aa340454eac9f029560c85e146a6))
* **deps:** update dependency verify.xunit to 28.16.0 ([f2cbae5](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/f2cbae5955a1f241a02af66154848e343648c2dc))
* **deps:** update dependency verify.xunit to 28.16.0 ([480a807](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/480a807a1e26a210b2e3b7ab72e2e8f79244f455))
* **deps:** update dependency verify.xunit to v31 ([90a39f0](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/90a39f0aca99255940bcad43629d65e5c0287348))
* **deps:** update dependency verify.xunit to v31 ([c779189](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/c7791893e2565f313c635274777b8aec79592f47))
* **deps:** update dependency zeroalloc.analyzers to 1.3.15 ([#26](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/26)) ([261a3e9](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/261a3e96d0e6cbddd6b3e8b733182442d1744c6d))
* **deps:** update microsoft.codeanalysis ([#27](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/issues/27)) ([6a07403](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/6a07403e54c915a0f98fd37d3d0197c687221ea7))
* initial ZeroAlloc.Cache project scaffold ([a48b840](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/a48b8403082e32cacca9e5e74243703f21b9163f))
* pin .NET SDK to 10.0.202 ([53ba061](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/53ba06123a777b644393d6ba93be5d7fa190e0f9))
* update renovate config to org standard ([1ab8306](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache/commit/1ab8306cb23235ecdeed9a4eabd1c47406a0747d))

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
