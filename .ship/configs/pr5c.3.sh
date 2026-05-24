#!/bin/bash
# Sprint 13.5 PR #5c.3 — Quarantine 2 hardcoded-seed migrations
# Surgical refactor: no schema changes, no prod data changes.

BRANCH="sprint-13.5/pr-5c.3-quarantine-hardcoded-seeds"
TITLE="refactor(seeds): quarantine 2 hardcoded-seed migrations (PR #5c.3)"

COMMIT_MSG="$SCRIPT_DIR/msgs/pr5c.3-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pr5c.3-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pr5c.3-comment.md"

FILES=(
  "Migrations/20260519_AddAdvancedShippingNotice.cs"
  "Migrations/20260519_SeedOrphanStockReceipts.cs"
  "seed/dev-demo/abs-machining-receiving.sql"
  "seed/dev-demo/README.md"
  ".ship/configs/pr5c.3.sh"
)
