#!/bin/bash
# Sprint 13.5 PRA-11 — PackLevel + ItemPackHierarchy
# Master Files Baseline cascade ship #9 of 10 (last small one before PRA-5b)

BRANCH="sprint-13.5/pra-11-pack-hierarchy"
TITLE="feat(masters): PRA-11 PackLevel + ItemPackHierarchy"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra11-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra11-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra11-comment.md"

FILES=(
  "Models/Masters/PackLevel.cs"
  "Models/Masters/ItemPackHierarchy.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524240000_AddPackLevelItemPackHierarchyPRA11.cs"
  ".ship/configs/pra11.sh"
)
