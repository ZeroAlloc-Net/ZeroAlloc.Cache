using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Cache.Generator.Tests;

internal static class TestHelper
{
    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    /// <summary>
    /// AppDomain assemblies only cover what the test host has already loaded, so the assemblies the
    /// generated code depends on are appended explicitly — without them the generated trees fail to
    /// bind and CS-level defects (see #87) stay invisible to the harness.
    /// </summary>
    private const string HybridCacheAssemblyFileName = "Microsoft.Extensions.Caching.Hybrid.dll";

    /// <summary>
    /// <paramref name="referenceHybridCache"/> set to <see langword="false"/> leaves the HybridCache
    /// assembly out, reproducing a project that excludes Microsoft.Extensions.Caching.Hybrid, where
    /// ZC0003 fires. The HybridCache type itself stays resolvable, as it does for that project,
    /// because it lives in Microsoft.Extensions.Caching.Abstractions.
    /// </summary>
    internal static List<MetadataReference> BuildReferences(bool referenceHybridCache = true) =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location))
            .Concat(new[]
            {
                typeof(ZeroAlloc.Cache.CacheAttribute).Assembly.Location,
                typeof(Microsoft.Extensions.Caching.Hybrid.HybridCache).Assembly.Location,
                typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache).Assembly.Location,
                typeof(Microsoft.Extensions.Caching.Memory.MemoryCache).Assembly.Location,
                typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location,
                typeof(Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions).Assembly.Location,
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => referenceHybridCache || !string.Equals(
                System.IO.Path.GetFileName(p), HybridCacheAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

    /// <summary>
    /// Snapshots the generated files. <paramref name="languageVersion"/> is the consumer's C# version.
    /// </summary>
    public static void Verify(string source, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = BuildReferences();

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CacheGenerator();
        var driver = CSharpGeneratorDriver
            .Create(generator)
            .WithUpdatedParseOptions(parseOptions)
            .RunGenerators(compilation);

        GeneratorSnapshot.Verify(driver);
    }

    public static IReadOnlyList<string> GetGeneratedFileNames(string source, bool referenceHybridCache = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        var references = BuildReferences(referenceHybridCache);

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(ParseOptions)
            .RunGenerators(compilation);

        return driver.GetRunResult().GeneratedTrees
            .Select(t => System.IO.Path.GetFileName(t.FilePath))
            .ToList();
    }

    public static IReadOnlyList<string> GetGeneratedSources(
        string source, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(parseOptions)
            .RunGenerators(compilation);

        return driver.GetRunResult().GeneratedTrees
            .Select(t => t.GetText().ToString())
            .ToList();
    }

    /// <summary>
    /// The consumer compilation with the generated trees added, for semantic checks of the
    /// generated code. <paramref name="languageVersion"/> is the consumer's C# version.
    /// </summary>
    public static (Compilation Compilation, IReadOnlyList<SyntaxTree> GeneratedTrees) Compile(
        string source, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(parseOptions)
            .RunGenerators(compilation)
            .GetRunResult();

        return (compilation.AddSyntaxTrees(result.GeneratedTrees), result.GeneratedTrees);
    }

    public static Task<IReadOnlyList<Diagnostic>> GetDiagnostics(
        string source, bool referenceHybridCache = true, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = BuildReferences(referenceHybridCache);

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CacheGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .WithUpdatedParseOptions(parseOptions)
            .RunGenerators(compilation);

        var result = driver.GetRunResult();
        var updated = compilation.AddSyntaxTrees(result.GeneratedTrees);
        var diags = result.Diagnostics
            .Concat(updated.GetDiagnostics())
            .ToList();

        return System.Threading.Tasks.Task.FromResult<IReadOnlyList<Diagnostic>>(diags);
    }

    /// <summary>
    /// Runs the generator over <paramref name="sources"/>, named <c>File0.cs</c>, <c>File1.cs</c>
    /// and so on, and returns its output and the compilation with that output added.
    /// </summary>
    public static GeneratorRun Run(params string[] sources) =>
        Run(sources.Select((source, i) => ($"File{i}.cs", source)).ToArray());

    public static GeneratorRun Run(params (string Path, string Source)[] files)
    {
        var compilation = CSharpCompilation.Create(
            "Tests",
            files.Select(f => CSharpSyntaxTree.ParseText(f.Source, ParseOptions, f.Path)),
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver.Create(new CacheGenerator())
            .WithUpdatedParseOptions(ParseOptions)
            .RunGenerators(compilation)
            .GetRunResult();

        return new GeneratorRun(
            result.Results[0].GeneratedSources.ToDictionary(
                s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal),
            result.Diagnostics,
            compilation.AddSyntaxTrees(result.GeneratedTrees));
    }
}
