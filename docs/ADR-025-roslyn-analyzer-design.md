# ADR-025 Roslyn Analyzer — Design Memo (Sprint 12.9 PR #1)

**Status:** Drafted 2026-05-22 · Companion to PR #272
**Authors:** Claude · approved by Dean (Sprint 12.9 elevation 2026-05-22)
**Implements:** ADR-025 D4 upgrade — grep → Roslyn semantic analyzer

---

## Why this PR exists

ADR-025 D4 shipped a bash grep gate (`scripts/check-control-plane.sh`) that has three known gaps:

1. **Only sees `Pages/**/*.cshtml.cs`** — Controllers, MinimalAPI endpoints, BackgroundServices, ViewComponents bypass it entirely.
2. **Only flags newly-ADDED files** (`--diff-filter=A`) — a developer can add a 20-call `SaveChangesAsync` block to a modified existing PageModel without tripping the gate.
3. **No AST awareness** — grep can't distinguish a read-only `AppDbContext` field from a write-heavy one.

A baseline audit run on `528f0a8` shows:

| Layer | Files injecting `AppDbContext` today |
|---|---|
| `Pages/**/*.cshtml.cs` | 106 (matches ADR-025 audit) |
| `Controllers/**/*.cs` | 8 |
| `Endpoints/**/*.cs` | 2 |
| `Services/**/*HostedService.cs` (BackgroundService) | 3 |
| **Total** | **119 unique class files** |

The grep gate has zero visibility into the bottom 13 of those.

## Decision — Roslyn DiagnosticAnalyzer

We ship `Abs.FixedAssets.ControlPlaneAnalyzer` — a `Microsoft.CodeAnalysis.CSharp` analyzer that emits diagnostic **CHERRY025** on any class that:

1. Inherits one of `PageModel` · `ControllerBase` · `Controller` · `BackgroundService`, OR implements `IHostedService` directly, AND
2. Has a constructor parameter of type `Abs.FixedAssets.Data.AppDbContext`, AND
3. Is NOT exempt via any of:
   - `[ControlPlaneExempt]` attribute on the class
   - `// PRAGMA: control-plane-exempt` leading comment in the file (backward-compat with the bash gate)
   - File path listed in `Analyzers/ControlPlaneAllowlist.txt` (grandfathered legacy files)

**Severity:** `Warning` by default; CI invokes `dotnet build /warnaserror:CHERRY025` to promote to `Error`. Local dev gets a yellow squiggle without breaking the build.

**Category:** `ControlPlane`.

**Diagnostic message:**

> `{ClassKind}` '`{ClassName}`' injects `AppDbContext` directly. Per ADR-025 (Service Layer Standard), mutations must flow through a domain service (`IFooService`). Either refactor to inject the relevant service, OR mark the class with `[ControlPlaneExempt]` if this is a read-only admin/lookup surface. See `docs/ADR-025-service-layer-standard.md`.

## Why an allowlist (not git-diff)

Roslyn analyzers run inside the compiler — they cannot run `git diff`. To replicate the grep gate's "modified-existing is grandfathered" behavior, we snapshot **all 119 current offenders** into `Analyzers/ControlPlaneAllowlist.txt` and ship it with PR #272.

