#!/bin/bash
# Sprint 13.5 PRA-5c — ApPostingService DEF-008 dual-write.
# First write-site wired post-PRA-5b foundation. PRA-5d through PRA-5j follow
# (one posting service per PR). PRA-5k de-static-izes JournalGenerator.

BRANCH="sprint-13.5/pra-5c-ap-posting-dual-write"
TITLE="feat(masters): PRA-5c ApPostingService AccountingKeyId dual-write"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5c-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5c-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5c-comment.md"

FILES=(
  "Services/AccountsPayable/ApPostingService.cs"
  "tests/Abs.FixedAssets.Tests/ApPostingServiceTests.cs"
  ".ship/configs/pra5c.sh"
)
