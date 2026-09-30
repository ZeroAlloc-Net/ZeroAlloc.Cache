; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|------------------------------------------------------------------
ZC0006  | ZeroAlloc.Cache | Warning  | Containing type of a [Cache] interface is not partial
ZC0007  | ZeroAlloc.Cache | Warning  | Generic [Cache] interface is not generated
ZC0008  | ZeroAlloc.Cache | Warning  | [Cache] interface is not accessible from its namespace
ZC0009  | ZeroAlloc.Cache | Error    | File-local [Cache] interface is not generated
ZC0010  | ZeroAlloc.Cache | Error    | Interface name differs only in case from another [Cache] interface
