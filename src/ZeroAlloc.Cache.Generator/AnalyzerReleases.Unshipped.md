; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category    | New Severity | Old Category    | Old Severity | Notes
--------|-----------------|--------------|-----------------|--------------|--------------------------------------------------------
ZC0006  | ZeroAlloc.Cache | Error        | ZeroAlloc.Cache | Warning      | Containing type of a [Cache] interface is not partial
ZC0007  | ZeroAlloc.Cache | Error        | ZeroAlloc.Cache | Warning      | Generic [Cache] interface is not generated
ZC0008  | ZeroAlloc.Cache | Error        | ZeroAlloc.Cache | Warning      | [Cache] interface is not accessible from its namespace
