#!/bin/bash
# Per-PR config for Theme B7 Wave D PR-2 — Cherry Bar ExplainMakeBuyDecision voice intent.

BRANCH="feat/b7.wd-2-makebuy-voice-intent"
TITLE="B7 Wave D PR-2 — Cherry Bar ExplainMakeBuyDecision voice intent"
COMMIT_MSG=".ship/msgs/pr451-commit.txt"
PR_BODY=".ship/msgs/pr451-body.md"

FILES=(
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/IntentPrototypes.cs"
  "Services/Production/IMakeBuyDecisionService.cs"
  "Services/Production/MakeBuyDecisionService.cs"
  "Endpoints/VoiceInvokeEndpoint.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  ".ship/configs/pr451-b7-waved-pr2-makebuy-voice.sh"
)
