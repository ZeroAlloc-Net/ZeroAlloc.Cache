using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// ZC0011: under NativeAOT or trimming, HybridCache's default serializer throws for any value type
/// other than string and byte[], so a UseHybridCache method needs an IHybridCacheSerializer for
/// its value type. See #223.
/// </summary>
public sealed class HybridAotSerializerTests
{
    private const string Id = "ZC0011";

    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    private static readonly Dictionary<string, string> PublishAot = new(StringComparer.Ordinal)
    {
        ["build_property.PublishAot"] = "true",
    };

    private const string IntPriceService = """
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Cache;
        namespace T;
        public interface IPriceService
        {
            [Cache(TtlMs = 60_000, UseHybridCache = true)]
            ValueTask<int> GetPriceAsync(int id, CancellationToken ct);
        }
        """;

    [Theory]
    [InlineData("build_property.PublishAot")]
    [InlineData("build_property.IsAotCompatible")]
    [InlineData("build_property.PublishTrimmed")]
    public void Reported_ForAValueTypeOtherThanStringOrBytes_WhenAotOrTrimmed(string property)
    {
        var diagnostics = Run(new Dictionary<string, string>(StringComparer.Ordinal) { [property] = "True" }, IntPriceService);

        var zc0011 = diagnostics.Should().ContainSingle(d => string.Equals(d.Id, Id, StringComparison.Ordinal)).Subject;
        zc0011.Severity.Should().Be(DiagnosticSeverity.Warning);
        zc0011.GetMessage().Should().Contain("GetPriceAsync").And.Contain("IHybridCacheSerializer<int>");
        zc0011.Location.IsInSource.Should().BeTrue();
    }

    [Theory]
    [InlineData("string")]
    [InlineData("string?")]
    [InlineData("byte[]")]
    [InlineData("byte[]?")]
    public void NotReported_ForStringOrBytes(string valueType)
    {
        var source = $$"""
            #nullable enable
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace T;
            public interface ILabelService
            {
                [Cache(TtlMs = 60_000, UseHybridCache = true)]
                ValueTask<{{valueType}}> GetLabelAsync(int id);
            }
            """;

        Run(PublishAot, source).Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("")]
    public void NotReported_WhenNotAotOrTrimmed(string? publishAot)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (publishAot is not null) properties["build_property.PublishAot"] = publishAot;

