; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|--------------------------------------------------
ZC0001  | ZeroAlloc.Cache | Warning  | Sliding expiration not supported with HybridCache
ZC0002  | ZeroAlloc.Cache | Warning  | Cache key parameter is a reference type
ZC0003  | ZeroAlloc.Cache | Error    | HybridCache not available
ZC0004  | ZeroAlloc.Cache | Warning  | Mixed MaxEntries values

## Release 1.1.43

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|-----------------------------------------------
ZC0005  | ZeroAlloc.Cache | Warning  | [Cache] ignored — return type cannot be cached

## Release 1.1.53

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|------------------------------------------------------------------
ZC0006  | ZeroAlloc.Cache | Warning  | Containing type of a [Cache] interface is not partial
ZC0007  | ZeroAlloc.Cache | Warning  | Generic [Cache] interface is not generated
ZC0008  | ZeroAlloc.Cache | Warning  | [Cache] interface is not accessible from its namespace
ZC0009  | ZeroAlloc.Cache | Error    | File-local [Cache] interface is not generated
ZC0010  | ZeroAlloc.Cache | Error    | Interface name differs only in case from another [Cache] interface
