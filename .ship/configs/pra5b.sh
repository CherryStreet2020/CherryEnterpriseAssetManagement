#!/bin/bash
# Sprint 13.5 PRA-5b — AccountingKey + IGlAccountResolver.ResolveAccountingKeyAsync
# Master Files Baseline cascade ship #10 of 10 (foundation only — write-site
# fan-out splits into PRA-5c through PRA-5j follow-up PRs per the
# zero-Codex-catches discipline).

BRANCH="sprint-13.5/pra-5b-accounting-key"
TITLE="feat(masters): PRA-5b AccountingKey + segment-key resolver"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5b-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5b-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5b-comment.md"

FILES=(
  "Models/Masters/AccountingKey.cs"
  "Models/JournalLine.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524250000_AddAccountingKeyPRA5b.cs"
  "Services/IGlAccountResolver.cs"
  "tests/Abs.FixedAssets.Tests/GlAccountResolverTests.cs"
  ".ship/configs/pra5b.sh"
)
