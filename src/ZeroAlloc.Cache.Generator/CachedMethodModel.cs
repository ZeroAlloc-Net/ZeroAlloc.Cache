using System.Collections.Immutable;

namespace ZeroAlloc.Cache.Generator;

internal sealed record CachedMethodModel(
    string Name,
    string ReturnTypeFqn,       // e.g., "System.Threading.Tasks.ValueTask<string>"
    string InnerReturnTypeFqn,  // e.g., "string" — the T in ValueTask<T>
    string ParameterList,       // e.g., "global::System.String id, global::System.Threading.CancellationToken ct"
    string ArgumentList,        // e.g., "id, ct" — all args
    string NonCtArgumentList,   // e.g., "id" — non-CancellationToken args only
    string KeyArguments,        // e.g., ":{id}" or ":{id}:{page}" — appended in key interpolation
    bool HasCancellationToken,
    string? CancellationTokenParamName,
    ImmutableArray<KeyParam> KeyParams,  // non-CT params for HybridCache state tuple
    CacheConfig EffectiveConfig          // method-level ?? interface-level
)
{
    // Override synthesized record equality for ImmutableArray<KeyParam>
    public bool Equals(CachedMethodModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, System.StringComparison.Ordinal)
            && string.Equals(ReturnTypeFqn, other.ReturnTypeFqn, System.StringComparison.Ordinal)
            && string.Equals(InnerReturnTypeFqn, other.InnerReturnTypeFqn, System.StringComparison.Ordinal)
            && string.Equals(ParameterList, other.ParameterList, System.StringComparison.Ordinal)
            && string.Equals(ArgumentList, other.ArgumentList, System.StringComparison.Ordinal)
            && string.Equals(NonCtArgumentList, other.NonCtArgumentList, System.StringComparison.Ordinal)
            && string.Equals(KeyArguments, other.KeyArguments, System.StringComparison.Ordinal)
            && HasCancellationToken == other.HasCancellationToken
            && string.Equals(CancellationTokenParamName, other.CancellationTokenParamName, System.StringComparison.Ordinal)
            && ArraysEqual(KeyParams, other.KeyParams)
            && EffectiveConfig == other.EffectiveConfig;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int h = Name is null ? 0 : System.StringComparer.Ordinal.GetHashCode(Name);
            h = h * 397 ^ (ReturnTypeFqn is null ? 0 : System.StringComparer.Ordinal.GetHashCode(ReturnTypeFqn));
            h = h * 397 ^ KeyParams.Length;
            h = h * 397 ^ (EffectiveConfig?.GetHashCode() ?? 0);
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
