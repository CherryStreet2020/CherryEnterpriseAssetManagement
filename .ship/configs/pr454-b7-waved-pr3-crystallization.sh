#!/bin/bash
# Per-PR config — B7 Wave D PR-3: Crystallization cockpit panel + CrystallizeJobToStandard
# voice intent. CLOSES Theme B7. Code-only (service methods + DTOs + pages + voice + tests);
# no schema change (IItemCrystallizationService substrate shipped Wave B PR-5).
BRANCH="feat/b7.wd-3-crystallization-cockpit-voice"
TITLE="feat(b7): crystallization cockpit panel + CrystallizeJobToStandard voice — CLOSES B7"
COMMIT_MSG=".ship/msgs/pr454-commit.txt"
PR_BODY=".ship/msgs/pr454-body.md"
FILES=(
  "Services/Production/IItemCrystallizationService.cs"
  "Services/Production/ItemCrystallizationService.cs"
  "Services/Production/IProductionCockpitService.cs"
  "Services/Production/ProductionCockpitService.cs"
  "Pages/Production/CrystallizationCockpitPanelModel.cs"
  "Pages/Production/_CockpitCrystallizationPanel.cshtml"
  "Pages/Production/Cockpit.cshtml.cs"
  "Pages/Production/Cockpit.cshtml"
  "Pages/Admin/CrystallizationCockpitPanelProbe.cshtml"
  "Pages/Admin/CrystallizationCockpitPanelProbe.cshtml.cs"
  "Services/Voice/IntentClassifier.cs"
  "Services/Voice/IntentPrototypes.cs"
  "Endpoints/VoiceInvokeEndpoint.cs"
  "tests/Abs.FixedAssets.Tests/HybridIntentRouterTests.cs"
  ".ship/configs/pr454-b7-waved-pr3-crystallization.sh"
)
