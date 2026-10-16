# Getting Started

## 1. Install the NuGet package

```bash
dotnet add package ZeroAlloc.Cache
```

---

## 2. Annotate your interface

Apply `[Cache]` at the interface level to set a default policy for all methods, then override individual methods as needed.

```csharp
using ZeroAlloc.Cache;

[Cache(TtlMs = 60_000)]
public interface IProductRepository
{
    // inherits interface-level cache: 60 s, IMemoryCache
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);

    // method-level override: longer TTL, bounded cache, sliding window
    [Cache(TtlMs = 300_000, MaxEntries = 1_000, Sliding = true)]
    ValueTask<IReadOnlyList<Product>> SearchAsync(string query, CancellationToken ct);
}
```

---

## 3. Register with DI

The source generator emits an `Add{Service}Cache<TImpl>()` extension method on `IServiceCollection`, where `{Service}` is the interface name with any leading `I` removed — `IProductRepository` produces `AddProductRepositoryCache`. Call it once during startup.

```csharp
// Program.cs / Startup.cs
builder.Services.AddProductRepositoryCache<ProductRepositoryImpl>();
```

This registers:
- `ProductRepositoryImpl` as a transient inner implementation
- The generated `IProductRepositoryCacheProxy` as the `IProductRepository` transient

No manual factory wiring is required.

When two interfaces in one namespace would get the same method name, such as `IProductRepository` and `ProductRepository`, both keep their whole name and start with the namespace instead: `AddShop_IProductRepositoryCache` and `AddShop_ProductRepositoryCache` in namespace `Shop`. Every other interface keeps the shorter name.

### Nested interfaces

An interface nested in another type works too. Its proxy is generated inside the containing type, so every containing type must be `partial`, or the generator reports [ZC0006](diagnostics/ZC0006.md). The extension method sits at namespace level and is named after the containing types, joined with underscores: `Catalog.IProductLookup` produces `AddCatalog_ProductLookupCache`.

```csharp
public static partial class Catalog
{
    [Cache(TtlMs = 60_000)]
    public interface IProductLookup
    {
        ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);
    }
}

builder.Services.AddCatalog_ProductLookupCache<ProductLookupImpl>();
```

The extension method cannot name a type parameter or a private type, so a generic interface, an interface nested in a generic type, and a `private` or `protected` nested interface get no proxy; see [ZC0007](diagnostics/ZC0007.md) and [ZC0008](diagnostics/ZC0008.md). A `file` interface cannot be referred to from a generated file; see [ZC0009](diagnostics/ZC0009.md).

---

## 4. Inject and use

Inject `IProductRepository` as usual. Caching is entirely transparent.

```csharp
public class ProductsController(IProductRepository repo)
{
    public async Task<Product?> Get(int id, CancellationToken ct)
        => await repo.GetByIdAsync(id, ct);
}
```

On a cache miss the inner `ProductRepositoryImpl` is called and the result is stored. On a cache hit the value is returned directly, without calling the inner implementation. For a `ValueTask<T>` method on the default `MemoryCache` on .NET 9 and later, the hit allocates nothing; [Performance](performance.md#where-a-hit-still-allocates) lists the cases that still allocate.

### Cache keys

Each entry is stored under the string `{Interface}.{Method}:{arg1}:{arg2}`, built from every parameter except the `CancellationToken`, each formatted with the current culture. A method without key parameters uses `{Interface}.{Method}`. To evict an entry from the shared `IMemoryCache`, remove it by that string:

```csharp
cache.Remove($"IProductRepository.GetByIdAsync:{id}");
```

`HybridCache` methods use the same key text. For an interface nested in another type, `{Interface}` starts with the containing types, as in `Catalog.IProductLookup.GetByIdAsync:42`. When two interfaces in the project would use the same `{Interface}`, such as `Shipping.IProductLookup` and `Billing.IProductLookup`, both start with their namespace instead, as in `Shipping.IProductLookup.GetByIdAsync:42`, so they never read each other's entries. Every other interface keeps the shorter text.

---

## 5. HybridCache opt-in

To use `Microsoft.Extensions.Caching.Hybrid` (L1 in-process + L2 distributed), set `UseHybridCache = true` on the attribute and register `HybridCache` in your DI container.

```bash
dotnet add package Microsoft.Extensions.Caching.Hybrid
```

```csharp
// Add a distributed cache provider, e.g. Redis
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost");
builder.Services.AddHybridCache();

// Annotate with UseHybridCache
[Cache(TtlMs = 60_000, UseHybridCache = true)]
public interface IProductRepository
{
    ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct);
}
```

> **Note:** Do not combine `Sliding = true` with `UseHybridCache = true`. HybridCache's distributed (L2) tier uses absolute TTL only; sliding is silently ignored. The compiler will emit a [ZC0001](diagnostics/ZC0001.md) warning.

### HybridCache under NativeAOT

The `IMemoryCache` path needs nothing extra under NativeAOT. `HybridCache` does: it serializes every value it stores, and for any type but `string` and `byte[]` its default serializer uses reflection-based `System.Text.Json`, which throws `Reflection-based serialization has been disabled` on the first cache miss in a NativeAOT app. Register a serializer for each cached type, backed by source-generated metadata:

```csharp
[JsonSerializable(typeof(Product))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

public sealed class ProductSerializer : IHybridCacheSerializer<Product>
{
    public Product Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize(ref reader, AppJsonContext.Default.Product)!;
    }

    public void Serialize(Product value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, AppJsonContext.Default.Product);
    }
}

builder.Services.AddHybridCache().AddSerializer<Product, ProductSerializer>();
```

In a project with `PublishAot`, `IsAotCompatible` or `PublishTrimmed`, the generator reports [ZC0011](diagnostics/ZC0011.md) for each `HybridCache` method whose type has no serializer. That page also shows a generic serializer and a serializer factory for many types.

`Microsoft.Extensions.Caching.Hybrid` is not trim-clean itself yet, [dotnet/extensions#5624](https://github.com/dotnet/extensions/issues/5624): publishing with `HybridCache` reports the package's own trim warnings (IL2104, IL3053, IL2026) even with serializers registered.
