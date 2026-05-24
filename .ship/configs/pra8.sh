#!/bin/bash
# Sprint 13.5 PRA-8 — Employee + WageGroup + LaborRateMaster + Department GL extension
# Master Files Baseline cascade ship #6 of 10

BRANCH="sprint-13.5/pra-8-employee-wagegroup-laborrate-master"
TITLE="feat(masters): PRA-8 Employee + WageGroup + LaborRateMaster + Department GL extension"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra8-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra8-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra8-comment.md"

FILES=(
  "Models/Masters/Employee.cs"
  "Models/Masters/WageGroup.cs"
  "Models/Masters/LaborRate.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524210000_AddEmployeeWageGroupLaborRatePRA8.cs"
  ".ship/configs/pra8.sh"
)
