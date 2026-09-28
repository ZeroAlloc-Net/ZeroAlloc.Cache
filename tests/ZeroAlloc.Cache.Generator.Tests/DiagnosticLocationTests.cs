using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Every diagnostic points at the code it is about, as a source location bound to the syntax
/// tree, so the IDE can navigate to it and <c>#pragma warning disable</c> can suppress one case.
/// See ZeroAlloc-Net/.github#46.
/// </summary>
public sealed class DiagnosticLocationTests
{
    private const string FilePath = "/src/Services.cs";

    // The tracking name of the step that builds each interface's model.
    private const string ModelsStep = "CacheModels";

    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    [Fact]
    public void ZC0001_PointsAtTheMethodsOwnCacheAttribute()
    {
        var (source, spans) = Unmark("""
            using System.Threading.Tasks;
            namespace T;
            public interface IMyService
            {
                [[|ZeroAlloc.Cache.Cache(TtlMs = 30_000, Sliding = true, UseHybridCache = true)|]]
                ValueTask<string> GetAsync(int id);
            }
            """);

        AssertAt(Single(Run(source), "ZC0001").Location, source, spans[0]);
    }

    [Fact]
    public void ZC0001_FromTheInterfaceAttribute_PointsAtTheMethod()
    {
        // The interface attribute applies to every method, and the warning names one of them.
        var (source, spans) = Unmark("""
            using System.Threading.Tasks;
            namespace T;
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000, Sliding = true, UseHybridCache = true)]
            public interface IMyService
            {
                ValueTask<string> [|GetAsync|](int id);
            }
            """);

        AssertAt(Single(Run(source), "ZC0001").Location, source, spans[0]);
    }

    [Fact]
    public void ZC0002_PointsAtTheParameter()
    {
        var (source, spans) = Unmark("""
            using System.Threading;
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { }
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(int id, ProductFilter [|filter|], CancellationToken ct);
            }
            """);

        AssertAt(Single(Run(source), "ZC0002").Location, source, spans[0]);
    }

    [Fact]
    public void ZC0003_PointsAtTheInterface()
    {
        var (source, spans) = Unmark("""
            using System.Threading.Tasks;
            namespace T;
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000, UseHybridCache = true)]
            public interface [|IMyService|]
            {
                ValueTask<string> GetAsync(int id);
            }
            """);

        AssertAt(Single(Run(source, referenceHybridCache: false), "ZC0003").Location, source, spans[0]);
    }

    [Fact]
    public void ZC0004_PointsAtTheInterface()
    {
        var (source, spans) = Unmark("""
            using System.Threading.Tasks;
            namespace T;
            public interface [|IMyService|]
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 30_000, MaxEntries = 500)]
                ValueTask<string> GetAsync(int id);

                [ZeroAlloc.Cache.Cache(TtlMs = 30_000, MaxEntries = 10_000)]
                ValueTask<string> GetOtherAsync(int id);
            }
            """);

        AssertAt(Single(Run(source), "ZC0004").Location, source, spans[0]);
    }

    [Fact]
    public void ZC0005_PointsAtTheIgnoredCacheAttribute()
    {
        var (source, spans) = Unmark("""
            namespace T;
            public interface IMyService
            {
                [[|ZeroAlloc.Cache.Cache(TtlMs = 30_000)|]]
                int Count();
            }
            """);

        AssertAt(Single(Run(source), "ZC0005").Location, source, spans[0]);
    }

