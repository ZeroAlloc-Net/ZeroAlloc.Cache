using Microsoft.Extensions.Caching.Memory;

namespace ZeroAlloc.Cache.Benchmarks;

/// <summary>
/// An <see cref="IMemoryCache"/> that is not exactly <see cref="MemoryCache"/>, such as a decorator.
/// The proxy cannot look it up by span, so it takes the string key lookup.
/// </summary>
public sealed class ForwardingMemoryCache(IMemoryCache inner) : IMemoryCache
{
    public bool TryGetValue(object key, out object? value) => inner.TryGetValue(key, out value);

    public ICacheEntry CreateEntry(object key) => inner.CreateEntry(key);

    public void Remove(object key) => inner.Remove(key);

    public void Dispose() => inner.Dispose();
}
