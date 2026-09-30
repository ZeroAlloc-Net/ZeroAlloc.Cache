using Microsoft.Extensions.Caching.Memory;

namespace ZeroAlloc.Cache.Tests;

/// <summary>
/// A <see cref="MemoryCache"/> subclass that re-implements <see cref="IMemoryCache"/>. It is a
/// MemoryCache, but its lookups go through its own code, so the proxy must not treat it as a plain
/// MemoryCache and look up by span behind its back.
/// </summary>
public sealed class ReimplementingMemoryCache() : MemoryCache(new MemoryCacheOptions()), IMemoryCache
{
    public int Lookups { get; private set; }

    bool IMemoryCache.TryGetValue(object key, out object? value)
    {
        Lookups++;
        return TryGetValue(key, out value);
    }
}
