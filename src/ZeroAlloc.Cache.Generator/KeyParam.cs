namespace ZeroAlloc.Cache.Generator;

/// <summary>A non-CancellationToken parameter used in cache key construction.</summary>
internal sealed record KeyParam(string Name, bool IsReferenceType);
