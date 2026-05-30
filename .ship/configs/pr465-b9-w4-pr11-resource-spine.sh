#!/bin/bash
# Per-PR config — B9 Wave 4 PR-11: resource/labor/expense spine.
# ProjectResourcePlan / ProjectResourceAssignment / ProjectTimeEntry / ProjectExpense
# + IProjectResourceService (planned-vs-actual rollups, closed-project capture
# guard, set-once approvals). SCHEMA CHANGE (4 new tables).
BRANCH="feat/b9.w4-11-resource-spine"
TITLE="feat(b9): resource/labor/expense spine — plan/assignment/time/expense + planned-vs-actual (B9 W4 PR-11)"
COMMIT_MSG=".ship/msgs/pr465-commit.txt"
PR_BODY=".ship/msgs/pr465-body.md"
FILES=(
  "Models/Projects/ProjectResource.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530213101_B9Wave4Pr11ResourceSpine.cs"
  "Migrations/20260530213101_B9Wave4Pr11ResourceSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectResourceService.cs"
  "Services/Projects/ProjectResourceService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectResourceServiceTests.cs"
  ".ship/configs/pr465-b9-w4-pr11-resource-spine.sh"
)
