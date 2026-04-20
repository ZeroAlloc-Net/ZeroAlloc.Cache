using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator;

internal sealed record CacheModel(
    string? Namespace,
    string InterfaceName,
    string InterfaceFqn,
    bool AnyMethodUsesHybridCache,
    bool AnyMethodUsesIMemoryCache,
    bool AnyMethodUsesIsolatedCache,      // MaxEntries > 0 on any method
    int IsolatedCacheMaxEntries,          // SizeLimit for the isolated MemoryCache (first MaxEntries > 0)
    ImmutableArray<CachedMethodModel> CachedMethods,
    ImmutableArray<PassthroughMethodModel> PassthroughMethods,
    ImmutableArray<Diagnostic> Diagnostics
)
{
    // Override synthesized record equality for ImmutableArray fields
    public bool Equals(CacheModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Namespace, other.Namespace, System.StringComparison.Ordinal)
            && string.Equals(InterfaceName, other.InterfaceName, System.StringComparison.Ordinal)
            && string.Equals(InterfaceFqn, other.InterfaceFqn, System.StringComparison.Ordinal)
            && AnyMethodUsesHybridCache == other.AnyMethodUsesHybridCache
            && AnyMethodUsesIMemoryCache == other.AnyMethodUsesIMemoryCache
            && AnyMethodUsesIsolatedCache == other.AnyMethodUsesIsolatedCache
            && IsolatedCacheMaxEntries == other.IsolatedCacheMaxEntries
            && ArraysEqual(CachedMethods, other.CachedMethods)
            && ArraysEqual(PassthroughMethods, other.PassthroughMethods)
            && Diagnostics.Length == other.Diagnostics.Length; // Diagnostic doesn't implement IEquatable; compare by count only
    }

    public override int GetHashCode()
    {
        // Use a simple hash that includes the interface identity;
        // full structural hash of methods is expensive and rarely needed
        unchecked
        {
            int h = Namespace is null ? 0 : System.StringComparer.Ordinal.GetHashCode(Namespace);
            h = h * 397 ^ (InterfaceName is null ? 0 : System.StringComparer.Ordinal.GetHashCode(InterfaceName));
            h = h * 397 ^ CachedMethods.Length;
            h = h * 397 ^ PassthroughMethods.Length;
            return h;
        }
    }

    private static bool ArraysEqual<T>(ImmutableArray<T> a, ImmutableArray<T> b)
        where T : System.IEquatable<T>
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }
}
