#!/bin/bash
# validate-docs-change.sh
# Lightweight documentation freshness check
# 
# Purpose: Ensures documentation is updated when code changes occur
# Usage: Run in CI pipeline (CI=true) to enforce, otherwise advisory only
#
# Exit codes:
#   0 - Pass (docs updated or not required)
#   1 - Fail (code changed without docs update) - only in CI mode

set -e

# Directories that require docs updates when changed
WATCHED_DIRS=(
    "Services/Seeding/"
    "Services/Testing/"
    "Services/Webhooks/"
    "Services/Integrations/"
    "wwwroot/css/"
    "wwwroot/js/"
    "Pages/Shared/"
    "Pages/Admin/"
    "Models/"
)

# Check if running in CI mode
CI_MODE="${CI:-false}"

echo "=============================================="
echo "  Documentation Freshness Check"
echo "=============================================="
echo "CI Mode: $CI_MODE"
echo "Date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo ""

# Get list of changed files (uncommitted + last commit)
CHANGED_FILES=$(git diff --name-only HEAD~1 HEAD 2>/dev/null || git diff --name-only 2>/dev/null || echo "")

if [ -z "$CHANGED_FILES" ]; then
    echo "No changed files detected."
    echo "RESULT: PASS (no changes)"
    exit 0
fi

echo "Changed files (up to 20):"
echo "$CHANGED_FILES" | head -20
TOTAL_CHANGED=$(echo "$CHANGED_FILES" | wc -l)
if [ "$TOTAL_CHANGED" -gt 20 ]; then
    echo "... and $((TOTAL_CHANGED - 20)) more"
fi
echo ""

# Check if any watched directories have changes
WATCHED_CHANGED=false
WATCHED_MATCHES=()
for dir in "${WATCHED_DIRS[@]}"; do
    if echo "$CHANGED_FILES" | grep -q "^$dir"; then
        WATCHED_MATCHES+=("$dir")
        WATCHED_CHANGED=true
    fi
done

if [ "$WATCHED_CHANGED" = false ]; then
    echo "No changes in watched directories."
    echo "RESULT: PASS (no watched changes)"
    exit 0
fi

echo "Changes detected in watched directories:"
for match in "${WATCHED_MATCHES[@]}"; do
    echo "  - $match"
done
echo ""

# Check if docs were also updated
DOCS_CHANGED=$(echo "$CHANGED_FILES" | grep -c "^docs/" || true)

if [ "$DOCS_CHANGED" -gt 0 ]; then
    echo "Documentation updated: $DOCS_CHANGED file(s) in docs/"
    echo "RESULT: PASS"
    exit 0
fi

# No docs updated
echo "WARNING: Code changed in watched directories but no docs updated!"
echo ""
echo "Suggested actions:"
echo "  - Update docs/README.md if adding new features"
echo "  - Create docs/adr/ADR-XXX.md for architectural changes"
echo "  - Update docs/TestingAndSmokeSuite.md for test changes"
echo "  - Update docs/SeedingAndDemoData.md for seed changes"
echo "  - Update docs/UXStandards.md for UI pattern changes"
echo "  - Update docs/OperationsRunbook.md for operational changes"
echo ""

if [ "$CI_MODE" = "true" ]; then
    echo "=============================================="
    echo "  RESULT: FAIL (CI enforcement enabled)"
    echo "=============================================="
    echo "Code changes in watched directories require documentation updates."
    exit 1
else
    echo "=============================================="
    echo "  RESULT: ADVISORY (local development)"
    echo "=============================================="
    echo "Consider updating documentation before committing."
    exit 0
fi
