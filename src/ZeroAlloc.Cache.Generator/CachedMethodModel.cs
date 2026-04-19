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
);
