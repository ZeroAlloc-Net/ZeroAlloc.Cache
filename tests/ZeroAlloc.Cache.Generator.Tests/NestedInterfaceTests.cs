using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// A nested <c>[Cache]</c> interface gets its proxy and bounded-cache holder inside partial
/// declarations of its containing types, and its <c>Add...Cache</c> extension, which has to stay
/// at namespace level, is named after the containing types. An interface the namespace-level
/// extension cannot name, a generic one or one that is not accessible from the namespace, is
/// reported and not generated. #194
/// </summary>
public sealed class NestedInterfaceTests
{
    private static string Iface(string name, string cache = "TtlMs = 1000") => $$"""
        [ZeroAlloc.Cache.Cache({{cache}})]
        public interface {{name}}
        {
            System.Threading.Tasks.ValueTask<string> GetAsync(int id, System.Threading.CancellationToken ct);
        }
        """;

    private static string Impl(string name, string iface) => $$"""
        public sealed class {{name}} : {{iface}}
        {
            public System.Threading.Tasks.ValueTask<string> GetAsync(int id, System.Threading.CancellationToken ct) => new("x");
        }
        """;

    [Fact]
    public void SameNamedNestedInterfaces_EachGetTheirOwnProxyAndExtension()
    {
        // Before: both proxies implemented global::N.IFoo, which does not exist (CS0234), and
        // both were declared as N.IFooCacheProxy (CS0101), with two AddFooCache methods.
        var run = TestHelper.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace N
            {
                public partial class Outer1 { {{Iface("IFoo")}} }
                public partial class Outer2 { {{Iface("IFoo", "TtlMs = 1000, MaxEntries = 10")}} }
                {{Impl("Impl1", "Outer1.IFoo")}}
                {{Impl("Impl2", "Outer2.IFoo")}}

                public static class Use
                {
                    public static void Register(IServiceCollection services) =>
                        services.AddOuter1_FooCache<Impl1>().AddOuter2_FooCache<Impl2>();
                }
            }
            """);

        run.ShouldCompileCleanly();
        run.Compilation.GetTypeByMetadataName("N.Outer1+IFooCacheProxy").Should().NotBeNull();
        run.Compilation.GetTypeByMetadataName("N.Outer2+IFooCacheProxy").Should().NotBeNull();
        run.Compilation.GetTypeByMetadataName("N.Outer2+IFooBoundedCache").Should().NotBeNull();
    }

    [Fact]
    public void NestedInterfaceNextToATopLevelInterfaceOfTheSameName_KeepsTheTopLevelOutputAsItWas()
    {
        var topLevelOnly = TestHelper.Run($$"""
            namespace N { {{Iface("IFoo")}} }
            """);
        var withNested = TestHelper.Run($$"""
            namespace N { {{Iface("IFoo")}} }
            namespace N { public partial class Outer { {{Iface("IFoo")}} } }
            """);

        withNested.ShouldCompileCleanly();
        withNested.Sources["N.IFoo.Cache.g.cs"].Should().Be(topLevelOnly.Sources["N.IFoo.Cache.g.cs"]);
    }

    [Fact]
    public void NestedInterface_IsGeneratedInsideItsContainingTypes()
    {
        var run = TestHelper.Run($$"""
            namespace N
            {
                public static partial class Outer
                {
                    internal partial class Middle { {{Iface("IFoo", "TtlMs = 1000, MaxEntries = 10")}} }
                }
            }
            """);

        run.ShouldCompileCleanly();
        var source = run.Sources["N.Outer+Middle+IFoo.Cache.g.cs"].ReplaceLineEndings("\n");
        source.Should().Contain("""

            namespace N;

            partial class Outer
            {
                partial class Middle
                {
                    internal sealed class IFooCacheProxy : global::N.Outer.Middle.IFoo
                    {
            """.ReplaceLineEndings("\n"));
        source.Should().Contain("\n        internal sealed class IFooBoundedCache : global::System.IDisposable\n");
        source.Should().Contain("""

            static partial class CacheServiceCollectionExtensions
            {
                internal static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddOuter_Middle_FooCache<
            """.ReplaceLineEndings("\n"));
        source.Should().Contain(".TryAddSingleton<global::N.Outer.Middle.IFooBoundedCache>(services, static _ => new global::N.Outer.Middle.IFooBoundedCache());");
        source.Should().Contain("new global::N.Outer.Middle.IFooCacheProxy(");
        // The key and the telemetry tag name the containing types, so two same-named nested
        // interfaces never read each other's entries from the shared IMemoryCache.
        source.Should().Contain("\"Outer.Middle.IFoo.GetAsync:{id}\"");
        source.Should().Contain("\"cache.method\", \"Outer.Middle.IFoo.GetAsync\"");
    }

    [Theory]
    [InlineData("public partial struct Holder<T> where T : unmanaged", false)]
    [InlineData("public partial interface Holder", true)]
    [InlineData("public partial record struct Holder(int Id)", true)]
    [InlineData("public partial record Holder(string Name)", true)]
    [InlineData("public readonly ref partial struct Holder", true)]
    [InlineData("public abstract partial class Holder", true)]
    public void ContainersOfEveryKind_AreReopenedWithTheirKind(string container, bool generated)
    {
        var run = TestHelper.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace N
            {
                {{container}}
                {
                    {{Iface("IFoo")}}
                }
            }
            """);

        if (generated)
        {
            run.ShouldCompileCleanly();
            run.HintNames.Should().Equal("N.Holder+IFoo.Cache.g.cs");
        }
        else
        {
            // A generic containing type is covered by ZC0007.
            run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZC0007");
            run.CompileErrors.Should().BeEmpty();
        }
    }

    [Fact]
    public void HybridAndSharedCacheMethods_InANestedInterface_Compile()
    {
        var run = TestHelper.Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace N
            {
                public partial class Outer
                {
                    public interface IFoo
                    {
                        [Cache(TtlMs = 1000, UseHybridCache = true)]
                        ValueTask<string> GetAsync(int id, CancellationToken ct);

                        [Cache(TtlMs = 1000)]
                        ValueTask<int> CountAsync(int id, CancellationToken ct);

                        [Cache(TtlMs = 1000, MaxEntries = 5)]
                        Task<long> SizeAsync(int id);

                        void Reset();
                    }
                }
            }
            """);

        run.ShouldCompileCleanly();
    }

    [Fact]
    public void VerbatimNames_AreWrittenAsVerbatimIdentifiers()
    {
        var run = TestHelper.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace N
            {
                public partial class @class { {{Iface("@event")}} }
                {{Impl("Impl", "@class.@event")}}

                public static class Use
                {
                    public static void Register(IServiceCollection services) => services.Addclass_eventCache<Impl>();
                }
            }
            """);

        run.ShouldCompileCleanly();
    }

    [Fact]
    public void ContainerDeclaredInSeveralParts_IsReopenedOnce()
    {
        var run = TestHelper.Run(
            "namespace N { public partial class Outer { public int A; } }",
            $$"""
            namespace N { partial class Outer { {{Iface("IFoo")}} } }
            """);

        run.ShouldCompileCleanly();
        run.HintNames.Should().Equal("N.Outer+IFoo.Cache.g.cs");
    }

    [Fact]
    public void NestedInterfaceInTheGlobalNamespace_IsGenerated()
    {
        var run = TestHelper.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            public partial class Outer { {{Iface("IFoo")}} }
            {{Impl("Impl", "Outer.IFoo")}}
            public static class Use
            {
                public static void Register(IServiceCollection services) => services.AddOuter_FooCache<Impl>();
            }
            """);

        run.ShouldCompileCleanly();
    }

    [Fact]
    public void ProtectedInternalNestedInterface_IsGeneratedWithAnInternalExtension()
    {
        var run = TestHelper.Run("""
            namespace N
            {
                public partial class Outer
                {
                    [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
                    protected internal interface IFoo
                    {
                        System.Threading.Tasks.ValueTask<string> GetAsync(int id);
                    }
                }
            }
            """);

        run.ShouldCompileCleanly();
        var source = run.Sources["N.Outer+IFoo.Cache.g.cs"];
        source.Should().Contain("\nstatic partial class CacheServiceCollectionExtensions");
        source.Should().Contain("internal static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddOuter_FooCache<");
    }

    [Fact]
    public void ContainingTypeNotPartial_ReportsZC0006_AndGeneratesNothingForIt()
    {
        var source = $$"""
            namespace N
            {
                public class Outer { {{Iface("IFoo")}} }
                {{Iface("IOther")}}
            }
            """;
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().Equal("N.IOther.Cache.g.cs");
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0006");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "No cache proxy is generated for interface 'N.Outer.IFoo' because its containing type 'N.Outer' is not partial");
        AssertOnName(diagnostic, source, "IFoo");
    }

    [Fact]
    public void NonPartialOuterTypeAbovePartialMiddleType_ReportsZC0006_NamingTheOuterType()
    {
        var source = $$"""
            namespace N
            {
                public class Outer { public partial class Middle { {{Iface("IFoo")}} } }
            }
            """;
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().BeEmpty();
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0006");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "No cache proxy is generated for interface 'N.Outer.Middle.IFoo' because its containing type 'N.Outer' is not partial");
        AssertOnName(diagnostic, source, "IFoo");
    }

    [Theory]
    [InlineData("namespace N { __IFOO__ }", "N.IFoo<T>")]
    [InlineData("namespace N { public partial class Outer<T> { __IFOO__ } }", "N.Outer<T>.IFoo")]
    [InlineData("namespace N { public partial class Outer<T> { public partial class Middle { __IFOO__ } } }", "N.Outer<T>.Middle.IFoo")]
    public void GenericInterfaceOrGenericContainer_ReportsZC0007_AndGeneratesNothing(string template, string display)
    {
        // Before: the proxy implemented the interface without its type arguments (CS0305).
        var name = display.EndsWith("<T>", StringComparison.Ordinal) ? "IFoo<T>" : "IFoo";
        var source = template.Replace("__IFOO__", Iface(name), StringComparison.Ordinal);
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().BeEmpty();
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0007");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"No cache proxy is generated for interface '{display}' because it is generic or nested in a generic type, which its Add...Cache extension method cannot name");
        AssertOnName(diagnostic, source, "IFoo");
    }

    [Theory]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("private protected")]
    public void InterfaceNotAccessibleFromItsNamespace_ReportsZC0008_AndGeneratesNothing(string accessibility)
    {
        // Before: the namespace-level proxy and extension referred to it, CS0122.
        var source = $$"""
            namespace N
            {
                public partial class Outer
                {
                    [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
                    {{accessibility}} interface IFoo
                    {
                        System.Threading.Tasks.ValueTask<string> GetAsync(int id);
                    }
                }
            }
            """;
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().BeEmpty();
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0008");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "No cache proxy is generated for interface 'N.Outer.IFoo' because it is private or protected, or nested in such a type, which its Add...Cache extension method cannot name");
        AssertOnName(diagnostic, source, "IFoo");
    }

    [Fact]
    public void InterfaceInAPrivateContainingType_ReportsZC0008()
    {
        var run = TestHelper.Run($$"""
            namespace N
            {
                public partial class Outer { private partial class Hidden { {{Iface("IFoo")}} } }
            }
            """);

        run.CompileErrors.Should().BeEmpty();
        run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZC0008");
    }

    [Fact]
    public void FileLocalInterface_ReportsZC0009_AndOtherInterfacesAreStillGenerated()
    {
        var source = """
            namespace N
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
                file interface IFoo
                {
                    System.Threading.Tasks.ValueTask<string> GetAsync(int id);
                }
            }
            """ + "\n" + $$"""
            namespace N { {{Iface("IOther")}} }
            """;
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().Equal("N.IOther.Cache.g.cs");
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0009");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "No cache proxy is generated for interface 'N.IFoo' because it is file-local or nested in a file-local type, and a generated file cannot refer to a file-local type");
        AssertOnName(diagnostic, source, "IFoo");
    }

    [Fact]
    public void InterfaceNestedInAFileLocalType_ReportsZC0009()
    {
        var run = TestHelper.Run($$"""
            namespace N { file partial class Outer { {{Iface("IFoo")}} } }
            """);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().BeEmpty();
        run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZC0009");
    }

    [Fact]
    public void InterfaceThatIsNotGenerated_ReportsOnlyWhyNot()
    {
        // ZC0002 about a reference-type key would be noise on an interface that gets no proxy.
        var run = TestHelper.Run("""
            namespace N
            {
                public class Outer
                {
                    [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
                    public interface IFoo
                    {
                        System.Threading.Tasks.ValueTask<string> GetAsync(object key);
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZC0006");
    }

    [Fact]
    public void PartialInterfaceWithAttributesInTwoParts_GetsOneProxy()
    {
        // Each part is a candidate on its own; the interface must still be generated once.
        var run = TestHelper.Run("""
            using System.Threading.Tasks;
            using ZeroAlloc.Cache;
            namespace N
            {
                [Cache(TtlMs = 1000)]
                public partial interface IFoo { ValueTask<string> GetAsync(int id); }

                public partial interface IFoo
                {
                    [Cache(TtlMs = 2000)]
                    ValueTask<int> CountAsync(int id);
                }
            }
            """);

        run.ShouldCompileCleanly();
        run.HintNames.Should().Equal("N.IFoo.Cache.g.cs");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialInterfaceInTwoFiles_IsReportedOnTheEarlierFile_WhateverTheCompilationOrder(bool reversed)
    {
        var a = ("A.cs", """
            namespace N { public partial interface IFoo<T> { System.Threading.Tasks.ValueTask<int> CountAsync(int id); } }
            """);
        var b = ("B.cs", """
            namespace N
            {
                [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
                public partial interface IFoo<T> { System.Threading.Tasks.ValueTask<T> GetAsync(int id); }
            }
            """);

        var run = reversed ? TestHelper.Run(b, a) : TestHelper.Run(a, b);

        run.CompileErrors.Should().BeEmpty();
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0007");
        diagnostic.Location.GetLineSpan().Path.Should().Be("A.cs");
    }

    private static void AssertOnName(Diagnostic diagnostic, string source, string name)
    {
        diagnostic.Location.IsInSource.Should().BeTrue();
        var span = diagnostic.Location.SourceSpan;
        source.Substring(span.Start, span.Length).Should().Be(name);
        source.Substring(span.Start - "interface ".Length, "interface ".Length).Should().Be("interface ");
    }
}