    [Fact]
    public void PragmaAroundOneParameter_SuppressesThatDiagnosticOnly()
    {
        const string source = """
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { }
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(
            #pragma warning disable ZC0002
                    ProductFilter quiet,
            #pragma warning restore ZC0002
                    ProductFilter loud);
            }
            """;

        var zc0002 = Run(source).Where(d => string.Equals(d.Id, "ZC0002", StringComparison.Ordinal)).ToList();

        zc0002.Should().HaveCount(2);
        zc0002.First(d => d.GetMessage().Contains("'quiet'", StringComparison.Ordinal)).IsSuppressed.Should().BeTrue();
        zc0002.First(d => d.GetMessage().Contains("'loud'", StringComparison.Ordinal)).IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void EditAboveTheInterface_ReportsTheDiagnosticAtItsNewPosition()
    {
        // The edit leaves the model's methods unchanged. A model that compared its diagnostics by
        // count only was treated as unchanged, and the driver replayed the diagnostic from the old
        // tree, at the old line, where the IDE shows it and #pragma looks for it.
        const string body = """
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { }
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(ProductFilter filter);
            }
            """;
        var edited = "// a comment added above\n" + body;

        var tree = CSharpSyntaxTree.ParseText(body, ParseOptions, FilePath);
        var compilation = CreateCompilation(tree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(ParseOptions)
            .RunGenerators(compilation);

        var editedTree = CSharpSyntaxTree.ParseText(edited, ParseOptions, FilePath);
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(tree, editedTree));

        var diagnostic = Single(driver.GetRunResult().Diagnostics, "ZC0002");
        diagnostic.Location.SourceTree.Should().BeSameAs(editedTree);
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.Should().Be(7);
    }

    [Fact]
    public void UnrelatedEdit_KeepsTheModelCached()
    {
        // A model that carries a diagnostic must still compare equal when another file changes,
        // or every keystroke anywhere re-emits the proxy.
        const string source = """
            using System.Threading.Tasks;
            namespace T;
            public class ProductFilter { }
            [ZeroAlloc.Cache.Cache(TtlMs = 30_000)]
            public interface IMyService
            {
                ValueTask<string> GetAsync(ProductFilter filter);
            }
            """;

        var compilation = CreateCompilation(CSharpSyntaxTree.ParseText(source, ParseOptions, FilePath));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new CacheGenerator().AsSourceGenerator() },
                parseOptions: ParseOptions,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true))
            .RunGenerators(compilation);

        var unrelated = CSharpSyntaxTree.ParseText("namespace T; public class Unrelated { }", ParseOptions, "/src/Unrelated.cs");
        driver = driver.RunGenerators(compilation.AddSyntaxTrees(unrelated));

        var results = driver.GetRunResult().Results;
        results.Should().HaveCount(1);
        var run = results[0];
        run.TrackedSteps.Should().ContainKey(ModelsStep);
        var outputs = run.TrackedSteps[ModelsStep]
            .SelectMany(step => step.Outputs)
            .ToList();
        outputs.Should().NotBeEmpty();
        outputs.Should().OnlyContain(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);

        // The diagnostic is still reported from the cached model.
        Single(driver.GetRunResult().Diagnostics, "ZC0002").Location.SourceTree!.FilePath.Should().Be(FilePath);
    }

    private static ImmutableArray<Diagnostic> Run(string source, bool referenceHybridCache = true)
    {
        var compilation = CreateCompilation(CSharpSyntaxTree.ParseText(source, ParseOptions, FilePath), referenceHybridCache);
        CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return diagnostics;
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree tree, bool referenceHybridCache = true) =>
        CSharpCompilation.Create(
            "Tests",
            new[] { tree },
            TestHelper.BuildReferences(referenceHybridCache),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static Diagnostic Single(ImmutableArray<Diagnostic> diagnostics, string id)
    {
        var matches = diagnostics.Where(d => string.Equals(d.Id, id, StringComparison.Ordinal)).ToList();
        matches.Should().HaveCount(1);
        return matches[0];
    }

    private static void AssertAt(Location location, string source, TextSpan expected)
    {
        // A source location, bound to the tree, is what #pragma and the IDE need.
        location.Kind.Should().Be(LocationKind.SourceFile);
        location.SourceTree!.FilePath.Should().Be(FilePath);
        location.SourceSpan.Should().Be(expected);
        location.GetLineSpan().Span.Should().Be(SourceText.From(source).Lines.GetLinePositionSpan(expected));
    }

    private static (string Source, List<TextSpan> Spans) Unmark(string marked)
    {
        var sb = new StringBuilder(marked.Length);
        var spans = new List<TextSpan>();
        var start = -1;
        for (var i = 0; i < marked.Length; i++)
        {
            if (string.CompareOrdinal(marked, i, "[|", 0, 2) == 0)
            {
                start = sb.Length;
                i++;
            }
            else if (string.CompareOrdinal(marked, i, "|]", 0, 2) == 0)
            {
                spans.Add(TextSpan.FromBounds(start, sb.Length));
                i++;
            }
            else
            {
                sb.Append(marked[i]);
            }
        }

        return (sb.ToString(), spans);
    }
}
