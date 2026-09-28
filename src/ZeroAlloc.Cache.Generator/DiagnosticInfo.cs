using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Cache.Generator;

/// <summary>
/// A diagnostic found while building the model, reported when the proxy is emitted.
/// </summary>
/// <remarks>
/// A <see cref="Diagnostic"/> compares its message arguments by reference, so one rebuilt from
/// the same source never compares equal to the last. The model used to compare its diagnostics by
/// count instead, which let a model whose diagnostic had moved compare equal, and the driver then
/// replayed the diagnostic of the earlier run. Here the arguments are strings compared by value,
/// and the location is a <see cref="LocationInfo"/>.
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    ImmutableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params object[] messageArgs)
    {
        var args = ImmutableArray.CreateBuilder<string>(messageArgs.Length);
        foreach (var arg in messageArgs)
            args.Add(System.Convert.ToString(arg, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

        return new DiagnosticInfo(descriptor, location, args.MoveToImmutable());
    }

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Length];
        for (var i = 0; i < args.Length; i++)
            args[i] = MessageArgs[i];

        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, args);
    }

    public bool Equals(DiagnosticInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!Descriptor.Equals(other.Descriptor) || !Equals(Location, other.Location)) return false;
        if (MessageArgs.Length != other.MessageArgs.Length) return false;
        for (var i = 0; i < MessageArgs.Length; i++)
        {
            if (!string.Equals(MessageArgs[i], other.MessageArgs[i], System.StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var h = Descriptor.GetHashCode();
            h = (h * 397) ^ (Location?.GetHashCode() ?? 0);
            return (h * 397) ^ MessageArgs.Length;
        }
    }
}
