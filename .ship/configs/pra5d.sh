#!/bin/bash
# Sprint 13.5 PRA-5d — ReceivingPostingService DEF-008 dual-write.
# Second write-site wired post-PRA-5b foundation. Same pattern as PRA-5c.

BRANCH="sprint-13.5/pra-5d-receiving-posting-dual-write"
TITLE="feat(masters): PRA-5d ReceivingPostingService AccountingKeyId dual-write"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5d-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5d-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5d-comment.md"

FILES=(
  "Services/Receiving/ReceivingPostingService.cs"
  "tests/Abs.FixedAssets.Tests/ReceivingPostingServiceTests.cs"
  ".ship/configs/pra5d.sh"
)