**Each subsequent refactor PR** in Sprint 12.9 (PRs #3, #4, #5 — WorkOrders, Purchasing, ItemEdit) removes its target file from the allowlist as part of the same diff. The allowlist's line count IS the audit-completeness metric (PR #6 wires this to CI output).

## Project layout

```
Analyzers/
  Abs.FixedAssets.ControlPlaneAnalyzer/
    Abs.FixedAssets.ControlPlaneAnalyzer.csproj   # netstandard2.0, references Microsoft.CodeAnalysis.CSharp 4.8.0
    ControlPlaneAnalyzer.cs                       # The DiagnosticAnalyzer
    ControlPlaneExemptAttribute.cs                # [ControlPlaneExempt] for opt-out
    AnalyzerReleases.Shipped.md
    AnalyzerReleases.Unshipped.md
  ControlPlaneAllowlist.txt                       # 119 grandfathered file paths, one per line
tests/
  Abs.FixedAssets.ControlPlaneAnalyzer.Tests/
    Abs.FixedAssets.ControlPlaneAnalyzer.Tests.csproj   # net9.0, xUnit + Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit
    ControlPlaneAnalyzerTests.cs                        # 8 test cases (see below)
```

`Abs.FixedAssets.csproj` references the analyzer via:

```xml
<ItemGroup>
  <ProjectReference Include="Analyzers\Abs.FixedAssets.ControlPlaneAnalyzer\Abs.FixedAssets.ControlPlaneAnalyzer.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
<ItemGroup>
  <AdditionalFiles Include="Analyzers\ControlPlaneAllowlist.txt" />
</ItemGroup>
```

This pattern is the .NET-canonical way to consume a same-repo analyzer.

## ControlPlaneExempt attribute

The new C# attribute (preferred over the bash-era PRAGMA comment) lives in the analyzer DLL itself so it can be applied without taking a dependency on the web app:

```csharp
namespace Abs.FixedAssets.ControlPlane;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ControlPlaneExemptAttribute : Attribute
{
    public string Reason { get; }
    public ControlPlaneExemptAttribute(string reason) => Reason = reason;
}
```

The reason string is required — code review can grep for `[ControlPlaneExempt("...")]` and audit reasons.

## Test cases (all 8 land in PR #272)

| # | Scenario | Expected |
|---|---|---|
| 1 | `PageModel` + `AppDbContext` ctor param | CHERRY025 fires |
| 2 | `ControllerBase` + `AppDbContext` ctor param | CHERRY025 fires |
| 3 | `BackgroundService` + `AppDbContext` ctor param | CHERRY025 fires |
| 4 | `PageModel` + only `IFooService` ctor param | No diagnostic |
| 5 | `PageModel` + `AppDbContext` + `[ControlPlaneExempt("lookup admin")]` | No diagnostic |
| 6 | `PageModel` + `AppDbContext` + leading `// PRAGMA: control-plane-exempt` | No diagnostic |
| 7 | Plain class (not Page/Controller/Endpoint/BgSvc) + `AppDbContext` | No diagnostic (services are OK) |
| 8 | File path matches allowlist entry | No diagnostic |

## CI workflow change

`.github/workflows/control-plane.yml` becomes:

```yaml
name: control-plane  # branch-protection name preserved

on:
  pull_request:
    branches: [main]
    paths:
      - 'Pages/**/*.cshtml.cs'
      - 'Controllers/**/*.cs'
      - 'Endpoints/**/*.cs'
      - 'Services/**/*HostedService.cs'
      - 'Analyzers/**'
      - '.github/workflows/control-plane.yml'
  workflow_dispatch:

jobs:
  control-plane-gate:
    runs-on: ubuntu-latest
    timeout-minutes: 8
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x
      - name: Restore
        run: dotnet restore Abs.FixedAssets.csproj --nologo
      - name: Build with CHERRY025 escalated to error
        run: dotnet build Abs.FixedAssets.csproj -c Release /warnaserror:CHERRY025 --no-restore --nologo
```

The bash script stays on disk (with a deprecation comment) for local dev convenience — but `dotnet build /warnaserror:CHERRY025` is now the canonical local check too.

## Backward compatibility

- **PRAGMA comment** still works (existing pattern preserved).
- **Bash script** still works locally (no-op deprecation marker added).
- **Job name** `control-plane-gate` preserved → no branch protection updates needed.
- **Allowlist** ships with all 119 current offenders → main is green on first run.

## What this PR does NOT do (queued for later)

- Detect actual `SaveChangesAsync` / `Add` / `Update` / `Remove` calls. (v2 — once the allowlist is populated, deepening the check is incremental.)
- Refactor any legacy page. (Sprint 12.9 PRs #3, #4, #5 — explicitly scoped.)
- `IPostingService<TSourceDoc>` contract. (Sprint 12.9 PR #2.)
- Audit-completeness CI metric. (Sprint 12.9 PR #6 — reads allowlist line count.)
- RLS tenant-leak test. (Sprint 12.9 PR #7.)

## Acceptance for merge

(a) `dotnet build Abs.FixedAssets.csproj -c Release /warnaserror:CHERRY025` exits 0 on this PR's HEAD.
(b) `dotnet test tests/Abs.FixedAssets.ControlPlaneAnalyzer.Tests/` exits 0 with all 8 tests passing.
(c) CI `control-plane-gate` job is green.
(d) `a11y-audit` and `build` workflows remain green (no main-app breakage from analyzer pickup).
(e) Live verification: pull on Replit, restart via Agent in Build mode, `/healthz` + `/readyz` + `/Account/Login` all 200.

---

## Cross-references

- `docs/ADR-025-service-layer-standard.md` — the standard this PR enforces (ADR-025 D4 upgrade)
- `MASTER_PLAN.md` Priority 1.6080 — Sprint 12.9 sprint-level entry
- Memory `project_sprint_12_9_control_plane_hardening_locked.md` — sprint lock decision (Dean 2026-05-22)
- Memory `project_sprint_12_9_pr1_kickoff.md` — this PR's kickoff note
