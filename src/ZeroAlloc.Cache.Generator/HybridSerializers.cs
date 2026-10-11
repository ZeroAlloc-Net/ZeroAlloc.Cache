using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// ZC0011. HybridCache serializes every value it stores, also without a distributed cache. Its
/// default serializer handles <c>string</c> and <c>byte[]</c> itself and sends every other type
/// to reflection-based System.Text.Json, which throws under NativeAOT and trimming on the first
/// miss. The consumer fixes that by registering an <c>IHybridCacheSerializer&lt;T&gt;</c>, so the
/// warning is reported only while the compilation has none for the type.
/// </summary>
internal static class HybridSerializers
{
    private const string HybridNamespace = "Microsoft.Extensions.Caching.Hybrid";
    private const string HybridAssembly = "Microsoft.Extensions.Caching.Hybrid";
    private const string AbstractionsAssembly = "Microsoft.Extensions.Caching.Abstractions";

    // FullyQualifiedFormat drops a reference type's '?' and keeps Nullable<T> as 'T?', which is
    // how HybridCache sees the type: GetOrCreateAsync<string?> and <string> are one T, <int?> is not <int>.
    private static readonly SymbolDisplayFormat KeyFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>The MSBuild properties under which reflection-based serialization fails or is not safe.</summary>
    private static readonly string[] AotProperties =
    {
        "build_property.PublishAot",
        "build_property.IsAotCompatible",
        "build_property.PublishTrimmed",
    };

    public static bool IsAotOrTrimmed(AnalyzerConfigOptions options)
    {
        foreach (var property in AotProperties)
        {
            if (options.TryGetValue(property, out var value)
                && string.Equals(value?.Trim(), "true", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>The use to check, or null when the value type needs no serializer.</summary>
    public static HybridValueUse? UseOf(string methodName, ITypeSymbol valueType, LocationInfo? location)
    {
        if (valueType.SpecialType == SpecialType.System_String) return null;
        if (valueType is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte }) return null;

        var type = valueType.IsReferenceType ? valueType.WithNullableAnnotation(NullableAnnotation.NotAnnotated) : valueType;
        return new HybridValueUse(methodName, type.ToDisplayString(), type.ToDisplayString(KeyFormat), location);
    }

    /// <summary>The uses of the interfaces a proxy is generated for.</summary>
    public static EquatableArray<HybridValueUse> Uses(ImmutableArray<CacheModel> models)
    {
        var uses = ImmutableArray.CreateBuilder<HybridValueUse>();
        foreach (var model in models)
        {
            if (model.IsGenerated) uses.AddRange(model.HybridValueUses);
        }
        return new EquatableArray<HybridValueUse>(uses.ToImmutable());
    }

    /// <summary>
    /// The serializers a concrete class or struct implements. A serializer factory is trusted to
    /// cover every type: which types it serves is decided at run time.
    /// </summary>
    public static HybridSerializerSet ImplementedBy(INamedTypeSymbol type)
    {
        if (type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            return HybridSerializerSet.Empty;

        List<string>? keys = null;
        var any = false;
        foreach (var iface in type.AllInterfaces)
        {
            if (!string.Equals(iface.ContainingNamespace?.ToDisplayString(), HybridNamespace, System.StringComparison.Ordinal))
                continue;

            if (string.Equals(iface.MetadataName, "IHybridCacheSerializerFactory", System.StringComparison.Ordinal))
            {
                any = true;
            }
            else if (string.Equals(iface.MetadataName, "IHybridCacheSerializer`1", System.StringComparison.Ordinal))
            {
                // An open generic serializer says nothing about which types it is registered for;
                // a class that closes it over a type, or the closed type itself, does.
                var argument = iface.TypeArguments[0];
                if (argument is not ITypeParameterSymbol)
                    (keys ??= new List<string>()).Add(argument.ToDisplayString(KeyFormat));
            }
        }

        return keys is null && !any
            ? HybridSerializerSet.Empty
            : new HybridSerializerSet(keys is null ? ImmutableArray<string>.Empty : ImmutableArray.CreateRange(keys), any);
    }

    /// <summary>
    /// The public serializers in referenced assemblies, such as a shared infrastructure project.
    /// Only assemblies that reference the HybridCache abstractions can declare one. The HybridCache
    /// assembly itself is skipped: its own factory is the reflection-based default.
    /// </summary>
    public static HybridSerializerSet InReferences(Compilation compilation, System.Threading.CancellationToken ct)
    {
        var found = new List<HybridSerializerSet>();
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(assembly.Name, HybridAssembly, System.StringComparison.Ordinal)
                || !ReferencesAbstractions(assembly))
                continue;

            CollectPublic(assembly.GlobalNamespace, found, ct);
        }
        return HybridSerializerSet.Merge(found);
    }

    /// <summary>A ZC0011 warning for each use the serializers do not cover.</summary>
    public static EquatableArray<DiagnosticInfo> Report(EquatableArray<HybridValueUse> uses, HybridSerializerSet serializers)
    {
        if (uses.Items.IsEmpty || serializers.CoversAnyType)
            return EquatableArray<DiagnosticInfo>.Empty;

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        foreach (var use in uses.Items)
        {
            if (serializers.TypeKeys.Contains(use.TypeKey, System.StringComparer.Ordinal))
                continue;

            diagnostics.Add(DiagnosticInfo.Create(
                CacheDiagnostics.HybridValueNeedsSerializer, use.Location, use.MethodName, use.DisplayType));
        }
        return new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable());
    }

    private static bool ReferencesAbstractions(IAssemblySymbol assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var identity in module.ReferencedAssemblies)
            {
                if (string.Equals(identity.Name, AbstractionsAssembly, System.StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    private static void CollectPublic(INamespaceOrTypeSymbol container, List<HybridSerializerSet> found, System.Threading.CancellationToken ct)
    {
        foreach (var member in container.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is INamespaceSymbol ns)
            {
                CollectPublic(ns, found, ct);
            }
            else if (member is INamedTypeSymbol { DeclaredAccessibility: Accessibility.Public } type)
            {
                var set = ImplementedBy(type);
                if (!set.IsEmpty) found.Add(set);
                CollectPublic(type, found, ct);
            }
        }
    }
}
