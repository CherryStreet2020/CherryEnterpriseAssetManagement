#!/bin/bash
# Sprint 13.5 PRA-5f — CapitalImprovementPostingService DEF-008 dual-write.
# Fourth write-site wired. First ship to consume the shared
# GlPostingHelpers.ResolveAccountAndKeyAsync extension from PRA-5e.1.

BRANCH="sprint-13.5/pra-5f-cap-impr-dual-write"
TITLE="feat(masters): PRA-5f CapitalImprovementPostingService AccountingKeyId dual-write"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5f-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5f-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5f-comment.md"

FILES=(
  "Services/CapitalImprovementPostingService.cs"
  ".ship/configs/pra5f.sh"
)
