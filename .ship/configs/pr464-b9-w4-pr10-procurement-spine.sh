#!/bin/bash
# Per-PR config — B9 Wave 4 PR-10: procurement spine (OPENS Wave 4).
# PurchaseOrder→project peg + ProjectProcurementPlan/Commitment/Receipt +
# the project-close gate (cannot close with open commitments unless waived).
# SCHEMA CHANGE (3 new tables + 2 new PurchaseOrder columns).
BRANCH="feat/b9.w4-10-procurement-spine"
TITLE="feat(b9): procurement spine — PO project peg + plan/commitment/receipt + close gate (B9 W4 PR-10)"
COMMIT_MSG=".ship/msgs/pr464-commit.txt"
PR_BODY=".ship/msgs/pr464-body.md"
FILES=(
  "Models/Projects/ProjectProcurement.cs"
  "Models/PurchaseOrder.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530204102_B9Wave4Pr10ProcurementSpine.cs"
  "Migrations/20260530204102_B9Wave4Pr10ProcurementSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectProcurementService.cs"
  "Services/Projects/ProjectProcurementService.cs"
  "Services/Projects/CustomerProjectService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectProcurementServiceTests.cs"
  ".ship/configs/pr464-b9-w4-pr10-procurement-spine.sh"
)
