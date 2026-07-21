using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Regression tests for #87: the generator emitted a proxy for every attributed interface in the
/// compilation, and the proxy for a cache-less/passthrough-only model was missing the
/// Microsoft.Extensions.DependencyInjection using (CS1061 on AddTransient/GetRequiredService).
/// </summary>
public sealed class OptInGateTests
{
    [Fact]
    public void UnrelatedAttribute_NoCacheAttribute_EmitsNothing()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            [AttributeUsage(AttributeTargets.Interface)]
            public sealed class SomethingElseAttribute : Attribute { }
            [SomethingElse]
            public interface IPostApi
            {
                ValueTask<string> GetAsync(string id, CancellationToken ct);
                ValueTask SaveAsync(string data, CancellationToken ct);
            }
            """;

        TestHelper.GetGeneratedFileNames(source).Should().BeEmpty();
    }

    [Fact]
    public void MethodLevelCacheAttribute_StillEmits()
    {
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public interface IMyService
            {
                [Cache(TtlMs = 30_000)]
                ValueTask<string> GetAsync(string id, CancellationToken ct);
            }
            """;

        TestHelper.GetGeneratedFileNames(source).Should().ContainSingle();
    }

    [Fact]
    public async Task PassthroughOnlyProxy_GeneratedDiExtensionCompiles()
    {
        // [Cache] on a non-generic return is passthrough — no cached methods, so the DI using
        // used to be skipped while the DI extension was still emitted.
        const string source = """
            using ZeroAlloc.Cache;
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public interface IMyService
            {
                [Cache(TtlMs = 30_000)]
                ValueTask SaveAsync(string data, CancellationToken ct);
            }
            """;

        var diags = await TestHelper.GetDiagnostics(source);
        diags.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }
}
