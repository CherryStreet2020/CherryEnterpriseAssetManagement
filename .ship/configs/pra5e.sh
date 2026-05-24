#!/bin/bash
# Sprint 13.5 PRA-5e — CipCapitalizationService DEF-008 dual-write.
# Third write-site wired. Follow-up cleanup PR extracts the helper to
# Services/Posting/GlPostingHelpers and refactors Ap/Receiving/Cip to consume.

BRANCH="sprint-13.5/pra-5e-cip-capitalization-dual-write"
TITLE="feat(masters): PRA-5e CipCapitalizationService AccountingKeyId dual-write"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5e-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5e-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5e-comment.md"

FILES=(
  "Services/Cip/CipCapitalizationService.cs"
  "tests/Abs.FixedAssets.Tests/CipCapitalizationHardeningTests.cs"
  ".ship/configs/pra5e.sh"
)
