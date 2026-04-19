using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Cache.Generator;

[Generator]
public sealed class CacheGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, ct) => TryParse(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(models, static (ctx, model) =>
        {
            foreach (var d in model.Diagnostics)
                ctx.ReportDiagnostic(d);

            if (model.CachedMethods.Length > 0 || model.PassthroughMethods.Length > 0)
                CacheWriter.Write(ctx, model);
        });
    }

    private static CacheModel? TryParse(
        GeneratorSyntaxContext ctx,
        System.Threading.CancellationToken ct)
    {
        // Implement in Task 4
        return null;
    }
}
