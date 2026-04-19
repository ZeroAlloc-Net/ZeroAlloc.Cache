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
            var hasErrors = false;
            foreach (var d in model.Diagnostics)
            {
                ctx.ReportDiagnostic(d);
                if (d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    hasErrors = true;
            }

            if (!hasErrors && (model.CachedMethods.Length > 0 || model.PassthroughMethods.Length > 0))
                CacheWriter.Write(ctx, model);
        });
    }

    private static CacheModel? TryParse(
        GeneratorSyntaxContext ctx,
        System.Threading.CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.Node is not Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax)
            return null;

        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol symbol)
            return null;

        var ifaceAttr = FindCacheAttr(symbol);
        var ifaceConfig = ifaceAttr != null ? ReadConfig(ifaceAttr) : null;

        var cachedMethods = new System.Collections.Generic.List<CachedMethodModel>();
        var passthroughMethods = new System.Collections.Generic.List<PassthroughMethodModel>();
        var diagnostics = new System.Collections.Generic.List<Microsoft.CodeAnalysis.Diagnostic>();

        foreach (var member in symbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            ParseMember(member, ifaceConfig, cachedMethods, passthroughMethods, diagnostics);
        }

        if (cachedMethods.Count == 0 && passthroughMethods.Count == 0 && diagnostics.Count == 0)
            return null;

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        var ifaceFqn = symbol.ContainingNamespace.IsGlobalNamespace
            ? symbol.Name
            : symbol.ContainingNamespace.ToDisplayString() + "." + symbol.Name;

        return new CacheModel(
            ns,
            symbol.Name,
            ifaceFqn,
            cachedMethods.Exists(static m => m.EffectiveConfig.UseHybridCache),
            cachedMethods.Exists(static m => m.EffectiveConfig.MaxEntries > 0),
            System.Collections.Immutable.ImmutableArray.CreateRange(cachedMethods),
            System.Collections.Immutable.ImmutableArray.CreateRange(passthroughMethods),
            System.Collections.Immutable.ImmutableArray.CreateRange(diagnostics)
        );
    }

    private static void ParseMember(
        ISymbol member,
        CacheConfig? ifaceConfig,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods,
        System.Collections.Generic.List<PassthroughMethodModel> passthroughMethods,
        System.Collections.Generic.List<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        if (member is not IMethodSymbol method)
            return;
        if (method.MethodKind != MethodKind.Ordinary)
            return;

        var methodAttr = FindCacheAttr(method);
        var methodConfig = methodAttr != null ? ReadConfig(methodAttr) : null;
        var effectiveConfig = methodConfig ?? ifaceConfig;

        BuildParamStrings(method, out var paramList, out var argList, out var nonCtArgList,
            out var keyArgs, out var hasCt, out var ctParamName, out var keyParams);

        // A method is passthrough if there is no effective config, OR if the return type
        // is non-generic (e.g. ValueTask, Task, void) — there is no cacheable value to store.
        // We always treat non-generic returns as passthrough even with explicit [Cache] to
        // avoid emitting broken generated code (caching ValueTask with no T is meaningless).
        bool isNonGenericReturn = method.ReturnType is not INamedTypeSymbol { IsGenericType: true };
        bool isPassthrough = effectiveConfig == null || isNonGenericReturn;

        if (isPassthrough)
        {
            AddPassthrough(method, passthroughMethods, paramList, argList);
            return;
        }

        EmitDiagnostics(method, effectiveConfig!, keyParams, diagnostics);
        AddCachedMethod(method, effectiveConfig!, paramList, argList, nonCtArgList, keyArgs,
            hasCt, ctParamName, keyParams, cachedMethods);
    }

    private static void BuildParamStrings(
        IMethodSymbol method,
        out string paramList,
        out string argList,
        out string nonCtArgList,
        out string keyArgs,
        out bool hasCt,
        out string? ctParamName,
        out System.Collections.Generic.List<KeyParam> keyParams)
    {
        var paramSb = new System.Text.StringBuilder();
        var argSb = new System.Text.StringBuilder();
        var nonCtArgSb = new System.Text.StringBuilder();
        var keySb = new System.Text.StringBuilder();
        hasCt = false;
        ctParamName = null;
        keyParams = new System.Collections.Generic.List<KeyParam>();
        bool firstParam = true;
        bool firstNonCtParam = true;

        foreach (var param in method.Parameters)
        {
            if (!firstParam) { paramSb.Append(", "); argSb.Append(", "); }
            firstParam = false;

            var fqn = param.Type.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
            paramSb.Append(fqn).Append(' ').Append(param.Name);
            argSb.Append(param.Name);

            bool isCt = string.Equals(param.Type.ToDisplayString(), "System.Threading.CancellationToken", System.StringComparison.Ordinal);
            if (isCt)
            {
                hasCt = true;
                ctParamName = param.Name;
            }
            else
            {
                if (!firstNonCtParam) nonCtArgSb.Append(", ");
                firstNonCtParam = false;
                nonCtArgSb.Append(param.Name);
                keySb.Append(":{").Append(param.Name).Append('}');

                bool isRef = param.Type.IsReferenceType
                    && param.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.None;
                keyParams.Add(new KeyParam(param.Name, isRef));
            }
        }

        paramList = paramSb.ToString();
        argList = argSb.ToString();
        nonCtArgList = nonCtArgSb.ToString();
        keyArgs = keySb.ToString();
    }

    private static void AddPassthrough(
        IMethodSymbol method,
        System.Collections.Generic.List<PassthroughMethodModel> passthroughMethods,
        string paramList,
        string argList)
    {
        passthroughMethods.Add(new PassthroughMethodModel(
            method.Name,
            method.ReturnType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat),
            paramList,
            argList
        ));
    }

    private static void EmitDiagnostics(
        IMethodSymbol method,
        CacheConfig effectiveConfig,
        System.Collections.Generic.List<KeyParam> keyParams,
        System.Collections.Generic.List<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        Location? firstLoc = null;
        foreach (var loc in method.Locations)
        {
            firstLoc = loc;
            break;
        }

        if (effectiveConfig.Sliding && effectiveConfig.UseHybridCache)
        {
            diagnostics.Add(Microsoft.CodeAnalysis.Diagnostic.Create(
                CacheDiagnostics.SlidingNotSupportedOnHybridCache,
                firstLoc,
                method.Name));
        }

#pragma warning disable HLQ012 // CollectionsMarshal.AsSpan not available on netstandard2.0
        foreach (var kp in keyParams)
#pragma warning restore HLQ012
        {
            if (kp.IsReferenceType)
            {
                diagnostics.Add(Microsoft.CodeAnalysis.Diagnostic.Create(
                    CacheDiagnostics.ReferenceTypeKeyParameter,
                    firstLoc,
                    kp.Name,
                    method.Name));
            }
        }
    }

    private static void AddCachedMethod(
        IMethodSymbol method,
        CacheConfig effectiveConfig,
        string paramList,
        string argList,
        string nonCtArgList,
        string keyArgs,
        bool hasCt,
        string? ctParamName,
        System.Collections.Generic.List<KeyParam> keyParams,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods)
    {
        string innerReturnFqn;
        if (method.ReturnType is INamedTypeSymbol namedReturn
            && namedReturn.IsGenericType
            && namedReturn.TypeArguments.Length == 1)
        {
            innerReturnFqn = namedReturn.TypeArguments[0]
                .ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else
        {
            innerReturnFqn = method.ReturnType
                .ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        }

        cachedMethods.Add(new CachedMethodModel(
            method.Name,
            method.ReturnType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat),
            innerReturnFqn,
            paramList,
            argList,
            nonCtArgList,
            keyArgs,
            hasCt,
            ctParamName,
            System.Collections.Immutable.ImmutableArray.CreateRange(keyParams),
            effectiveConfig
        ));
    }

    private static AttributeData? FindCacheAttr(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (string.Equals(attr.AttributeClass?.ToDisplayString(), "ZeroAlloc.Cache.CacheAttribute", System.StringComparison.Ordinal))
                return attr;
        }
        return null;
    }

    private static CacheConfig? ReadConfig(AttributeData attr)
    {
        int ttlMs = 0;
        bool sliding = false;
        int maxEntries = 0;
        bool useHybridCache = false;

        foreach (var kv in attr.NamedArguments)
        {
            switch (kv.Key)
            {
                case "TtlMs":          ttlMs = (int)kv.Value.Value!;          break;
                case "Sliding":        sliding = (bool)kv.Value.Value!;        break;
                case "MaxEntries":     maxEntries = (int)kv.Value.Value!;      break;
                case "UseHybridCache": useHybridCache = (bool)kv.Value.Value!; break;
            }
        }

        return new CacheConfig(ttlMs, sliding, maxEntries, useHybridCache);
    }
}
