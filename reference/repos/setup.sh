#!/bin/bash
# reference/repos/setup.sh — shallow-clone the high-touch external deps used
# as agent context for the grep-before-code workflow.
#
# Run from the repo root:
#   bash reference/repos/setup.sh
#
# Idempotent — if a clone already exists, fetches latest and resets to the
# pinned tag/branch. Safe to re-run after bumping our package versions.
#
# AUTHORITY:
#   - reference/repos/README.md (the "why" + "what's in here")
#   - docs/engineering/code-structure-cleanup-skill.md (the sister skill)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "==== reference/repos/setup.sh ===="
echo "Cloning high-touch external deps into $SCRIPT_DIR"
echo "Disk impact: ~1-2 GB total (shallow clones)"
echo ""

# Pinned to release tags matching our deployed version. Bump as needed.
# Format: clone_or_update <local_dir> <repo_url> <ref>

clone_or_update() {
  local dir="$1"
  local url="$2"
  local ref="$3"

  if [ -d "$dir/.git" ]; then
    echo "==> $dir exists — fetching + resetting to $ref"
    (cd "$dir" && git fetch --depth 1 origin "$ref" --quiet && git reset --hard FETCH_HEAD)
  else
    echo "==> Cloning $url @ $ref into $dir"
    mkdir -p "$(dirname "$dir")"
    if ! git clone --depth 1 --branch "$ref" "$url" "$dir" 2>&1; then
      echo "    WARN: branch '$ref' not found — falling back to default branch"
      git clone --depth 1 "$url" "$dir"
    fi
  fi
  echo ""
}

# =============================================================================
# Pinned to stable refs that match our deployed package versions
# (Codex P2 review catch on PR #323 — tracking `main` makes reference
# context non-reproducible and can mislead debugging when upstream diverges
# from prod). Bump these when bumping the corresponding NuGet packages.
#
# Current prod deps (from Abs.FixedAssets.csproj):
#   Microsoft.EntityFrameworkCore.* @ 9.0.0           → efcore: release/9.0
#   Npgsql.EntityFrameworkCore.PostgreSQL @ 9.0.4     → efcore.pg: v9.0.4
#   .NET 9 SDK (TargetFramework=net9.0)               → sdk: release/9.0.3xx (current latest 9.x feature band)
# =============================================================================

# .NET SDK — MSBuild internals, web SDK content rules, publish targets.
# release/9.0.3xx tracks the current .NET 9 SDK feature band (9.0.3xx).
# Bump to .4xx / .5xx as later feature bands ship.
clone_or_update "dotnet/sdk" \
  "https://github.com/dotnet/sdk.git" \
  "release/9.0.3xx"

# EF Core — migrations, model snapshots, query translation, FluentAPI semantics.
# release/9.0 is the canonical .NET 9 line (single branch — no .Nxx feature
# bands like SDK).
clone_or_update "dotnet/efcore" \
  "https://github.com/dotnet/efcore.git" \
  "release/9.0"

# Npgsql EF Core provider — partial UNIQUE quirks, JSONB mapping, prod-validator
# quoting issues. Pinned to the tag matching our deployed version
# (Npgsql.EntityFrameworkCore.PostgreSQL @ 9.0.4 per Abs.FixedAssets.csproj).
clone_or_update "npgsql/Npgsql.EntityFrameworkCore.PostgreSQL" \
  "https://github.com/npgsql/efcore.pg.git" \
  "v9.0.4"

echo "==== Done ===="
echo ""
echo "Reference clones available at:"
echo "  $SCRIPT_DIR/dotnet/sdk"
echo "  $SCRIPT_DIR/dotnet/efcore"
echo "  $SCRIPT_DIR/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL"
echo ""
echo "Grep usage:"
echo "  grep -rn 'MSB3030' $SCRIPT_DIR/dotnet/sdk"
echo "  grep -rn 'IModelSnapshot' $SCRIPT_DIR/dotnet/efcore"
echo "  grep -rn 'jsonb' $SCRIPT_DIR/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL"
