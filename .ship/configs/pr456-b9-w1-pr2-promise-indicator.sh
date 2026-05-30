#!/bin/bash
# Per-PR config — B9 Wave 1 PR-2: "Can we still hit the promise?" indicator +
# command-center badge + ProjectPromiseStatus voice intent. No schema change.
BRANCH="feat/b9.w1-2-promise-indicator"
TITLE="feat(b9): can-we-hit-the-promise indicator + voice (B9 W1 PR-2)"
COMMIT_MSG=".ship/msgs/pr456-commit.txt"
PR_BODY=".ship/msgs/pr456-body.md"
FILES=(
  "Services/Projects/IProjectPromiseService.cs"
  "Services/Projects/ProjectPromiseService.cs"
  "Pages/CustomerProjects/CommandCenter.cshtml"
  "Pages/CustomerProjects/CommandCenter.cshtml.cs"
  "Program.cs"
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/IntentPrototypes.cs"
  "Endpoints/VoiceInvokeEndpoint.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  "tests/Abs.FixedAssets.Tests/ProjectPromiseServiceTests.cs"
  ".ship/configs/pr456-b9-w1-pr2-promise-indicator.sh"
)
