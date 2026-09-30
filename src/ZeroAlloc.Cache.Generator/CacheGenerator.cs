using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Cache.Generator;

[Generator]
public sealed class CacheGenerator : IIncrementalGenerator
{
    // Every type name the proxy emits must keep a nullable reference annotation: without it a
    // ValueTask<string?> member is implemented as ValueTask<string>, CS8613 and CS8603 in a
    // consumer with nullable enabled. FullyQualifiedFormat alone drops the '?'.
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>The tracking name of the step that builds each interface's model.</summary>
    internal const string ModelsTrackingName = "CacheModels";

    /// <summary>The tracking name of the step that finds case-only hint-name collisions.</summary>
    internal const string CaseCollisionsTrackingName = "CacheCaseCollisions";

    /// <summary>The tracking name of the step that finds interfaces whose names must be qualified.</summary>
    internal const string NameCollisionsTrackingName = "CacheNameCollisions";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateInterface(node),
                transform: static (ctx, ct) => TryParse(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .WithTrackingName(ModelsTrackingName);

        // Interfaces whose hint names differ only in case from an earlier one's: Roslyn compares
        // hint names ignoring case, so only the first can be added. See #197.
        var collisions = models
            .Collect()
            .Select(static (all, _) => CaseCollisions.Find(all))
            .WithTrackingName(CaseCollisionsTrackingName);

        context.RegisterSourceOutput(collisions, static (ctx, found) =>
        {
            foreach (var d in found.Diagnostics)
                ctx.ReportDiagnostic(d.ToDiagnostic());
        });

        var skippedHintNames = collisions.Select(static (found, _) => found.SkippedHintNames);

        // Interfaces that would share cache keys or an Add...Cache method with another get
        // qualified names; every other interface keeps its own. See #199. The result holds only
        // hint names, so it stays equal, and every output cached, unless a name changes.
        var nameCollisions = models
            .Collect()
            .Combine(skippedHintNames)
            .Select(static (pair, _) => NameCollisions.Find(pair.Left, pair.Right))
            .WithTrackingName(NameCollisionsTrackingName);

        context.RegisterSourceOutput(models.Combine(skippedHintNames).Combine(nameCollisions), static (ctx, pair) =>
        {
            var ((model, skipped), names) = pair;
            foreach (var d in model.Diagnostics)
                ctx.ReportDiagnostic(d.ToDiagnostic());

            if (model.IsGenerated && !skipped.Contains(model.HintName, System.StringComparer.Ordinal))
                CacheWriter.Write(ctx, names.Apply(model));
        });
    }

    /// <summary>
    /// Cheap syntax-only shortlist: an interface carrying an attribute on itself or on one of its
    /// members. Whether that attribute is actually <c>[Cache]</c> is decided semantically in
    /// <see cref="TryParse"/>.
    /// </summary>
    private static bool IsCandidateInterface(SyntaxNode node)
    {
        if (node is not InterfaceDeclarationSyntax iface)
            return false;
        if (iface.AttributeLists.Count > 0)
            return true;

        foreach (var member in iface.Members)
        {
            if (member.AttributeLists.Count > 0)
                return true;
        }
        return false;
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

        // A partial interface is visited once per declaration. Only its first candidate
        // declaration builds the model, or the same file would be added twice (CS8785).
        if (!IsFirstCandidateDeclaration(ctx.Node, symbol, ct))
            return null;

        var ifaceAttr = FindCacheAttr(symbol);
        var ifaceConfig = ifaceAttr != null ? ReadConfig(ifaceAttr) : null;

        // Opt-in gate: the syntax predicate only knows "interface with some attribute", so an
        // interface carrying unrelated attributes still lands here. Emit nothing unless [Cache]
        // is actually present on the interface or on one of its methods.
        if (ifaceAttr == null && !HasMethodLevelCacheAttr(symbol, ct))
            return null;

        var cachedMethods = new System.Collections.Generic.List<CachedMethodModel>();
        var passthroughMethods = new System.Collections.Generic.List<PassthroughMethodModel>();
        var diagnostics = new System.Collections.Generic.List<DiagnosticInfo>();

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

        // An interface no proxy can be generated for carries only the diagnostic that says why.
        var unsupported = ContainingTypes.Check(symbol, ct);
        if (unsupported is not null)
            return Unsupported(symbol, ns, unsupported);

        return BuildModel(ctx, symbol, ns, cachedMethods, passthroughMethods, diagnostics);
    }

