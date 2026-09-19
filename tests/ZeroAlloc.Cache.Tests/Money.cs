namespace ZeroAlloc.Cache.Tests;

/// <summary>A custom struct return, to prove the fix is not special-cased to primitives.</summary>
public readonly record struct Money(decimal Amount);
