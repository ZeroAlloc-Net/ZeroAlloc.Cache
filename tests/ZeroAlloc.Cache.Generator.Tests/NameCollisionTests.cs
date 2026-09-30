using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Qualify on collision only. Interfaces whose cache keys would be the same, such as
/// <c>M1.IX</c> and <c>M2.IX</c>, get keys that start with their namespace. Interfaces whose
/// <c>Add...Cache</c> methods would be the same, such as <c>IFoo</c> and <c>Foo</c>, get method
/// names that keep the whole interface name and start with the namespace. Every other interface
/// keeps its names. #199
/// </summary>
public sealed class NameCollisionTests
{
    private static string Iface(string name, string cache = "TtlMs = 1000") => $$"""
        [ZeroAlloc.Cache.Cache({{cache}})]
        public interface {{name}}
        {
            System.Threading.Tasks.ValueTask<string> GetAsync(int id);
        }
        """;

    [Fact]
    public void SameKeysInDifferentNamespaces_AreQualifiedWithTheNamespace()
    {
        // Before: both stored under IX.GetAsync:{id} in the shared cache, and read each other's entries.
        var run = TestHelper.Run($$"""
            namespace M1 { {{Iface("IX")}} {{Iface("IOther")}} }
            namespace M2.Sub { {{Iface("IX", "TtlMs = 1000, UseHybridCache = true")}} }
            """);

        run.ShouldCompileCleanly();
        Key(run, "M1.IX.Cache.g.cs").Should().Be("M1.IX");
        Key(run, "M2.Sub.IX.Cache.g.cs").Should().Be("M2.Sub.IX");
        Key(run, "M1.IOther.Cache.g.cs").Should().Be("IOther");
        // The method names do not collide: each namespace has its own extension class.
        run.Sources["M1.IX.Cache.g.cs"].Should().Contain(" AddXCache<");
        run.Sources["M2.Sub.IX.Cache.g.cs"].Should().Contain(" AddXCache<");
    }

    [Fact]
    public void SameKeysOfNestedInterfaces_AreQualifiedWithTheNamespace()
    {
        var run = TestHelper.Run($$"""
            namespace N1 { public partial class Outer { {{Iface("IFoo")}} } }
            namespace N2 { public partial class Outer { {{Iface("IFoo")}} } }
            """);

        run.ShouldCompileCleanly();
        Key(run, "N1.Outer+IFoo.Cache.g.cs").Should().Be("N1.Outer.IFoo");
        Key(run, "N2.Outer+IFoo.Cache.g.cs").Should().Be("N2.Outer.IFoo");
    }

    [Fact]
    public void InterfaceInTheGlobalNamespace_KeepsItsKey_AndTheOtherIsQualified()
    {
        var run = TestHelper.Run($$"""
            {{Iface("IX")}}
            namespace M { {{Iface("IX")}} }
            """);

        run.ShouldCompileCleanly();
        Key(run, "IX.Cache.g.cs").Should().Be("IX");
        Key(run, "M.IX.Cache.g.cs").Should().Be("M.IX");
    }

    [Fact]
    public void QualifiedKeyEqualToAnotherInterfacesKey_QualifiesThatOneToo()
    {
        // M1.IX becomes M1.IX, which is the key N.M1.IX, nested in type M1, has on its own.
        var run = TestHelper.Run($$"""
            namespace M1 { {{Iface("IX")}} }
            namespace M2 { {{Iface("IX")}} }
            namespace N { public partial class M1 { {{Iface("IX")}} } }
            """);

        run.ShouldCompileCleanly();
        Key(run, "M1.IX.Cache.g.cs").Should().Be("M1.IX");
        Key(run, "M2.IX.Cache.g.cs").Should().Be("M2.IX");
        Key(run, "N.M1+IX.Cache.g.cs").Should().Be("N.M1.IX");
    }