    private static CacheModel BuildModel(
        GeneratorSyntaxContext ctx,
        INamedTypeSymbol symbol,
        string? ns,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods,
        System.Collections.Generic.List<PassthroughMethodModel> passthroughMethods,
        System.Collections.Generic.List<DiagnosticInfo> diagnostics)
    {
        var containers = ContainingTypes.Of(symbol);
        // The fully qualified name without its global:: prefix, with keywords escaped.
        var ifaceFqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Substring("global::".Length);

        var isolatedCacheMaxEntries = FirstMaxEntries(cachedMethods);

        CheckMixedMaxEntries(symbol, cachedMethods, isolatedCacheMaxEntries, diagnostics);
        CheckHybridCacheAvailability(ctx, symbol, cachedMethods, diagnostics);

        return new CacheModel(
            ns,
            symbol.Name,
            ifaceFqn,
            HintNames.ForHost(symbol),
            symbol.ToDisplayString(),
            LocationInfo.FirstDeclaration(symbol),
            ContainingTypes.Headers(containers),
            containers.Count == 0
                ? string.Empty
                : symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".",
            KeyName(symbol, containers),
            ExtensionMethodName(symbol, containers),
            QualifiedKeyName(symbol, containers),
            QualifiedExtensionMethodName(symbol, containers),
            IsPubliclyAccessible(symbol),
            cachedMethods.Exists(static m => m.EffectiveConfig.UseHybridCache),
            cachedMethods.Exists(static m => !m.EffectiveConfig.UseHybridCache && m.EffectiveConfig.MaxEntries == 0),
            cachedMethods.Exists(static m => m.UsesBoundedCache),
            isolatedCacheMaxEntries,
            IsSpanKeyLookupAvailable(ctx.SemanticModel.Compilation, ctx.Node.SyntaxTree.Options),
            System.Collections.Immutable.ImmutableArray.CreateRange(cachedMethods),
            System.Collections.Immutable.ImmutableArray.CreateRange(passthroughMethods),
            System.Collections.Immutable.ImmutableArray.CreateRange(diagnostics)
        );
    }

    private static CacheModel Unsupported(INamedTypeSymbol symbol, string? ns, DiagnosticInfo diagnostic) =>
        new(
            ns,
            symbol.Name,
            string.Empty,
            HintNames.ForHost(symbol),
            symbol.ToDisplayString(),
            LocationInfo.FirstDeclaration(symbol),
            System.Collections.Immutable.ImmutableArray<string>.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            false,
            false,
            0,
            false,
            System.Collections.Immutable.ImmutableArray<CachedMethodModel>.Empty,
            System.Collections.Immutable.ImmutableArray<PassthroughMethodModel>.Empty,
            System.Collections.Immutable.ImmutableArray.Create(diagnostic));

