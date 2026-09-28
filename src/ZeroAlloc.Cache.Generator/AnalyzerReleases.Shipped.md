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
