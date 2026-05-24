#!/bin/bash
# Sprint 13.5 PR #5d — Operator Workbench
BRANCH="sprint-13.5/pr-5d-operator-workbench"
TITLE="feat(production): Operator Workbench + LaborEntry + ReasonCode (PR #5d)"

COMMIT_MSG="$SCRIPT_DIR/msgs/pr5d-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pr5d-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pr5d-comment.md"

FILES=(
  "Models/Production/LaborEntry.cs"
  "Models/Production/ReasonCode.cs"
  "Migrations/20260524130000_AddLaborAndReasonCodes.cs"
  "Data/AppDbContext.cs"
  "Services/Production/ILaborService.cs"
  "Services/Navigation/ControlCenterRegistry.cs"
  "Pages/Production/Workbench.cshtml"
  "Pages/Production/Workbench.cshtml.cs"
  "Pages/Production/_WorkbenchPreview.cshtml"
  "Program.cs"
  ".ship/configs/pr5d.sh"
)
