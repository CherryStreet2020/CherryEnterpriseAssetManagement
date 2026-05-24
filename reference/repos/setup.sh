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

# .NET SDK — MSBuild internals, web SDK content rules, publish targets.
# Track main (release/* branches are point-version specific and shift).
clone_or_update "dotnet/sdk" \
  "https://github.com/dotnet/sdk.git" \
  "main"

# EF Core — migrations, model snapshots, query translation, FluentAPI semantics.
# Track main; we read latest source for the conceptual understanding.
clone_or_update "dotnet/efcore" \
  "https://github.com/dotnet/efcore.git" \
  "main"

# Npgsql EF Core provider — partial UNIQUE quirks, JSONB mapping, prod-validator
# quoting issues. Track main; small enough that drift is fine.
clone_or_update "npgsql/Npgsql.EntityFrameworkCore.PostgreSQL" \
  "https://github.com/npgsql/efcore.pg.git" \
  "main"

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
