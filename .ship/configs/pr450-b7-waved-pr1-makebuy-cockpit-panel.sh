#!/bin/bash
# Per-PR config for Theme B7 Wave D PR-1 — Make-or-Buy panel in the Production Cockpit.

BRANCH="feat/b7.wd-1-makebuy-cockpit-panel"
TITLE="B7 Wave D PR-1 — Make-or-Buy panel in the Production Cockpit"
COMMIT_MSG=".ship/msgs/pr450-commit.txt"
PR_BODY=".ship/msgs/pr450-body.md"

FILES=(
  "Pages/Production/MakeBuyCockpitPanelModel.cs"
  "Pages/Production/_CockpitMakeBuyPanel.cshtml"
  "Pages/Production/Cockpit.cshtml.cs"
  "Pages/Production/Cockpit.cshtml"
  "Pages/Admin/MakeBuyCockpitPanelProbe.cshtml"
  "Pages/Admin/MakeBuyCockpitPanelProbe.cshtml.cs"
  ".ship/configs/pr450-b7-waved-pr1-makebuy-cockpit-panel.sh"
)
