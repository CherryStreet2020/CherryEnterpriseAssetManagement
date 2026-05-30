#!/bin/bash
# Per-PR config — B9 Wave 5 PR-12: financials / the margin engine (OPENS Wave 5).
# ProjectBudget / ProjectBudgetLine / ProjectActualCost / ProjectForecast /
# ProjectEACSnapshot + IProjectFinancialsService (live Contract−EAC margin,
# committed wired from PR-10 commitments). SCHEMA CHANGE (5 new tables).
BRANCH="feat/b9.w5-12-financials-spine"
TITLE="feat(b9): financials / margin engine — budget/actual/forecast/EAC + live Contract-EAC margin (B9 W5 PR-12)"
COMMIT_MSG=".ship/msgs/pr466-commit.txt"
PR_BODY=".ship/msgs/pr466-body.md"
FILES=(
  "Models/Projects/ProjectFinancials.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530220051_B9Wave5Pr12FinancialsSpine.cs"
  "Migrations/20260530220051_B9Wave5Pr12FinancialsSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectFinancialsService.cs"
  "Services/Projects/ProjectFinancialsService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectFinancialsServiceTests.cs"
  ".ship/configs/pr466-b9-w5-pr12-financials-spine.sh"
)
