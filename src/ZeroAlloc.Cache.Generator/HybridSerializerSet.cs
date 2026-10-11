using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// The value types the compilation has an <c>IHybridCacheSerializer&lt;T&gt;</c> for, and whether it
/// has an <c>IHybridCacheSerializerFactory</c>, which is trusted to cover any type.
/// </summary>
internal sealed record HybridSerializerSet(ImmutableArray<string> TypeKeys, bool CoversAnyType)
{
    public static readonly HybridSerializerSet Empty = new(ImmutableArray<string>.Empty, false);

    public bool IsEmpty => TypeKeys.IsEmpty && !CoversAnyType;

    public static HybridSerializerSet Merge(IEnumerable<HybridSerializerSet> sets)
    {
        var keys = new SortedSet<string>(System.StringComparer.Ordinal);
        var any = false;
        foreach (var set in sets)
        {
            any |= set.CoversAnyType;
            foreach (var key in set.TypeKeys) keys.Add(key);
        }
        return keys.Count == 0 && !any ? Empty : new HybridSerializerSet(ImmutableArray.CreateRange(keys), any);
    }

    public bool Equals(HybridSerializerSet? other) =>
        other is not null && CoversAnyType == other.CoversAnyType && EquatableArray.SequenceEqual(TypeKeys, other.TypeKeys);

    public override int GetHashCode() => (TypeKeys.Length * 397) ^ (CoversAnyType ? 1 : 0);
}
