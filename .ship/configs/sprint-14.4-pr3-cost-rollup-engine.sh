#!/bin/bash
# PR config — Sprint 14.4 PR-3: Cost Rollup Engine
# Graph builder + cycle detection + dual-mode rollup + exception detection

BRANCH="feat/14.4.3-cost-rollup-engine"
TITLE="Sprint 14.4 PR-3 — Cost Rollup Engine (graph builder + cycle detection + dual-mode Financial/Exploded rollup + exception detection)"
COMMIT_MSG=".ship/msgs/pr393-commit.txt"
PR_BODY=".ship/msgs/pr393-body.md"
SHIP_COMMENT=".ship/msgs/pr393-comment.md"

FILES=(
  "Models/Production/CostRollup.cs"
  "Services/Production/ICostRollupService.cs"
  "Services/Production/CostRollupService.cs"
  "Pages/Admin/CostRollupProbe.cshtml"
  "Pages/Admin/CostRollupProbe.cshtml.cs"
  "Data/AppDbContext.cs"
  "Program.cs"
  "Data/Migrations/20260527233430_AddCostRollupEngine.cs"
  "Data/Migrations/20260527233430_AddCostRollupEngine.Designer.cs"
  "Data/Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/sprint-14.4-pr3-cost-rollup-engine.sh"
  ".ship/msgs/pr393-commit.txt"
  ".ship/msgs/pr393-body.md"
)
