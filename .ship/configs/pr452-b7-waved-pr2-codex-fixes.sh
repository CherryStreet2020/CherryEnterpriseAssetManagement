#!/bin/bash
# Per-PR config — B7 Wave D PR-2 follow-up: the two Codex P2 fixes that missed #451's merge.

BRANCH="fix/b7.wd-2-makebuy-voice-codex"
TITLE="fix(b7): make-buy voice ref guard + vector-win re-extract (PR #451 follow-up)"
COMMIT_MSG=".ship/msgs/pr452-commit.txt"
PR_BODY=".ship/msgs/pr452-body.md"
FILES=(
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/HybridIntentRouter.cs"
  ".ship/configs/pr452-b7-waved-pr2-codex-fixes.sh"
)
