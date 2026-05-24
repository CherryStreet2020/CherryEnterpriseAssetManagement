#!/bin/bash
# Sprint 13.5 PRA-5a — COA additive expansion (Master Files Baseline ship #2)

BRANCH="sprint-13.5/pra-5a-coa-additive"
TITLE="feat(coa): PRA-5a manufacturing/inventory/variance COA expansion (additive)"

COMMIT_MSG="$SCRIPT_DIR/msgs/pra5a-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5a-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5a-comment.md"

FILES=(
  "Models/GlAccount.cs"
  "Migrations/20260524170000_AddCoaManufacturingCategoriesPRA5a.cs"
  ".ship/configs/pra5a.sh"
)
