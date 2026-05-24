#!/bin/bash
# Sprint 13.5 PRA-7 — Warehouse / Bin / Lot / Serial / ItemGroup / PostingProfile
# Master Files Baseline cascade ship #5 of 8.
# See docs/ADR-019-wms-posting-profile-pattern.md.

BRANCH="sprint-13.5/pra-7-warehouse-bin-lot-serial-itemgroup-postingprofile"
TITLE="feat(masters): PRA-7 Warehouse / Bin / Lot / Serial / ItemGroup / PostingProfile + ADR-019"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra7-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra7-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra7-comment.md"

FILES=(
  "Models/Masters/WarehouseMaster.cs"
  "Models/Masters/BinMaster.cs"
  "Models/Masters/LotMaster.cs"
  "Models/Masters/SerialMaster.cs"
  "Models/Masters/ItemGroup.cs"
  "Models/Masters/PostingProfile.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524200000_AddWarehouseBinLotSerialItemGroupPostingProfilePRA7.cs"
  "docs/ADR-019-wms-posting-profile-pattern.md"
  ".ship/configs/pra7.sh"
)
