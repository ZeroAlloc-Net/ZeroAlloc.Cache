; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category        | Severity | Notes
--------|-----------------|----------|----------------------------------------------------
ZC0011  | ZeroAlloc.Cache | Warning  | HybridCache value type needs an AOT-safe serializer
