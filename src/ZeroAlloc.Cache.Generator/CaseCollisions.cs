using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// The interfaces whose hint names differ only in case from an earlier interface's, which Roslyn
/// would reject as duplicates, and the ZC0010 errors about them. See #197.
/// </summary>
internal sealed record CaseCollisions(
    ImmutableArray<string> SkippedHintNames,
    ImmutableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>
    /// Groups the generated interfaces by hint name as Roslyn compares them, ignoring case. In each
    /// group the interface declared first, by file path and then position, keeps its file; every
    /// later one is skipped and reported. The order does not depend on the order of the syntax
    /// trees, so the same interface is generated on every run.
    /// </summary>
    public static CaseCollisions Find(ImmutableArray<CacheModel> models)
    {
        var generated = new List<CacheModel>();
        foreach (var model in models)
        {
            if (model.IsGenerated) generated.Add(model);
        }
        generated.Sort(CompareDeclarationOrder);

        var first = new Dictionary<string, CacheModel>(System.StringComparer.OrdinalIgnoreCase);
        var skipped = ImmutableArray.CreateBuilder<string>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        for (var i = 0; i < generated.Count; i++)
        {
            var model = generated[i];
            if (!first.TryGetValue(model.HintName, out var earlier))
            {
                first.Add(model.HintName, model);
                continue;
            }

            skipped.Add(model.HintName);
            diagnostics.Add(DiagnosticInfo.Create(
                CacheDiagnostics.NameDiffersOnlyInCase,
                model.Location,
                model.DisplayName,
                model.HintName,
                earlier.DisplayName));
        }

        return new CaseCollisions(skipped.ToImmutable(), diagnostics.ToImmutable());
    }

    private static int CompareDeclarationOrder(CacheModel x, CacheModel y)
    {
        var byPath = string.CompareOrdinal(x.Location?.Tree.FilePath, y.Location?.Tree.FilePath);
        if (byPath != 0) return byPath;
        var byPosition = (x.Location?.Span.Start ?? 0).CompareTo(y.Location?.Span.Start ?? 0);
        return byPosition != 0 ? byPosition : string.CompareOrdinal(x.HintName, y.HintName);
    }

    // ImmutableArray compares by reference; the pipeline needs value equality to keep this cached.
    public bool Equals(CaseCollisions? other) =>
        other is not null &&
        SequenceEqual(SkippedHintNames, other.SkippedHintNames) &&
        SequenceEqual(Diagnostics, other.Diagnostics);

    public override int GetHashCode() => (SkippedHintNames.Length * 397) ^ Diagnostics.Length;

    private static bool SequenceEqual<T>(ImmutableArray<T> a, ImmutableArray<T> b)
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
