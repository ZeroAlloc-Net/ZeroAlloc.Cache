namespace ZeroAlloc.Cache.Generator;

internal sealed record PassthroughMethodModel(
    string Name,
    string ReturnTypeFqn,
    string ParameterList,
    string ArgumentList
);