    /// <summary>
    /// True when <paramref name="node"/> is the first declaration of <paramref name="symbol"/>
    /// that the syntax predicate picks, so a partial interface builds one model.
    /// </summary>
    private static bool IsFirstCandidateDeclaration(
        SyntaxNode node, INamedTypeSymbol symbol, System.Threading.CancellationToken ct)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(ct);
            if (!IsCandidateInterface(declaration)) continue;
            return declaration.SyntaxTree == node.SyntaxTree && declaration.Span == node.Span;
        }
        return false;
    }

    /// <summary>
    /// The interface's name in cache keys and in the <c>cache.method</c> telemetry tag. At the top
    /// of a namespace it is the interface's name, as it always was. A nested interface is prefixed
    /// with its containing types, <c>Outer.IFoo</c>, so two same-named nested interfaces never
    /// read each other's entries from the shared cache.
    /// </summary>
    private static string KeyName(
        INamedTypeSymbol symbol, System.Collections.Generic.IReadOnlyList<INamedTypeSymbol> containers)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < containers.Count; i++)
            sb.Append(containers[i].Name).Append('.');
        return sb.Append(symbol.Name).ToString();
    }

    /// <summary>
    /// The name of the <c>Add...Cache</c> extension method: <c>AddFooCache</c> for <c>IFoo</c>. The
    /// method sits in a class at namespace level, so a nested interface is prefixed with its
    /// containing types, joined with underscores as the ZeroAlloc.EventSourcing registry names are:
    /// <c>AddOuter_FooCache</c> for <c>Outer.IFoo</c>.
    /// </summary>
    private static string ExtensionMethodName(
        INamedTypeSymbol symbol, System.Collections.Generic.IReadOnlyList<INamedTypeSymbol> containers)
    {
        var sb = new System.Text.StringBuilder("Add");
        for (var i = 0; i < containers.Count; i++)
            sb.Append(containers[i].Name).Append('_');
        return sb.Append(StripInterfacePrefix(symbol.Name)).Append("Cache").ToString();
    }

    /// <summary>
    /// The key name for an interface whose key another interface shares: <see cref="KeyName"/>
    /// after the namespace, <c>M1.IX</c> for <c>M1.IX</c>. In the global namespace it is the key
    /// name itself. See #199.
    /// </summary>
    private static string QualifiedKeyName(
        INamedTypeSymbol symbol, System.Collections.Generic.IReadOnlyList<INamedTypeSymbol> containers)
    {
        var sb = new System.Text.StringBuilder();
        AppendNamespace(sb, symbol.ContainingNamespace, '.');
        return sb.Append(KeyName(symbol, containers)).ToString();
    }

    /// <summary>
    /// The method name for an interface whose <c>Add...Cache</c> method another interface of its
    /// namespace shares: the namespace, the containing types and the whole interface name, joined
    /// with underscores, so <c>N.IFoo</c> and <c>N.Foo</c> get <c>AddN_IFooCache</c> and
    /// <c>AddN_FooCache</c>. See #199.
    /// </summary>
    private static string QualifiedExtensionMethodName(
        INamedTypeSymbol symbol, System.Collections.Generic.IReadOnlyList<INamedTypeSymbol> containers)
    {
        var sb = new System.Text.StringBuilder("Add");
        AppendNamespace(sb, symbol.ContainingNamespace, '_');
        for (var i = 0; i < containers.Count; i++)
            sb.Append(containers[i].Name).Append('_');
        return sb.Append(symbol.Name).Append("Cache").ToString();
    }

    // The namespace's names, outermost first, each followed by the separator.
    private static void AppendNamespace(System.Text.StringBuilder sb, INamespaceSymbol? ns, char separator)
    {
        if (ns is null || ns.IsGlobalNamespace) return;
        AppendNamespace(sb, ns.ContainingNamespace, separator);
        sb.Append(ns.Name).Append(separator);
    }

    private static string StripInterfacePrefix(string name)
    {
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }

    /// <summary>
    /// All bounded methods share one MemoryCache whose SizeLimit is the first bounded method's
    /// MaxEntries. Hybrid methods are skipped: they never use that cache.
    /// </summary>
    private static int FirstMaxEntries(System.Collections.Generic.List<CachedMethodModel> cachedMethods)
    {
        for (int i = 0; i < cachedMethods.Count; i++)
        {
            if (cachedMethods[i].UsesBoundedCache)
                return cachedMethods[i].EffectiveConfig.MaxEntries;
        }
        return 0;
    }

    private static bool HasMethodLevelCacheAttr(
        INamedTypeSymbol symbol,
        System.Threading.CancellationToken ct)
    {
        foreach (var member in symbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                && FindCacheAttr(method) != null)
                return true;
        }
        return false;
    }

    private static void ParseMember(
        ISymbol member,
        CacheConfig? ifaceConfig,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods,
        System.Collections.Generic.List<PassthroughMethodModel> passthroughMethods,
        System.Collections.Generic.List<DiagnosticInfo> diagnostics)
    {
        if (member is not IMethodSymbol method)
            return;
        if (method.MethodKind != MethodKind.Ordinary)
            return;

        var methodAttr = FindCacheAttr(method);
        var methodConfig = methodAttr != null ? ReadConfig(methodAttr) : null;
        var effectiveConfig = methodConfig ?? ifaceConfig;

        BuildParamStrings(method, out var paramList, out var argList,
            out var keyArgs, out var hasCt, out var ctParamName, out var keyParams);

        // A method is passthrough if there is no effective config, OR if it does not return
        // Task<T> or ValueTask<T>, the only shapes the emitted async cache path can await and store.
        // Checking "any generic type" let synchronous List<T>, int? or IAsyncEnumerable<T> through,
        // and the generated proxy then failed to compile with CS1983.
        bool isUncacheableReturn = !IsCacheableReturnType(method.ReturnType);
        bool isPassthrough = effectiveConfig == null || isUncacheableReturn;

        if (isPassthrough)
        {
            // An explicit [Cache] that cannot be honoured must say so. Silently emitting a
            // passthrough left the author believing caching was active while the inner method
            // ran on every call (#121).
            if (methodAttr != null && isUncacheableReturn)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    CacheDiagnostics.CacheAttributeIgnored,
                    LocationInfo.From(methodAttr) ?? LocationInfo.From(method),
                    method.Name,
                    method.ReturnType.ToDisplayString()));
            }

            AddPassthrough(method, passthroughMethods, paramList, argList);
            return;
        }

        EmitDiagnostics(method, methodAttr, effectiveConfig!, keyParams, diagnostics);
        AddCachedMethod(method, effectiveConfig!, paramList, argList, keyArgs,
            hasCt, ctParamName, keyParams, cachedMethods);
    }

    private static bool IsCacheableReturnType(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named)
            return false;

        var definition = named.OriginalDefinition;
        return string.Equals(definition.ContainingNamespace?.ToDisplayString(), "System.Threading.Tasks", System.StringComparison.Ordinal)
            && (string.Equals(definition.MetadataName, "Task`1", System.StringComparison.Ordinal)
                || string.Equals(definition.MetadataName, "ValueTask`1", System.StringComparison.Ordinal));
    }

    private static void BuildParamStrings(
        IMethodSymbol method,
        out string paramList,
        out string argList,
        out string keyArgs,
        out bool hasCt,
        out string? ctParamName,
        out System.Collections.Generic.List<KeyParam> keyParams)
    {
        var paramSb = new System.Text.StringBuilder();
        var argSb = new System.Text.StringBuilder();
        var keySb = new System.Text.StringBuilder();
        hasCt = false;
        ctParamName = null;
        keyParams = new System.Collections.Generic.List<KeyParam>();
        bool firstParam = true;

        foreach (var param in method.Parameters)
        {
            if (!firstParam) { paramSb.Append(", "); argSb.Append(", "); }
            firstParam = false;

            var fqn = param.Type.ToDisplayString(TypeFormat);
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
                keySb.Append(":{").Append(param.Name).Append('}');

                bool isRef = param.Type.IsReferenceType
                    && param.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.None;
                keyParams.Add(new KeyParam(param.Name, isRef, fqn));
            }
        }

        paramList = paramSb.ToString();
        argList = argSb.ToString();
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
            method.ReturnType.ToDisplayString(TypeFormat),
            paramList,
            argList
        ));
    }

    private static void EmitDiagnostics(
        IMethodSymbol method,
        AttributeData? methodAttr,
        CacheConfig effectiveConfig,
        System.Collections.Generic.List<KeyParam> keyParams,
        System.Collections.Generic.List<DiagnosticInfo> diagnostics)
    {
        // ZC0001 is about the settings, so it points at the method's own [Cache] when that set
        // them. Settings from the interface's [Cache] apply to every method, and the warning names
        // one method, so it points at that method.
        if (effectiveConfig.Sliding && effectiveConfig.UseHybridCache)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                CacheDiagnostics.SlidingNotSupportedOnHybridCache,
                (methodAttr is null ? null : LocationInfo.From(methodAttr)) ?? LocationInfo.From(method),
                method.Name));
        }

