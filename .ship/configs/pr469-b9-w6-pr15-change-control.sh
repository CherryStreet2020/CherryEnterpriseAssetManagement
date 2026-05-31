#!/bin/bash
# Per-PR config — B9 Wave 6 PR-15: change control (OPENS Wave 6).
# ProjectChangeRequest (intake/impact/approval) → converts into a ProjectAmendment
# (change order). IProjectChangeService hosts the §20 convert gate (no applied
# customer scope change before its approval clears). EXTENDS ProjectAmendment
# (adds SourceChangeRequestId back-link). SCHEMA CHANGE (1 new table + 1 column).
BRANCH="feat/b9.w6-15-change-control"
TITLE="feat(b9): change control — ProjectChangeRequest → change order + §20 convert gate — OPENS B9 Wave 6 (PR-15)"
COMMIT_MSG=".ship/msgs/pr469-commit.txt"
PR_BODY=".ship/msgs/pr469-body.md"
FILES=(
  "Models/Projects/ProjectChangeRequest.cs"
  "Models/Projects/ProjectAmendment.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260531080308_B9Wave6Pr15ChangeControl.cs"
  "Migrations/20260531080308_B9Wave6Pr15ChangeControl.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectChangeService.cs"
  "Services/Projects/ProjectChangeService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectChangeServiceTests.cs"
  ".ship/configs/pr469-b9-w6-pr15-change-control.sh"
)
