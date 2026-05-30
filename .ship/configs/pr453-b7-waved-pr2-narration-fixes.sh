#!/bin/bash
# Per-PR config — B7 Wave D PR-2 final follow-up: the two voice loose ends.
#  1. Cosmetic narration glitch in HandleExplainMakeBuyDecisionAsync — LowerFirst
#     mangled ALL-CAPS hard-gate reasons and RationaleText doubled the reason.
#  2. 3rd Codex P2 (return null on a rejected explicit item/part ref instead of
#     falling through to the bare-integer scan) — was uncommitted, now shipped.
BRANCH="fix/b7.wd-2-makebuy-voice-narration"
TITLE="fix(b7): make-buy voice narration de-dupe + null-guard (PR #452 follow-up)"
COMMIT_MSG=".ship/msgs/pr453-commit.txt"
PR_BODY=".ship/msgs/pr453-body.md"
FILES=(
  "Endpoints/VoiceInvokeEndpoint.cs"
  "Services/Voice/IntentClassifier.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  ".ship/configs/pr453-b7-waved-pr2-narration-fixes.sh"
)
