#!/bin/bash
# Per-PR config — B9 Wave 3 PR-9 (CLOSES Wave 3): project Gantt + critical path.
# NO schema change — CPM read service + Gantt page/SVG partial + voice + demo seed.
BRANCH="feat/b9.w3-9-gantt-critical-path"
TITLE="feat(b9): project Gantt + critical path (CPM) — CLOSES B9 Wave 3"
COMMIT_MSG=".ship/msgs/pr463-commit.txt"
PR_BODY=".ship/msgs/pr463-body.md"
FILES=(
  "Services/Projects/IProjectScheduleService.cs"
  "Services/Projects/ProjectScheduleService.cs"
  "Pages/CustomerProjects/Gantt.cshtml"
  "Pages/CustomerProjects/Gantt.cshtml.cs"
  "Pages/Shared/Partials/_ProjectGantt.cshtml"
  "Pages/CustomerProjects/CommandCenter.cshtml"
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/IntentPrototypes.cs"
  "Services/Voice/HybridIntentRouter.cs"
  "Endpoints/VoiceInvokeEndpoint.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "tests/Abs.FixedAssets.Tests/ProjectScheduleServiceTests.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  ".ship/configs/pr463-b9-w3-pr9-gantt.sh"
)
