#!/bin/bash
# Per-PR config — B9 Wave 3 PR-7 (OPENS Wave 3): WBS hardening on ProjectPhase.
# Owner / cost bucket / 100%-rule weighted roll-up + set-once baseline gate.
# SCHEMA CHANGE (additive ProjectPhases columns + 6 CHECKs + partial index + xmin).
BRANCH="feat/b9.w3-7-wbs-harden"
TITLE="feat(b9): WBS hardening on ProjectPhase — owner/cost bucket/100%-rule + set-once baseline (OPENS B9 W3)"
COMMIT_MSG=".ship/msgs/pr461-commit.txt"
PR_BODY=".ship/msgs/pr461-body.md"
FILES=(
  "Models/Projects/CustomerProjects.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530183500_B9Wave3Pr7WbsHardenProjectPhase.cs"
  "Migrations/20260530183500_B9Wave3Pr7WbsHardenProjectPhase.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectWbsService.cs"
  "Services/Projects/ProjectWbsService.cs"
  "Services/Projects/ICustomerProjectService.cs"
  "Services/Projects/CustomerProjectService.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectWbsServiceTests.cs"
  ".ship/configs/pr461-b9-w3-pr7-wbs-harden.sh"
)
