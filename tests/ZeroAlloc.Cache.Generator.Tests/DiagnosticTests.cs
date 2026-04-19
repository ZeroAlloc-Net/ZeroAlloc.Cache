using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

public sealed class DiagnosticTests
{
    [Fact]
    public async Task ZC0001_SlidingTrue_HybridCache_EmitsWarning()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, Sliding = true, UseHybridCache = true)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0001 = diags.Where(d => string.Equals(d.Id, "ZC0001", System.StringComparison.Ordinal)).ToList();
        zc0001.Should().HaveCount(1);
        zc0001[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        zc0001[0].GetMessage().Should().Contain("GetAsync");
    }

    [Fact]
    public async Task ZC0001_NotEmitted_WhenIMemoryCache()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000, Sliding = true)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0001", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZC0002_ReferenceTypeParam_EmitsWarning()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { } // reference type
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(ProductFilter filter, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        var zc0002 = diags.Where(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal)).ToList();
        zc0002.Should().HaveCount(1);
        var d0 = zc0002.First();
        d0.Severity.Should().Be(DiagnosticSeverity.Warning);
        d0.GetMessage().Should().Contain("filter").And.Contain("GetAsync");
    }

    [Fact]
    public async Task ZC0002_NotEmitted_ForString()
    {
        const string source = """
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

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZC0002_NotEmitted_ForValueTypes()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(int id, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Should().NotContain(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal));
    }
}
