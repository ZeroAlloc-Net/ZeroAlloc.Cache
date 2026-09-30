using System.Collections.Immutable;
namespace ZeroAlloc.Cache.Generator;

internal sealed record CacheModel(
    string? Namespace,
    string InterfaceName,
    string InterfaceFqn,
    string HintName,            // unique within the compilation, see HintNames.ForHost
    string DisplayName,         // as diagnostics name the interface, e.g. "N.Outer.IFoo"
    LocationInfo? Location,     // the interface's name, where ZC0010 points
    ImmutableArray<string> ContainingDeclarations, // partial headers of the containing types, outermost first
    string NestedTypePrefix,    // "" at the top of a namespace, else e.g. "global::N.Outer." for the proxy and holder
    string KeyName,             // names the interface in cache keys and telemetry, e.g. "Outer.IFoo"
    string ExtensionMethodName, // e.g. "AddFooCache", or "AddOuter_FooCache" when nested
    bool IsPubliclyAccessible,  // false => emit the proxy and DI extension as internal
    bool AnyMethodUsesHybridCache,
    bool AnyMethodUsesIMemoryCache,
    bool AnyMethodUsesIsolatedCache,      // MaxEntries > 0 on any non-hybrid method
    int IsolatedCacheMaxEntries,          // SizeLimit for the isolated MemoryCache (first MaxEntries > 0)
    bool SpanKeyLookupAvailable,          // MemoryCache.TryGetValue(ReadOnlySpan<char>, out object?) is usable, #185
    ImmutableArray<CachedMethodModel> CachedMethods,
    ImmutableArray<PassthroughMethodModel> PassthroughMethods,
    ImmutableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// The method's hit path looks the key up as a span instead of a string. Only a method with
    /// key parameters needs it: without any, the key is a constant string and costs nothing.
    /// HybridCache takes string keys only, so hybrid methods never use it.
    /// </summary>
    public bool UsesSpanKeyLookup(CachedMethodModel m) =>
        SpanKeyLookupAvailable && !m.EffectiveConfig.UseHybridCache && !m.KeyParams.IsEmpty;

    /// <summary>
    /// A proxy is written for the interface: it has methods and no error. An interface that
    /// cannot be generated keeps only the diagnostic that says why.
    /// </summary>
    public bool IsGenerated
    {
        get
        {
            if (CachedMethods.Length == 0 && PassthroughMethods.Length == 0) return false;
            foreach (var d in Diagnostics)
            {
                if (d.Descriptor.DefaultSeverity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error) return false;
            }
            return true;
        }
    }

    /// <summary>A method on the shared IMemoryCache uses the span lookup, so the proxy needs the MemoryCache field.</summary>
    public bool NeedsSharedMemoryCacheField
    {
        get
        {
            foreach (var m in CachedMethods)
            {
                if (UsesSpanKeyLookup(m) && !m.UsesBoundedCache)
                    return true;
            }
            return false;
        }
    }

    // Override synthesized record equality for ImmutableArray fields
    public bool Equals(CacheModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Namespace, other.Namespace, System.StringComparison.Ordinal)
            && string.Equals(InterfaceName, other.InterfaceName, System.StringComparison.Ordinal)
            && string.Equals(InterfaceFqn, other.InterfaceFqn, System.StringComparison.Ordinal)
            && string.Equals(HintName, other.HintName, System.StringComparison.Ordinal)
            && string.Equals(DisplayName, other.DisplayName, System.StringComparison.Ordinal)
            && Equals(Location, other.Location)
            && ArraysEqual(ContainingDeclarations, other.ContainingDeclarations)
            && string.Equals(NestedTypePrefix, other.NestedTypePrefix, System.StringComparison.Ordinal)
            && string.Equals(KeyName, other.KeyName, System.StringComparison.Ordinal)
            && string.Equals(ExtensionMethodName, other.ExtensionMethodName, System.StringComparison.Ordinal)
            && IsPubliclyAccessible == other.IsPubliclyAccessible
            && AnyMethodUsesHybridCache == other.AnyMethodUsesHybridCache
            && AnyMethodUsesIMemoryCache == other.AnyMethodUsesIMemoryCache
            && AnyMethodUsesIsolatedCache == other.AnyMethodUsesIsolatedCache
            && IsolatedCacheMaxEntries == other.IsolatedCacheMaxEntries
            && SpanKeyLookupAvailable == other.SpanKeyLookupAvailable
            && ArraysEqual(CachedMethods, other.CachedMethods)
            && ArraysEqual(PassthroughMethods, other.PassthroughMethods)
            && ArraysEqual(Diagnostics, other.Diagnostics);
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
