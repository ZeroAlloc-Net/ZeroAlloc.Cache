using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// The interfaces whose names must be qualified, because another interface would otherwise
/// share them: the cache keys, which every interface writes to the same caches, or the
/// <c>Add...Cache</c> method, which every interface of a namespace adds to the same class.
/// Every other interface keeps its names. See #199.
/// </summary>
internal sealed record NameCollisions(
    ImmutableArray<string> QualifiedKeyHintNames,
    ImmutableArray<string> QualifiedMethodHintNames)
{
    /// <summary>
    /// Finds the interfaces to qualify among those that are generated. Both kinds of name are
    /// qualified only for the interfaces that share it. A qualified name can equal another
    /// interface's own name, <c>M1.IX</c> is also the key of an <c>IX</c> nested in a type
    /// <c>N.M1</c>, so the search repeats until no name is shared by two interfaces, or only by
    /// interfaces that are all qualified already.
    /// </summary>
    public static NameCollisions Find(ImmutableArray<CacheModel> models, ImmutableArray<string> skippedHintNames)
    {
        var skipped = new HashSet<string>(skippedHintNames, System.StringComparer.Ordinal);
        var generated = new List<CacheModel>();
        foreach (var model in models)
        {
            if (model.IsGenerated && !skipped.Contains(model.HintName)) generated.Add(model);
        }

        var qualifiedKeys = new HashSet<string>(System.StringComparer.Ordinal);
        var qualifiedMethods = new HashSet<string>(System.StringComparer.Ordinal);
        bool changed;
        do
        {
            // Keys: one scope for the whole compilation, since every interface can write to the
            // same IMemoryCache or HybridCache.
            changed = QualifyShared(generated, qualifiedKeys, m =>
                qualifiedKeys.Contains(m.HintName) ? m.QualifiedKeyName : m.KeyName);

            // Methods: one CacheServiceCollectionExtensions class per namespace.
            changed |= QualifyShared(generated, qualifiedMethods, m =>
                (m.Namespace ?? string.Empty) + " " +
                (qualifiedMethods.Contains(m.HintName) ? m.QualifiedExtensionMethodName : m.ExtensionMethodName));
        }
        while (changed);

        return new NameCollisions(Sorted(qualifiedKeys), Sorted(qualifiedMethods));
    }

    /// <summary>
    /// The model with the qualified names this result gives it; the model itself when it keeps
    /// its names.
    /// </summary>
    public CacheModel Apply(CacheModel model)
    {
        var qualifyKey = QualifiedKeyHintNames.Contains(model.HintName, System.StringComparer.Ordinal);
        var qualifyMethod = QualifiedMethodHintNames.Contains(model.HintName, System.StringComparer.Ordinal);
        if (!qualifyKey && !qualifyMethod) return model;

        return model with
        {
            KeyName = qualifyKey ? model.QualifiedKeyName : model.KeyName,
            ExtensionMethodName = qualifyMethod ? model.QualifiedExtensionMethodName : model.ExtensionMethodName,
        };
    }

    // Marks every interface that shares its name with another, and says whether any was new.
    private static bool QualifyShared(
        List<CacheModel> models, HashSet<string> qualified, System.Func<CacheModel, string> name)
    {
        var byName = new Dictionary<string, List<CacheModel>>(System.StringComparer.Ordinal);
        for (var i = 0; i < models.Count; i++)
        {
            var key = name(models[i]);
            if (!byName.TryGetValue(key, out var group)) byName.Add(key, group = new List<CacheModel>());
            group.Add(models[i]);
        }

        var added = false;
        foreach (var group in byName.Values)
        {
            if (group.Count < 2) continue;
            for (var i = 0; i < group.Count; i++)
                added |= qualified.Add(group[i].HintName);
        }
        return added;
    }

    private static ImmutableArray<string> Sorted(HashSet<string> names)
    {
        var list = new List<string>(names);
        list.Sort(System.StringComparer.Ordinal);
        return list.ToImmutableArray();
    }

    // ImmutableArray compares by reference; the pipeline needs value equality to keep this cached.
    public bool Equals(NameCollisions? other) =>
        other is not null &&
        SequenceEqual(QualifiedKeyHintNames, other.QualifiedKeyHintNames) &&
        SequenceEqual(QualifiedMethodHintNames, other.QualifiedMethodHintNames);

    public override int GetHashCode() => (QualifiedKeyHintNames.Length * 397) ^ QualifiedMethodHintNames.Length;

    private static bool SequenceEqual(ImmutableArray<string> a, ImmutableArray<string> b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i], b[i], System.StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
