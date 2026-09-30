using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator.Tests;

/// <summary>What one run of the generator produced, and the compilation with its output added.</summary>
internal sealed record GeneratorRun(
    IReadOnlyDictionary<string, string> Sources,
    IReadOnlyList<Diagnostic> GeneratorDiagnostics,
    Compilation Compilation)
{
    public IReadOnlyList<string> HintNames => Sources.Keys.ToList();

    public IReadOnlyList<Diagnostic> CompileErrors =>
        Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    /// <summary>Asserts that the run reported nothing and the output compiles.</summary>
    public void ShouldCompileCleanly()
    {
        GeneratorDiagnostics.Should().BeEmpty();
        CompileErrors.Should().BeEmpty();
    }
}
