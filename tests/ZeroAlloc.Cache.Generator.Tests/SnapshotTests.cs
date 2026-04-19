using System.Threading.Tasks;

namespace ZeroAlloc.Cache.Generator.Tests;

public sealed class SnapshotTests
{
    [Fact]
    public Task InterfaceLevel_IMemoryCache_SingleMethod()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;
        return TestHelper.Verify(source);
    }

    [Fact]
    public Task InterfaceLevel_WithPassthrough_GeneratesProxy()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
                ValueTask SaveAsync(string data, CancellationToken ct);
            }
            """;
        return TestHelper.Verify(source);
    }

    [Fact]
    public Task MethodLevel_Override_ShadowsInterfaceLevel()
    {
        var source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetByIdAsync(string id, CancellationToken ct);
                [Cache(TtlMs = 5_000)]
                ValueTask<string> GetBySlugAsync(string slug, CancellationToken ct);
            }
            """;
        return TestHelper.Verify(source);
    }
}
