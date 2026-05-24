#!/bin/bash
# Sprint 13.5 PRA-9 — PriceListMaster + PriceListLine + DiscountSchema + RebateAgreement + ADR-027
# Master Files Baseline cascade ship #7 of 10

BRANCH="sprint-13.5/pra-9-pricelist-discount-rebate"
TITLE="feat(masters): PRA-9 PriceList + Discount + Rebate + ADR-027 (SO Lines drive PO)"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra9-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra9-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra9-comment.md"

FILES=(
  "Models/Masters/PriceListMaster.cs"
  "Models/Masters/PriceListLine.cs"
  "Models/Masters/DiscountSchema.cs"
  "Models/Masters/RebateAgreement.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260524220000_AddPriceListDiscountRebatePRA9.cs"
  "docs/ADR-027-sales-order-line-and-release-shape.md"
  ".ship/configs/pra9.sh"
)
