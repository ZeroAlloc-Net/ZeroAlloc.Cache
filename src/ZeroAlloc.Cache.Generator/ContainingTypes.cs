using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// The types a nested <c>[Cache]</c> interface is declared in. Its proxy and bounded-cache holder
/// are generated inside partial declarations of those types, so same-named interfaces nested in
/// different types get distinct proxies. The helpers port ZeroAlloc.Mapping's <c>HostDeclarations</c>.
/// </summary>
internal static class ContainingTypes
{
    /// <summary>
    /// The containing types of <paramref name="symbol"/>, outermost first. Empty for an interface
    /// at the top of its namespace.
    /// </summary>
    public static IReadOnlyList<INamedTypeSymbol> Of(INamedTypeSymbol symbol)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var t = symbol.ContainingType; t is not null; t = t.ContainingType) chain.Add(t);
        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// The partial declaration headers of <paramref name="containers"/>, for example
    /// <c>partial record struct Orders</c>. Each carries the kind; accessibility and constraints are
    /// left out, which partial parts allow. No container is generic: that is ZC0007.
    /// </summary>
    public static ImmutableArray<string> Headers(IReadOnlyList<INamedTypeSymbol> containers)
    {
        var headers = ImmutableArray.CreateBuilder<string>(containers.Count);
        for (var i = 0; i < containers.Count; i++)
        {
            var type = containers[i];
            var sb = new StringBuilder();
            if (type.IsRefLikeType) sb.Append("ref ");
            sb.Append("partial ").Append(Keyword(type)).Append(' ').Append(Identifier(type.Name));
            headers.Add(sb.ToString());
        }
        return headers.MoveToImmutable();
    }

    /// <summary>
    /// Why no proxy can be generated for <paramref name="symbol"/>, or null when one can: ZC0009
    /// when it is file-local, ZC0007 when it is generic, ZC0006 when a containing type is not
    /// partial, and ZC0008 when the namespace-level extension method cannot see it.
    /// </summary>
    public static DiagnosticInfo? Check(INamedTypeSymbol symbol, CancellationToken ct)
    {
        var location = LocationInfo.From(symbol);
        var name = symbol.ToDisplayString();

        if (IsFileLocalOrNestedInOne(symbol))
            return DiagnosticInfo.Create(CacheDiagnostics.FileLocalInterface, location, name);

        // IsGenericType, not Arity: it is also true for a type nested in a generic type.
        if (symbol.IsGenericType)
            return DiagnosticInfo.Create(CacheDiagnostics.GenericInterface, location, name);

        var nonPartial = FirstNonPartialContainingType(symbol, ct);
        if (nonPartial is not null)
        {
            return DiagnosticInfo.Create(
                CacheDiagnostics.ContainingTypeNotPartial, location, name, nonPartial.ToDisplayString());
        }

        if (!IsAccessibleFromNamespace(symbol))
            return DiagnosticInfo.Create(CacheDiagnostics.InterfaceNotAccessible, location, name);

        return null;
    }

    // Only a top-level type can be declared file, but everything nested in it is file-local too.
    private static bool IsFileLocalOrNestedInOne(INamedTypeSymbol symbol)
    {
        for (var t = symbol; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return true;
        }
        return false;
    }

    /// <summary>
    /// The outermost containing type that is not declared <c>partial</c>, or null when all of
    /// them are.
    /// </summary>
    private static INamedTypeSymbol? FirstNonPartialContainingType(INamedTypeSymbol symbol, CancellationToken ct)
    {
        INamedTypeSymbol? outermost = null;
        for (var t = symbol.ContainingType; t is not null; t = t.ContainingType)
        {
            if (!IsPartial(t, ct)) outermost = t;
        }
        return outermost;
    }

    /// <summary>
    /// True when code elsewhere in the assembly can name the interface: neither it nor a
    /// containing type is private, protected or private protected. Protected internal is
    /// accessible to the whole assembly.
    /// </summary>
    private static bool IsAccessibleFromNamespace(INamedTypeSymbol symbol)
    {
        for (var t = symbol; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                return false;
        }
        return true;
    }

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken ct) =>
        type.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax(ct) is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));

    private static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