#pragma warning disable HLQ012 // CollectionsMarshal.AsSpan not available on netstandard2.0
        foreach (var kp in keyParams)
#pragma warning restore HLQ012
        {
            if (kp.IsReferenceType)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    CacheDiagnostics.ReferenceTypeKeyParameter,
                    ParameterLocation(method, kp.Name),
                    kp.Name,
                    method.Name));
            }
        }
    }

    /// <summary>The parameter's identifier, which ZC0002 is about; the method if it is not found.</summary>
    private static LocationInfo? ParameterLocation(IMethodSymbol method, string name)
    {
        foreach (var parameter in method.Parameters)
        {
            if (string.Equals(parameter.Name, name, System.StringComparison.Ordinal))
                return LocationInfo.From(parameter) ?? LocationInfo.From(method);
        }
        return LocationInfo.From(method);
    }

    private static void AddCachedMethod(
        IMethodSymbol method,
        CacheConfig effectiveConfig,
        string paramList,
        string argList,
        string keyArgs,
        bool hasCt,
        string? ctParamName,
        System.Collections.Generic.List<KeyParam> keyParams,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods)
    {
        string innerReturnFqn;
        ITypeSymbol innerReturnSymbol;
        if (method.ReturnType is INamedTypeSymbol namedReturn
            && namedReturn.IsGenericType
            && namedReturn.TypeArguments.Length == 1)
        {
            innerReturnSymbol = namedReturn.TypeArguments[0];
        }
        else
        {
            innerReturnSymbol = method.ReturnType;
        }

        innerReturnFqn = innerReturnSymbol.ToDisplayString(TypeFormat);

        // The cache-hit path tests the stored object against T without its nullability, because
        // a type pattern cannot name a nullable type. For Nullable<U> that is U; for an annotated
        // reference type it is the type without the annotation. See CacheWriter and #182.
        bool innerIsValueType = innerReturnSymbol.IsValueType;
        bool innerIsNullableValueType =
            innerReturnSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        bool innerIsNullable =
            innerReturnSymbol.NullableAnnotation == NullableAnnotation.Annotated || innerIsNullableValueType;
        var innerUnderlyingSymbol = innerIsNullableValueType
            ? ((INamedTypeSymbol)innerReturnSymbol).TypeArguments[0]
            : innerReturnSymbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var innerUnderlyingFqn = innerUnderlyingSymbol
            .ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);

        cachedMethods.Add(new CachedMethodModel(
            method.Name,
            method.ReturnType.ToDisplayString(TypeFormat),
            innerReturnFqn,
            innerIsValueType,
            innerIsNullable,
            innerUnderlyingFqn,
            paramList,
            argList,
            keyArgs,
            hasCt,
            ctParamName,
            System.Collections.Immutable.ImmutableArray.CreateRange(keyParams),
            effectiveConfig
        ));
    }

    /// <summary>
    /// True when the type and every type containing it are public, so generated members that
    /// reference it may themselves be public. A nested public type inside an internal type is
    /// not publicly reachable, hence the walk up the containing chain.
    /// </summary>
    private static bool IsPubliclyAccessible(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
                return false;
        }
        return true;
    }

    private static void CheckMixedMaxEntries(
        INamedTypeSymbol symbol,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods,
        int firstMaxEntries,
        System.Collections.Generic.List<DiagnosticInfo> diagnostics)
    {
        if (firstMaxEntries <= 0) return;

        bool hasDifferent = false;
        for (int i = 0; i < cachedMethods.Count; i++)
        {
            if (!cachedMethods[i].UsesBoundedCache) continue;
            if (cachedMethods[i].EffectiveConfig.MaxEntries != firstMaxEntries) { hasDifferent = true; break; }
        }

        if (!hasDifferent) return;

        diagnostics.Add(DiagnosticInfo.Create(
            CacheDiagnostics.MixedMaxEntriesValues,
            LocationInfo.From(symbol),
            symbol.Name,
            firstMaxEntries));
    }

    private static void CheckHybridCacheAvailability(
        GeneratorSyntaxContext ctx,
        INamedTypeSymbol symbol,
        System.Collections.Generic.List<CachedMethodModel> cachedMethods,
        System.Collections.Generic.List<DiagnosticInfo> diagnostics)
    {
        if (!cachedMethods.Exists(static m => m.EffectiveConfig.UseHybridCache))
            return;

        // Probe for the AddHybridCache() extension the generated DI registration calls, not for the
        // HybridCache type. HybridCache itself lives in Microsoft.Extensions.Caching.Abstractions 9.0+,
        // which ZeroAlloc.Cache always brings in, so probing it never fired: a net8.0 consumer
        // without the Microsoft.Extensions.Caching.Hybrid package got CS1061 in generated code.
        var addHybridCacheType = ctx.SemanticModel.Compilation
            .GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.HybridCacheServiceExtensions");
        if (addHybridCacheType is not null)
            return;

        // Microsoft.Extensions.Caching.Hybrid not referenced — emit an error
        diagnostics.Add(DiagnosticInfo.Create(
            CacheDiagnostics.HybridCacheNotAvailable,
            LocationInfo.From(symbol)));
    }

    /// <summary>
    /// True when the generated hit path can look the key up without building a string: the
    /// referenced MemoryCache has the non-generic <c>TryGetValue(ReadOnlySpan&lt;char&gt;, out object?)</c>,
    /// Microsoft.Extensions.Caching.Memory 9.0+ on net9.0+, and the key can be formatted into a
    /// span with <c>MemoryExtensions.TryWrite</c>, which needs C# 10 interpolated string handlers.
    /// A net8.0 or netstandard2.0 consumer gets neither and keeps the string key lookup. See #185.
    /// </summary>
    private static bool IsSpanKeyLookupAvailable(Compilation compilation, ParseOptions options)
    {
        if (options is not Microsoft.CodeAnalysis.CSharp.CSharpParseOptions { LanguageVersion: >= Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp10 })
            return false;

        if (compilation.GetTypeByMetadataName("System.MemoryExtensions+TryWriteInterpolatedStringHandler") is null)
            return false;

        var memoryCache = compilation.GetTypeByMetadataName("Microsoft.Extensions.Caching.Memory.MemoryCache");
        if (memoryCache is null)
            return false;

        foreach (var member in memoryCache.GetMembers("TryGetValue"))
        {
            if (member is IMethodSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsGenericMethod: false, Parameters.Length: 2 } method
                && method.Parameters[0].Type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } span
                && string.Equals(span.OriginalDefinition.ToDisplayString(), "System.ReadOnlySpan<T>", System.StringComparison.Ordinal)
                && span.TypeArguments[0].SpecialType == SpecialType.System_Char
                && method.Parameters[1].RefKind == RefKind.Out
                && method.Parameters[1].Type.SpecialType == SpecialType.System_Object)
            {
                return true;
            }
        }
        return false;
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

        if (ttlMs <= 0) return null; // guard: TtlMs is required; malformed attribute data treated as no-cache

        return new CacheConfig(ttlMs, sliding, maxEntries, useHybridCache);
    }
}
