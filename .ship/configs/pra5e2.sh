#!/bin/bash
# Sprint 13.5 PRA-5e.2 — Seed system-default GlAccounts for IndustryDefaults.
# Fix for E2E-discovered visibility gap: PRA-5c/5d/5e shipped but
# AccountingKeyId stamped NULL on every new line because COA lacked rows.

BRANCH="sprint-13.5/pra-5e2-seed-system-glaccounts"
TITLE="fix(masters): PRA-5e.2 seed system GlAccounts for IndustryDefaults"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5e2-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5e2-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5e2-comment.md"

FILES=(
  "Migrations/20260524260000_SeedSystemGlAccountsForIndustryDefaultsPRA5e2.cs"
  ".ship/configs/pra5e2.sh"
)
