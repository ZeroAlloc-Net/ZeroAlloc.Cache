using Microsoft.Extensions.Caching.Memory;

namespace ZeroAlloc.Cache.Tests;

/// <summary>An <see cref="IMemoryCache"/> decorator, the kind of cache the span key lookup must not bypass.</summary>
public sealed class DecoratingMemoryCache(MemoryCache inner) : IMemoryCache
{
    public int Lookups { get; private set; }

    public bool TryGetValue(object key, out object? value)
    {
        Lookups++;
        return inner.TryGetValue(key, out value);
    }

    public ICacheEntry CreateEntry(object key) => inner.CreateEntry(key);

    public void Remove(object key) => inner.Remove(key);

    public void Dispose() => inner.Dispose();
}
