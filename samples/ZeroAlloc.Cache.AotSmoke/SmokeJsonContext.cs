using System.Text.Json.Serialization;

namespace ZeroAlloc.Cache.AotSmoke;

// Source-generated metadata for the HybridCache values that are not string or byte[]. HybridCache
// serializes every value it stores, and its default serializer uses reflection, which NativeAOT
// disables. See ZC0011.
[JsonSerializable(typeof(int))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;
