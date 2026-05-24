#!/bin/bash
# Sprint 13.5 PRA-4 — Unified UOM master.
# Master Files Baseline cascade ship #1 (of 8 PRA-cascade).

BRANCH="sprint-13.5/pra-4-uom-master"
TITLE="feat(masters): PRA-4 unified UOM master + Item FK columns"

COMMIT_MSG="$SCRIPT_DIR/msgs/pra4-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra4-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra4-comment.md"

FILES=(
  "Models/Masters/UomCategory.cs"
  "Models/Masters/UnitOfMeasureMaster.cs"
  "Models/Masters/UomConversion.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Services/Masters/IUomService.cs"
  "Migrations/20260524160000_AddUomMasterPRA4.cs"
  "Program.cs"
  "docs/research/master-files-baseline-2026-05-24.md"
  "docs/research/master-plan-audit-2026-05-24.md"
  ".ship/configs/pra4.sh"
)
