using System.Runtime.CompilerServices;
using VerifyTests;

namespace ZeroAlloc.Cache.Generator.Tests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
