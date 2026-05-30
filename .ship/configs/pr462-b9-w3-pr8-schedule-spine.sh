#!/bin/bash
# Per-PR config — B9 Wave 3 PR-8: schedule spine (milestones / tasks / deps).
# Achieve / complete / cycle gates. SCHEMA CHANGE (3 new tables).
BRANCH="feat/b9.w3-8-schedule-spine"
TITLE="feat(b9): schedule spine — milestones/tasks/dependencies + achieve/complete/cycle gates (B9 W3 PR-8)"
COMMIT_MSG=".ship/msgs/pr462-commit.txt"
PR_BODY=".ship/msgs/pr462-body.md"
FILES=(
  "Models/Projects/ProjectSchedule.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530191226_B9Wave3Pr8ScheduleSpine.cs"
  "Migrations/20260530191226_B9Wave3Pr8ScheduleSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectScheduleService.cs"
  "Services/Projects/ProjectScheduleService.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectScheduleServiceTests.cs"
  ".ship/configs/pr462-b9-w3-pr8-schedule-spine.sh"
)
