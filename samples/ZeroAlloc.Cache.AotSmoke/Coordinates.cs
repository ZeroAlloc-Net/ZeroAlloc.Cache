using System.Runtime.InteropServices;

namespace ZeroAlloc.Cache.AotSmoke;

[StructLayout(LayoutKind.Auto)]
public readonly record struct Coordinates(int X, int Y);
