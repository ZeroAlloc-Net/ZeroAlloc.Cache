using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Every [Cache] interface in a namespace adds a part of the same
/// <c>CacheServiceCollectionExtensions</c> class. A public interface's part says <c>public</c>;
/// an internal one's says nothing, so the class is public when any part is and internal
/// otherwise, and parts never conflict. Only the extension method carries the interface's
/// accessibility. #198
/// </summary>
public sealed class ExtensionClassAccessibilityTests
{
    private static string Iface(string accessibility, string name) => $$"""
        [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
        {{accessibility}} interface {{name}}
        {
            System.Threading.Tasks.ValueTask<string> GetAsync(int id);
        }
        """;

    [Fact]
    public void PublicAndInternalInterfacesInOneNamespace_Compile_WithAPublicClass()
    {
        // Before: the parts said public and internal, CS0262.
        var run = TestHelper.Run($$"""
            namespace N
            {
                {{Iface("public", "IA")}}
                {{Iface("internal", "IB")}}
                internal partial class Hidden { {{Iface("public", "IC")}} }
            }
            """);

        run.ShouldCompileCleanly();
        var extensions = run.Compilation.GetTypeByMetadataName("N.CacheServiceCollectionExtensions")!;
        extensions.DeclaredAccessibility.Should().Be(Accessibility.Public);
        Method(extensions, "AddACache").DeclaredAccessibility.Should().Be(Accessibility.Public);
        Method(extensions, "AddBCache").DeclaredAccessibility.Should().Be(Accessibility.Internal);
        Method(extensions, "AddHidden_CCache").DeclaredAccessibility.Should().Be(Accessibility.Internal);
    }

    [Fact]
    public void OnlyInternalInterfaces_GiveAnInternalClass()
    {
        var run = TestHelper.Run($$"""
            namespace N
            {
                {{Iface("internal", "IA")}}
                {{Iface("internal", "IB")}}
            }
            """);

        run.ShouldCompileCleanly();
        run.Compilation.GetTypeByMetadataName("N.CacheServiceCollectionExtensions")!
            .DeclaredAccessibility.Should().Be(Accessibility.Internal);
        run.Sources["N.IA.Cache.g.cs"].Should().Contain("\nstatic partial class CacheServiceCollectionExtensions");
    }

    [Fact]
    public void PublicInterface_KeepsItsPublicClassDeclaration()
    {
        var run = TestHelper.Run($$"""
            namespace N { {{Iface("public", "IA")}} }
            """);

        run.ShouldCompileCleanly();
        run.Sources["N.IA.Cache.g.cs"].Should().Contain("\npublic static partial class CacheServiceCollectionExtensions");
    }

    private static IMethodSymbol Method(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IMethodSymbol>().Should().ContainSingle().Which;
}
