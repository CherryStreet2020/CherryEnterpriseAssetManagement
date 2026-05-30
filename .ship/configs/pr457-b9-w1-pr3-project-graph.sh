#!/bin/bash
# Per-PR config — B9 Wave 1 PR-3 (CLOSES B9 Wave 1): project lifecycle graph
# (Quote → Project → WBS/Phase → Job → Purchasing → Receipt → Cost → Billing →
# Acceptance) + ShowProjectGraph voice intent. No schema change.
BRANCH="feat/b9.w1-3-project-graph"
TITLE="feat(b9): project lifecycle graph + ShowProjectGraph voice — CLOSES B9 W1"
COMMIT_MSG=".ship/msgs/pr457-commit.txt"
PR_BODY=".ship/msgs/pr457-body.md"
FILES=(
  "Services/Projects/IProjectGraphService.cs"
  "Services/Projects/ProjectGraphService.cs"
  "Pages/CustomerProjects/Graph.cshtml"
  "Pages/CustomerProjects/Graph.cshtml.cs"
  "Pages/Shared/Partials/_ProjectGraph.cshtml"
  "Pages/CustomerProjects/CommandCenter.cshtml"
  "Pages/CustomerProjects/Details.cshtml"
  "Program.cs"
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/IntentPrototypes.cs"
  "Services/Voice/HybridIntentRouter.cs"
  "Endpoints/VoiceInvokeEndpoint.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  "tests/Abs.FixedAssets.Tests/ProjectGraphServiceTests.cs"
  ".ship/configs/pr457-b9-w1-pr3-project-graph.sh"
)
