#!/bin/bash
# Pre-Push Verification Script
# Optional local helper that runs CI verification before pushing
#
# Usage:
#   ./tools/pre-push-verify.sh           # Run in local mode (warnings only)
#   CI=true ./tools/pre-push-verify.sh   # Run in strict CI mode (fails on issues)
#
# Git Hook Installation (optional):
#   cp tools/pre-push-verify.sh .git/hooks/pre-push
#   chmod +x .git/hooks/pre-push
#
# Last updated: 2026-01-24

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║          CherryAI EAM - Pre-Push Verification              ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if CI mode
if [ "$CI" = "true" ]; then
    echo -e "${YELLOW}Running in CI mode (strict)${NC}"
else
    echo -e "${GREEN}Running in local mode (warnings only)${NC}"
    echo -e "Set CI=true for strict enforcement"
fi
echo ""

# Run the main CI verification script
if [ -f "$SCRIPT_DIR/ci-verify.sh" ]; then
    echo -e "${BLUE}► Running CI verification...${NC}"
    echo ""
    
    if bash "$SCRIPT_DIR/ci-verify.sh"; then
        echo ""
        echo -e "${GREEN}✓ All checks passed!${NC}"
        echo ""
        echo -e "${GREEN}╔════════════════════════════════════════════════════════════╗${NC}"
        echo -e "${GREEN}║                    READY TO PUSH                           ║${NC}"
        echo -e "${GREEN}╚════════════════════════════════════════════════════════════╝${NC}"
        exit 0
    else
        echo ""
        echo -e "${RED}✗ Verification failed${NC}"
        
        if [ "$CI" = "true" ]; then
            echo -e "${RED}Push blocked in CI mode. Fix issues before pushing.${NC}"
            exit 1
        else
            echo -e "${YELLOW}⚠ Issues found. Consider fixing before pushing.${NC}"
            echo ""
            echo -e "To enforce strict mode: ${BLUE}CI=true ./tools/pre-push-verify.sh${NC}"
            exit 0
        fi
    fi
else
    echo -e "${RED}Error: ci-verify.sh not found${NC}"
    echo "Expected at: $SCRIPT_DIR/ci-verify.sh"
    exit 1
fi
