using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

/// <summary>Value equality for immutable arrays, which compare by reference.</summary>
internal static class EquatableArray
{
    public static bool SequenceEqual<T>(ImmutableArray<T> a, ImmutableArray<T> b)
        where T : System.IEquatable<T>
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
        }
        return true;
    }
}
