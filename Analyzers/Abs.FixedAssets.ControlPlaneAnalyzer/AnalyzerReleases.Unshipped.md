; Unshipped analyzer release.
; See https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category     | Severity | Notes
---------|--------------|----------|------------------------------------------------------------------
CHERRY025 | ControlPlane | Warning  | Class injects AppDbContext directly. See docs/ADR-025-service-layer-standard.md.
