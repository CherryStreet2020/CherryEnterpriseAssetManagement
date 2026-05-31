#!/bin/bash
# Per-PR config — B9 Wave 6 PR-18: service handoff + warranty + AI project review.
# CLOSES B9. ProjectServiceHandoff/ProjectWarranty + IProjectServiceService (handoff
# sign-off gate, warranty lifecycle, data-driven project review onto the AI-summary
# fields) + the equipment-project closeout gate in CustomerProjectService.UpdateStatusAsync.
# SCHEMA (2 new tables).
BRANCH="feat/b9.w6-18-service-warranty"
TITLE="feat(b9): service handoff + warranty + AI project review — CLOSES B9 (PR-18)"
COMMIT_MSG=".ship/msgs/pr472-commit.txt"
PR_BODY=".ship/msgs/pr472-body.md"
FILES=(
  "Models/Projects/ProjectService.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260531095943_B9Wave6Pr18ServiceWarranty.cs"
  "Migrations/20260531095943_B9Wave6Pr18ServiceWarranty.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectServiceService.cs"
  "Services/Projects/ProjectServiceService.cs"
  "Services/Projects/CustomerProjectService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectServiceServiceTests.cs"
  ".ship/configs/pr472-b9-w6-pr18-service-warranty.sh"
)
