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

    /// <summary>
    /// [Cache] on a method the generator cannot cache used to be silently ignored: the method was
    /// classified passthrough and the inner implementation ran on every call, with nothing to tell
    /// the author caching was never applied. See #121.
    /// </summary>
    public static readonly DiagnosticDescriptor CacheAttributeIgnored = new(
        id: "ZC0005",
        title: "[Cache] ignored — return type cannot be cached",
        messageFormat: "Method '{0}' is marked [Cache] but returns '{1}'. Only Task<T> and ValueTask<T> results can be cached. Caching is not applied and the method is called every time. Return Task<T> or ValueTask<T> to enable caching.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MixedMaxEntriesValues = new(
        id: "ZC0004",
        title: "Mixed MaxEntries values",
        messageFormat: "Interface '{0}': bounded methods specify different MaxEntries values. The bounded methods of an interface share one size-limited cache; the first bounded method's MaxEntries ({1}) sets its SizeLimit.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A nested interface whose containing type is not <c>partial</c>. The proxy is generated
    /// inside the containing types, which only a partial type allows. See #194.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainingTypeNotPartial = new(
        id: "ZC0006",
        title: "Containing type of a [Cache] interface is not partial",
        messageFormat: "No cache proxy is generated for interface '{0}' because its containing type '{1}' is not partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A generic interface, or one nested in a generic type. Its Add...Cache extension method has
    /// to sit in a non-generic class at namespace level, and cannot name the type parameter. See #194.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericInterface = new(
        id: "ZC0007",
        title: "Generic [Cache] interface is not generated",
        messageFormat: "No cache proxy is generated for interface '{0}' because it is generic or nested in a generic type, which its Add...Cache extension method cannot name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A private or protected interface, or one nested in such a type. Its Add...Cache extension
    /// method sits at namespace level, where the interface is not accessible. See #194.
    /// </summary>
    public static readonly DiagnosticDescriptor InterfaceNotAccessible = new(
        id: "ZC0008",
        title: "[Cache] interface is not accessible from its namespace",
        messageFormat: "No cache proxy is generated for interface '{0}' because it is private or protected, or nested in such a type, which its Add...Cache extension method cannot name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A file-local interface, or one nested in a file-local type. A generated file cannot refer
    /// to it. See #194.
    /// </summary>
    public static readonly DiagnosticDescriptor FileLocalInterface = new(
        id: "ZC0009",
        title: "File-local [Cache] interface is not generated",
        messageFormat: "No cache proxy is generated for interface '{0}' because it is file-local or nested in a file-local type, and a generated file cannot refer to a file-local type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Roslyn compares hint names ignoring case, so an interface whose qualified name differs only
    /// in case from an earlier one's cannot get its own file. See #197.
    /// </summary>
    public static readonly DiagnosticDescriptor NameDiffersOnlyInCase = new(
        id: "ZC0010",
        title: "Interface name differs only in case from another [Cache] interface",
        messageFormat: "No cache proxy is generated for interface '{0}' because its file name '{1}' differs only in case from that of interface '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
