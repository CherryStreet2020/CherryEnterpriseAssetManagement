#!/bin/bash
# ADR-025 — Service Layer Standard CI gate (LEGACY).
#
# ⚠️ DEPRECATED 2026-05-22 (Sprint 12.9 PR #1, commit history search "CHERRY025").
#
# The canonical gate is now the Roslyn analyzer `CHERRY025` at
# `Analyzers/Abs.FixedAssets.ControlPlaneAnalyzer/`. The new gate catches
# direct AppDbContext injection on:
#   - PageModels (this script's original scope)
#   - Controllers
#   - MinimalAPI Endpoints
#   - BackgroundServices / IHostedService classes
#
# Run the canonical gate locally:
#   dotnet build Abs.FixedAssets.csproj -c Release /warnaserror:CHERRY025
#
# This script is kept on disk for backward-compatible local convenience —
# greps only the Pages layer, only NEW files. It will be removed in a
# follow-up PR once nothing references it.
#
# Original detection logic preserved below for reference.
#
# Usage:
#   bash scripts/check-control-plane.sh [base-ref]
#
# Default base-ref is origin/main. The gate exits 0 (pass) when no
# violations are detected, 1 (fail) otherwise.
set -euo pipefail

echo "⚠️  scripts/check-control-plane.sh is DEPRECATED."
echo "   Run \`dotnet build Abs.FixedAssets.csproj -c Release /warnaserror:CHERRY025\` instead."
echo "   See docs/ADR-025-roslyn-analyzer-design.md."
echo ""

BASE_REF="${1:-origin/main}"

# Discover newly-added .cshtml.cs files under Pages/ in this PR.
# --diff-filter=A restricts to ADDED files (not modified — that's the
# grandfather clause).
NEW_PAGES=$(git diff --name-only --diff-filter=A "$BASE_REF"...HEAD -- 'Pages/**/*.cshtml.cs' 2>/dev/null || true)

if [ -z "$NEW_PAGES" ]; then
  echo "control-plane gate: no new PageModels in this PR. PASS."
  exit 0
fi

echo "control-plane gate: checking $(echo "$NEW_PAGES" | wc -l | tr -d ' ') newly-added PageModel(s)..."

VIOLATIONS=()
for f in $NEW_PAGES; do
  # Skip if file no longer exists (defensive)
  [ -f "$f" ] || continue

  # Check for AppDbContext injection patterns. Both common forms:
  #   private readonly AppDbContext _db;
  #   private readonly AppDbContext _context;
  # plus the constructor parameter form for completeness.
  if grep -qE '(AppDbContext\s+_?(db|context|ctx)|AppDbContext\s+\w+\s*[,)])' "$f"; then
    # Check for the explicit allow-comment opt-out
    if grep -qE 'PRAGMA:\s*control-plane-exempt' "$f"; then
      echo "  [exempt] $f — has // PRAGMA: control-plane-exempt"
      continue
    fi
    VIOLATIONS+=("$f")
  fi
done

if [ "${#VIOLATIONS[@]}" -eq 0 ]; then
  echo "control-plane gate: PASS (no violations)."
  exit 0
fi

echo ""
echo "❌ control-plane gate: FAIL"
echo ""
echo "The following NEW PageModels inject AppDbContext directly:"
for v in "${VIOLATIONS[@]}"; do
  echo "  • $v"
done
echo ""
echo "Per ADR-025 (Service Layer Standard), new PageModels must inject"
echo "a domain Service (IFooService), not AppDbContext, for any code"
echo "path that mutates data."
echo ""
echo "Remediation options:"
echo "  1. Refactor the page to inject IFooService instead of AppDbContext."
echo "     If no Service exists for this domain yet, create one."
echo ""
echo "  2. If this is a legitimate read-only admin CRUD page with no"
echo "     business logic (lookup tables, simple admin lists, etc.),"
echo "     add this comment near the top of the file:"
echo ""
echo "         // PRAGMA: control-plane-exempt"
echo ""
echo "     Code review will police that this pragma is used sparingly."
echo ""
echo "See: docs/ADR-025-service-layer-standard.md"
echo "Top-10 refactor backlog (legacy pages) in MASTER_PLAN.md Priority 1.61."
exit 1
