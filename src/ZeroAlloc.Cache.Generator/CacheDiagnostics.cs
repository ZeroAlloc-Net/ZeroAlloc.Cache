using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator;

internal static class CacheDiagnostics
{
    private const string Category = "ZeroAlloc.Cache";

    public static readonly DiagnosticDescriptor SlidingNotSupportedOnHybridCache = new(
        id: "ZC0001",
        title: "Sliding expiration not supported with HybridCache",
        messageFormat: "Method '{0}': Sliding = true with UseHybridCache = true — HybridCache L2 does not support sliding expiration; absolute TTL will be used for the distributed tier",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ReferenceTypeKeyParameter = new(
        id: "ZC0002",
        title: "Cache key parameter is a reference type",
        messageFormat: "Parameter '{0}' of method '{1}' is a reference type included in the cache key — ensure ToString() returns a stable, unique value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor HybridCacheNotAvailable = new(
        id: "ZC0003",
        title: "HybridCache not available",
        messageFormat: "UseHybridCache = true requires Microsoft.Extensions.Caching.Hybrid (net9.0+). Add a package reference or target net9.0 or later.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MixedMaxEntriesValues = new(
        id: "ZC0004",
        title: "Mixed MaxEntries values",
        messageFormat: "Interface '{0}': multiple methods specify different MaxEntries values. All methods share a single isolated MemoryCache; the first MaxEntries value ({1}) is used as SizeLimit.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
