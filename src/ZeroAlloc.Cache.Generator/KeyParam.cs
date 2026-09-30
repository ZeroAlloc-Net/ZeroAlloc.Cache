namespace ZeroAlloc.Cache.Generator;

/// <summary>A non-CancellationToken parameter used in cache key construction.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="IsReferenceType">A reference type other than a special type, for ZC0002.</param>
/// <param name="TypeFqn">The parameter type as emitted, for the span key lookup's local function.</param>
internal sealed record KeyParam(string Name, bool IsReferenceType, string TypeFqn);
