using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>
/// Roslyn compares hint names ignoring case. Interfaces whose qualified names differ only in case
/// would need the same file, so the interface declared first keeps it, and every later one gets
/// ZC0010 and is skipped. Everything else is generated. #197
/// </summary>
public sealed class CaseCollisionTests
{
    private static string Iface(string name) => $$"""
        [ZeroAlloc.Cache.Cache(TtlMs = 1000)]
        public interface {{name}}
        {
            System.Threading.Tasks.ValueTask<string> GetAsync(int id);
        }
        """;

    [Fact]
    public void NamesDifferingOnlyInCase_ReportZC0010OnTheLaterInterface_AndGenerateTheRest()
    {
        // Before: AddSource threw, CS8785 was reported, and no interface in the project got a proxy.
        var source = $$"""
            namespace N
            {
                {{Iface("IFoo")}}
                {{Iface("iFoo")}}
                {{Iface("IOther")}}
            }
            """;
        var run = TestHelper.Run(source);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().BeEquivalentTo("N.IFoo.Cache.g.cs", "N.IOther.Cache.g.cs");
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0010");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "No cache proxy is generated for interface 'N.iFoo' because its file name 'N.iFoo.Cache.g.cs' differs only in case from that of interface 'N.IFoo'");
        diagnostic.Location.IsInSource.Should().BeTrue();
        var span = diagnostic.Location.SourceSpan;
        source.Substring(span.Start, span.Length).Should().Be("iFoo");
    }

    [Fact]
    public void ThreeInterfacesDifferingOnlyInCase_ReportTwoAndGenerateOne()
    {
        var run = TestHelper.Run($$"""
            {{Iface("IApp")}}
            {{Iface("IAPP")}}
            {{Iface("Iapp")}}
            """);

        run.CompileErrors.Should().BeEmpty();
        run.HintNames.Should().Equal("IApp.Cache.g.cs");
        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZC0010", "ZC0010");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcrossFiles_TheInterfaceInTheEarlierFileWins_WhateverTheCompilationOrder(bool reversed)
    {
        var a = ("A.cs", $$"""namespace N { {{Iface("iFoo")}} }""");
        var b = ("B.cs", $$"""namespace N { {{Iface("IFoo")}} }""");

        var run = reversed ? TestHelper.Run(b, a) : TestHelper.Run(a, b);

        run.HintNames.Should().Equal("N.iFoo.Cache.g.cs");
        var diagnostic = run.GeneratorDiagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("ZC0010");
        diagnostic.Location.GetLineSpan().Path.Should().Be("B.cs");
    }

    [Fact]
    public void InterfaceSkippedForAnotherReason_DoesNotTakeTheName()
    {
        // A ZC0006 interface generates nothing, so it holds no file name to clash with.
        var run = TestHelper.Run($$"""
            namespace N
            {
                public class Outer { {{Iface("IFoo")}} }
                public partial class outer { {{Iface("IFoo")}} }
            }
            """);

        run.HintNames.Should().Equal("N.outer+IFoo.Cache.g.cs");
        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZC0006");
    }
}
