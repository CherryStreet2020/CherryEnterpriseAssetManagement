#!/bin/bash
# Per-PR config — B9 Wave 6 PR-17: quality + acceptance.
# ProjectInspection/NCR/MRB/PunchItem/Acceptance + IProjectQualityService with the
# §22.4 acceptance gate (no accept while open blocking NCR / pending MRB / blocking
# punch); a RevenueTrigger acceptance flips the PR-14 billing AcceptanceConfirmed
# (the real entity the #468 placeholder flag stood in for). SCHEMA (5 new tables).
BRANCH="feat/b9.w6-17-quality"
TITLE="feat(b9): quality + acceptance — inspection/NCR/MRB/punch/acceptance + §22.4 gate (PR-17)"
COMMIT_MSG=".ship/msgs/pr471-commit.txt"
PR_BODY=".ship/msgs/pr471-body.md"
FILES=(
  "Models/Projects/ProjectQuality.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260531093429_B9Wave6Pr17Quality.cs"
  "Migrations/20260531093429_B9Wave6Pr17Quality.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectQualityService.cs"
  "Services/Projects/ProjectQualityService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectQualityServiceTests.cs"
  ".ship/configs/pr471-b9-w6-pr17-quality.sh"
)
