namespace ZeroAlloc.Cache;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CacheAttribute : Attribute
{
    /// <summary>Cache entry lifetime in milliseconds.</summary>
    public required int TtlMs { get; init; }

    /// <summary>
    /// When true, each access resets the expiration timer (IMemoryCache only).
    /// With UseHybridCache = true, the L2 tier uses absolute TTL — ZC0001 is emitted.
    /// </summary>
    public bool Sliding { get; init; } = false;

    /// <summary>
    /// Maximum number of entries. When > 0, the proxy uses an isolated MemoryCache
    /// with SizeLimit instead of the shared IMemoryCache.
    /// </summary>
    public int MaxEntries { get; init; } = 0;

    /// <summary>
    /// When true, the proxy uses HybridCache (L1 + optional L2) instead of IMemoryCache.
    /// Requires Microsoft.Extensions.Caching.Hybrid and .NET 9+ or the NuGet backport.
    /// </summary>
    public bool UseHybridCache { get; init; } = false;
}
