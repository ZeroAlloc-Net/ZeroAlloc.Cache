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
    ImmutableArray<CachedMethodModel> CachedMethods,
    ImmutableArray<PassthroughMethodModel> PassthroughMethods,
    ImmutableArray<Diagnostic> Diagnostics
);
