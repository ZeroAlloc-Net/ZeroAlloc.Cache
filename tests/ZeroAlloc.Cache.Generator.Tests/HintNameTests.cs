using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Every cached interface gets a generated file whose hint name is unique within the compilation.
/// The name was <c>{namespace}_{interface}.Cache.g.cs</c>, so interfaces that differ only in their
/// containing type, their generic arity, or where an underscore sits made <c>AddSource</c> throw,
/// and the generator then produced nothing for the whole project with CS8785. #193
/// </summary>
/// <remarks>
/// The code generated for nested and generic interfaces does not compile yet (#194), so those
/// tests check the file names only.
/// </remarks>
public sealed class HintNameTests
{
    private static string Iface(string name) => $$"""
        [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
        public interface {{name}}
        {
            System.Threading.Tasks.ValueTask<string> GetAsync(string id, System.Threading.CancellationToken ct);
        }
        """;

    [Fact]
    public void UnderscoreInNamespaceOrName_BothGenerated_AndCompile()
    {
        var source = $$"""
            namespace A_B { {{Iface("IC")}} }
            namespace A { {{Iface("B_IC")}} }
            """;

        var (compilation, trees) = TestHelper.Compile(source);

        trees.Select(t => System.IO.Path.GetFileName(t.FilePath)).Should()
            .BeEquivalentTo("A_B.IC.Cache.g.cs", "A.B_IC.Cache.g.cs");
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void GlobalNamespace_HasNoNamespacePart()
    {
        TestHelper.GetGeneratedFileNames(Iface("IFoo")).Should().Equal("IFoo.Cache.g.cs");
    }

    [Fact]
    public void SameNameInDifferentContainingTypes_GetsDistinctHintNames()
    {
        var source = $$"""
            namespace N
            {
                public partial class Outer1 { {{Iface("IFoo")}} }
                public partial class Outer2 { {{Iface("IFoo")}} }
            }
            """;

        TestHelper.GetGeneratedFileNames(source).Should()
            .BeEquivalentTo("N.Outer1+IFoo.Cache.g.cs", "N.Outer2+IFoo.Cache.g.cs");
    }

    [Fact]
    public void NestedInGenericContainer_CarriesArity()
    {
        var source = $$"""
            namespace N
            {
                public partial class Outer<T> { {{Iface("IFoo")}} }
            }
            """;

        TestHelper.GetGeneratedFileNames(source).Should().Equal("N.Outer`1+IFoo.Cache.g.cs");
    }

    [Fact]
    public void GenericAndNonGenericOfSameName_GetDistinctHintNames()
    {
        var source = $$"""
            namespace N
            {
                {{Iface("IFoo")}}
                {{Iface("IFoo<T>")}}
            }
            """;

        TestHelper.GetGeneratedFileNames(source).Should()
            .BeEquivalentTo("N.IFoo.Cache.g.cs", "N.IFoo`1.Cache.g.cs");
    }

    [Fact]
    public void NestedAndTopLevelInSameNamedNamespace_GetDistinctHintNames()
    {
        var source = $$"""
            namespace N
            {
                public partial class Outer { {{Iface("IFoo")}} }
            }
            namespace N.Outer2 { {{Iface("IFoo")}} }
            """;

        TestHelper.GetGeneratedFileNames(source).Should()
            .BeEquivalentTo("N.Outer+IFoo.Cache.g.cs", "N.Outer2.IFoo.Cache.g.cs");
    }

    [Fact]
    public void VerbatimAndNonAsciiNames_AreKept()
    {
        var source = $$"""
            namespace @event { {{Iface("IÜber")}} }
            """;

        TestHelper.GetGeneratedFileNames(source).Should().Equal("event.IÜber.Cache.g.cs");
    }

    [Theory]
    [InlineData("App.IFoo", "App.IFoo")]
    [InlineData("App.Outer`1+IFoo", "App.Outer`1+IFoo")]
    [InlineData("a<b>", "a-u003Cb-u003E")]
    [InlineData("a b", "a-u0020b")]
    [InlineData("a@b", "a-u0040b")]
    [InlineData("\U0001D49Cx", "\U0001D49Cx")]
    public void Sanitize_KeepsIdentifierCharactersAndEscapesTheRest(string input, string expected)
    {
        HintNames.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_EscapesALoneSurrogate()
    {
        HintNames.Sanitize("a" + '\uD800' + "b").Should().Be("a-uD800b");
    }
}