        Run(properties, IntPriceService).Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void NotReported_ForAMemoryCacheMethod()
    {
        const string source = """
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace T;
            public interface IPriceService
            {
                [Cache(TtlMs = 60_000)]
                ValueTask<int> GetPriceAsync(int id);
            }
            """;

        Run(PublishAot, source).Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void NotReported_WhenASerializerForTheValueTypeExists()
    {
        Run(PublishAot, IntPriceService, Serializer("int")).Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Reported_WhenTheSerializerIsForAnotherType()
    {
        Run(PublishAot, IntPriceService, Serializer("long")).Should().ContainSingle(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void AnOpenGenericSerializer_CountsOnlyForTheTypesItIsClosedOver()
    {
        const string generic = """
            #nullable enable
            using System.Buffers;
            using Microsoft.Extensions.Caching.Hybrid;
            namespace T;
            public class JsonSerializer<TValue> : IHybridCacheSerializer<TValue>
            {
                public TValue Deserialize(ReadOnlySequence<byte> source) => default!;
                public void Serialize(TValue value, IBufferWriter<byte> target) { }
            }
            """;

        Run(PublishAot, IntPriceService, generic).Should().ContainSingle(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
        Run(PublishAot, IntPriceService, generic, "namespace T; public sealed class PriceSerializer : JsonSerializer<int> { }")
            .Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void NotReported_WhenASerializerFactoryExists()
    {
        const string factory = """
            using System.Diagnostics.CodeAnalysis;
            using Microsoft.Extensions.Caching.Hybrid;
            namespace T;
            public sealed class Factory : IHybridCacheSerializerFactory
            {
                public bool TryCreateSerializer<TValue>([NotNullWhen(true)] out IHybridCacheSerializer<TValue>? serializer)
                {
                    serializer = null;
                    return false;
                }
            }
            """;

        Run(PublishAot, IntPriceService, "#nullable enable\n" + factory).Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void NotReported_WhenAPublicSerializerExistsInAReferencedAssembly()
    {
        var library = CSharpCompilation.Create(
            "Serializers",
            new[] { CSharpSyntaxTree.ParseText(Serializer("int"), ParseOptions) },
            TestHelper.BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        library.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        Run(PublishAot, new[] { IntPriceService }, library.ToMetadataReference())
            .Should().NotContain(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Reported_ForANullableRecord_UntilItsSerializerExists()
    {
        const string source = """
            #nullable enable
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace T;
            public sealed record Product(int Id, string Name);
            [Cache(TtlMs = 60_000, UseHybridCache = true)]
            public interface IProductService
            {
                ValueTask<Product?> GetAsync(int id);
                Task<int?> CountAsync();
            }
            """;

        var without = Run(PublishAot, source).Where(d => string.Equals(d.Id, Id, StringComparison.Ordinal)).ToList();
        without.Should().HaveCount(2);
        without.Select(d => d.GetMessage()).Should().Contain(m => m.Contains("IHybridCacheSerializer<T.Product>", StringComparison.Ordinal))
            .And.Contain(m => m.Contains("IHybridCacheSerializer<int?>", StringComparison.Ordinal));

        // The reference type's annotation is not part of the serializer's type; Nullable<int> is.
        var with = Run(PublishAot, source, Serializer("Product"), Serializer("int?", "NullableIntSerializer"))
            .Where(d => string.Equals(d.Id, Id, StringComparison.Ordinal));
        with.Should().BeEmpty();
    }

    [Fact]
    public void FromTheInterfaceAttribute_PointsAtTheMethod()
    {
        const string source = """
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace T;
            [Cache(TtlMs = 60_000, UseHybridCache = true)]
            public interface IPriceService
            {
                ValueTask<int> GetPriceAsync(int id);
            }
            """;

        var zc0011 = Run(PublishAot, source).Should().ContainSingle(d => string.Equals(d.Id, Id, StringComparison.Ordinal)).Subject;
        var tree = zc0011.Location.SourceTree!;
        tree.GetText().ToString(zc0011.Location.SourceSpan).Should().Be("GetPriceAsync");
    }

    private static string Serializer(string valueType, string name = "Serializer")
    {
        var type = valueType.TrimEnd('?');
        return $$"""
            #nullable enable
            using System.Buffers;
            using Microsoft.Extensions.Caching.Hybrid;
            namespace T;
            public sealed class {{name}}{{type.Replace("[]", "Array", StringComparison.Ordinal)}} : IHybridCacheSerializer<{{valueType}}>
            {
                public {{valueType}} Deserialize(ReadOnlySequence<byte> source) => default!;
                public void Serialize({{valueType}} value, IBufferWriter<byte> target) { }
            }
            """;
    }

    private static IReadOnlyList<Diagnostic> Run(Dictionary<string, string> properties, params string[] sources) =>
        Run(properties, sources, Array.Empty<MetadataReference>());

    private static IReadOnlyList<Diagnostic> Run(
        Dictionary<string, string> properties, string[] sources, params MetadataReference[] extraReferences)
    {
        var compilation = CSharpCompilation.Create(
            "Tests",
            sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, ParseOptions, $"File{i}.cs")),
            TestHelper.BuildReferences().Concat(extraReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            new[] { new CacheGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions,
            optionsProvider: new GlobalOptionsProvider(properties));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return generatorDiagnostics;
    }

    private sealed class GlobalOptionsProvider(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
    {
        private static readonly Options Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
