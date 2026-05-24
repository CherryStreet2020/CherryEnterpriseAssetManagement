#!/bin/bash
# Sprint 13.5 PRA-10 — TaxRateMaster effective-dated rate matrix
# Master Files Baseline cascade ship #8 of 10

BRANCH="sprint-13.5/pra-10-taxrate-master"
TITLE="feat(masters): PRA-10 TaxRateMaster (effective-dated rate matrix)"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra10-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra10-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra10-comment.md"

FILES=(
  "Models/Masters/TaxRateMaster.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524230000_AddTaxRateMasterPRA10.cs"
  ".ship/configs/pra10.sh"
)
