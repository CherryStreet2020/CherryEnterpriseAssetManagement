#!/bin/bash
# Per-PR config — B9 Wave 6 PR-16: governance (RAID + meetings).
# ProjectRisk / ProjectIssue / ProjectMeeting / ProjectActionItem / ProjectDecision
# + IProjectGovernanceService (per-entity create/transition + GetGovernance rollup;
# set-once close/complete stamps; cross-linked to WBS phases + change requests).
# SCHEMA CHANGE (5 new tables).
BRANCH="feat/b9.w6-16-governance"
TITLE="feat(b9): governance — RAID (risks/issues/actions/decisions) + meetings (PR-16)"
COMMIT_MSG=".ship/msgs/pr470-commit.txt"
PR_BODY=".ship/msgs/pr470-body.md"
FILES=(
  "Models/Projects/ProjectGovernance.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260531084317_B9Wave6Pr16Governance.cs"
  "Migrations/20260531084317_B9Wave6Pr16Governance.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectGovernanceService.cs"
  "Services/Projects/ProjectGovernanceService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectGovernanceServiceTests.cs"
  ".ship/configs/pr470-b9-w6-pr16-governance.sh"
)
