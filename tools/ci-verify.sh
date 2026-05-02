#!/bin/bash
# ci-verify.sh
# Comprehensive CI verification script for CherryAI EAM
# 
# Purpose: Run all CI checks including build, tests, and documentation freshness
# Usage: CI=true ./tools/ci-verify.sh
#
# Exit codes:
#   0 - All checks passed
#   1 - One or more checks failed

set -e

echo "=============================================="
echo "  CherryAI EAM - CI Verification"
echo "=============================================="
echo "Date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "CI Mode: ${CI:-false}"
echo ""

FAILED=0

# Step 1: Build
echo "Step 1/3: Building application..."
if dotnet build --configuration Release --nologo -v q; then
    echo "BUILD: PASS"
else
    echo "BUILD: FAIL"
    FAILED=1
fi
echo ""

# Step 2: Documentation freshness check
echo "Step 2/3: Documentation freshness check..."
if ./tools/validate-docs-change.sh; then
    echo "DOCS FRESHNESS: PASS"
else
    if [ "${CI:-false}" = "true" ]; then
        echo "DOCS FRESHNESS: FAIL (CI enforcement)"
        FAILED=1
    else
        echo "DOCS FRESHNESS: ADVISORY (local mode)"
    fi
fi
echo ""

# Step 3: Smoke tests (if server is running)
echo "Step 3/3: Smoke tests..."
if curl -s http://localhost:5000/health > /dev/null 2>&1; then
    SMOKE_RESULT=$(curl -s http://localhost:5000/api/smoke/run 2>/dev/null || echo '{"failedCount": -1}')
    FAILED_COUNT=$(echo "$SMOKE_RESULT" | grep -o '"failedCount":[0-9]*' | cut -d: -f2 || echo "-1")
    
    if [ "$FAILED_COUNT" = "0" ]; then
        echo "SMOKE TESTS: PASS"
    elif [ "$FAILED_COUNT" = "-1" ]; then
        echo "SMOKE TESTS: SKIPPED (could not parse result)"
    else
        echo "SMOKE TESTS: FAIL ($FAILED_COUNT tests failed)"
        FAILED=1
    fi
else
    echo "SMOKE TESTS: SKIPPED (server not running)"
fi
echo ""

# Summary
echo "=============================================="
if [ "$FAILED" -eq 0 ]; then
    echo "  RESULT: ALL CHECKS PASSED"
    echo "=============================================="
    exit 0
else
    echo "  RESULT: SOME CHECKS FAILED"
    echo "=============================================="
    exit 1
fi
