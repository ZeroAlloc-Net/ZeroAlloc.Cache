namespace ZeroAlloc.Cache.Generator;

internal sealed record CacheConfig(
    int TtlMs,
    bool Sliding,
    int MaxEntries,
    bool UseHybridCache
);