    [Fact]
    public void SameMethodNameInOneNamespace_KeepsTheWholeInterfaceName()
    {
        // Before: IFoo and Foo both got AddFooCache in N.CacheServiceCollectionExtensions, CS0111.
        var run = TestHelper.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace N
            {
                {{Iface("IFoo")}}
                {{Iface("Foo")}}
                {{Iface("IBar")}}
                public sealed class FooImpl : IFoo { public System.Threading.Tasks.ValueTask<string> GetAsync(int id) => new("i"); }
                public sealed class PlainFooImpl : Foo { public System.Threading.Tasks.ValueTask<string> GetAsync(int id) => new("p"); }
                public sealed class BarImpl : IBar { public System.Threading.Tasks.ValueTask<string> GetAsync(int id) => new("b"); }

                public static class Use
                {
                    public static void Register(IServiceCollection services) =>
                        services.AddN_IFooCache<FooImpl>().AddN_FooCache<PlainFooImpl>().AddBarCache<BarImpl>();
                }
            }
            """);

        run.ShouldCompileCleanly();
        // Their keys, IFoo and Foo, differ, so they stay as they were.
        Key(run, "N.IFoo.Cache.g.cs").Should().Be("IFoo");
        Key(run, "N.Foo.Cache.g.cs").Should().Be("Foo");
    }

    [Fact]
    public void SameMethodNameInTheGlobalNamespace_KeepsTheWholeInterfaceName()
    {
        var run = TestHelper.Run($$"""
            {{Iface("IFoo")}}
            {{Iface("Foo")}}
            """);

        run.ShouldCompileCleanly();
        run.Sources["IFoo.Cache.g.cs"].Should().Contain(" AddIFooCache<");
        run.Sources["Foo.Cache.g.cs"].Should().Contain(" AddFooCache<");
    }

    [Fact]
    public void SameMethodNameOfANestedInterface_IsQualified()
    {
        // Outer.IFoo and Outer_Foo both got AddOuter_FooCache.
        var run = TestHelper.Run($$"""
            namespace N
            {
                public partial class Outer { {{Iface("IFoo")}} }
                {{Iface("Outer_Foo")}}
            }
            """);

        run.ShouldCompileCleanly();
        run.Sources["N.Outer+IFoo.Cache.g.cs"].Should().Contain(" AddN_Outer_IFooCache<");
        run.Sources["N.Outer_Foo.Cache.g.cs"].Should().Contain(" AddN_Outer_FooCache<");
    }

    [Fact]
    public void InterfaceThatIsNotGenerated_QualifiesNoOther()
    {
        // Outer.IX gets ZC0006 and IX<T> gets ZC0007: neither has a proxy or keys.
        var run = TestHelper.Run($$"""
            namespace M1 { {{Iface("IX")}} }
            namespace M2 { public class Outer { {{Iface("IX")}} } }
            namespace M3 { {{Iface("IX<T>")}} }
            """);

        run.CompileErrors.Should().BeEmpty();
        run.GeneratorDiagnostics.Select(d => d.Id).Should().BeEquivalentTo("ZC0006", "ZC0007");
        Key(run, "M1.IX.Cache.g.cs").Should().Be("IX");
    }

    [Fact]
    public void UnrelatedEdit_KeepsTheNameCollisionStepCached()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { CSharpSyntaxTree.ParseText($$"""namespace M1 { {{Iface("IX")}} } namespace M2 { {{Iface("IX")}} }""", parseOptions, "A.cs") },
            TestHelper.BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new CacheGenerator().AsSourceGenerator() },
                parseOptions: parseOptions,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true))
            .RunGenerators(compilation);

        driver = driver.RunGenerators(compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace M1; public class Unrelated { }", parseOptions, "B.cs")));

        var outputs = driver.GetRunResult().Results[0]
            .TrackedSteps[CacheGenerator.NameCollisionsTrackingName]
            .SelectMany(step => step.Outputs)
            .ToList();
        outputs.Should().NotBeEmpty();
        outputs.Should().OnlyContain(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
    }

    /// <summary>The interface name the generated file's cache.method tag and keys start with.</summary>
    private static string Key(GeneratorRun run, string hintName)
    {
        const string marker = "__activity?.SetTag(\"cache.method\", \"";
        var source = run.Sources[hintName];
        var start = source.IndexOf(marker, System.StringComparison.Ordinal) + marker.Length;
        var tag = source.Substring(start, source.IndexOf('"', start) - start);
        var name = tag.Substring(0, tag.LastIndexOf('.'));
        // Every key of the interface uses the same name.
        source.Should().Contain("$\"" + name + ".GetAsync:{id}\"");
        return name;
    }
}
