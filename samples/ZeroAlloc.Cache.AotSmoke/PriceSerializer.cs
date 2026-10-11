using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace ZeroAlloc.Cache.AotSmoke;

// The AOT-safe serializer IPriceService.GetPriceAsync needs: HybridCache's default one throws
// "Reflection-based serialization has been disabled" under NativeAOT on the first miss. Its
// existence is also what satisfies ZC0011 for int.
public sealed class PriceSerializer : IHybridCacheSerializer<int>
{
    public int Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);
        return JsonSerializer.Deserialize(ref reader, SmokeJsonContext.Default.Int32);
    }

    public void Serialize(int value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);
        JsonSerializer.Serialize(writer, value, SmokeJsonContext.Default.Int32);
    }
}
