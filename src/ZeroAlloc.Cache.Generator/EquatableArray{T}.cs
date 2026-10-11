using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

/// <summary>An immutable array that compares by value, so a pipeline step stays cached.</summary>
internal sealed class EquatableArray<T> : System.IEquatable<EquatableArray<T>>
    where T : System.IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    public EquatableArray(ImmutableArray<T> items) => Items = items;

    public ImmutableArray<T> Items { get; }

    public bool Equals(EquatableArray<T>? other) => other is not null && EquatableArray.SequenceEqual(Items, other.Items);

    public override bool Equals(object? obj) => Equals(obj as EquatableArray<T>);

    public override int GetHashCode() => Items.Length;
}
