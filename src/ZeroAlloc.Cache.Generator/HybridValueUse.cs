namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// A UseHybridCache method whose value type HybridCache cannot serialize without reflection:
/// anything but <c>string</c> and <c>byte[]</c>. See ZC0011 and #223.
/// </summary>
/// <param name="TypeKey">The value type, fully qualified, as a serializer's type argument is compared.</param>
/// <param name="DisplayType">The value type as the message names it.</param>
internal sealed record HybridValueUse(string MethodName, string DisplayType, string TypeKey, LocationInfo? Location);
