#!/bin/bash
# Sprint 13.5 PR #5c.2 — Tenant Scoping Hardening (P0 cross-tenant security)
# Run from the repo root:
#   bash .ship/run.sh full .ship/configs/pr5c.2.sh

BRANCH="sprint-13.5/pr-5c.2-tenant-scoping-hardening"
TITLE="feat(production): tenant scoping hardening + 4 UNIQUE leak fixes (PR #5c.2)"

COMMIT_MSG="$SCRIPT_DIR/msgs/pr5c.2-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pr5c.2-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pr5c.2-comment.md"

FILES=(
  "Migrations/20260524120000_TenantScopingHardeningPr5c2.cs"
  "Models/Production/ProductionOrder.cs"
  "Models/Production/ProductionBatch.cs"
  "Models/Production/MaterialMaster.cs"
  "Models/Production/MaterialStructure.cs"
  "Models/Production/ProductionOperation.cs"
  "Models/AssetMaintenance.cs"
  "Data/AppDbContext.cs"
  "Services/Production/ProductionOrderService.cs"
  "Services/Production/IProductionOperationService.cs"
  ".ship/drafts/PR-5c2-tenant-scoping.sql"
  ".ship/configs/pr5c.2.sh"
  ".ship/msgs/pr5c.2-commit.txt"
  ".ship/msgs/pr5c.2-body.md"
  ".ship/msgs/pr5c.2-comment.md"
)
