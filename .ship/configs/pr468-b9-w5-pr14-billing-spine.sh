#!/bin/bash
# Per-PR config — B9 Wave 5 PR-14: billing/invoice/revenue recognition (CLOSES W5).
# ProjectBillingSchedule / ProjectInvoiceLink / ProjectRevenueRecognition +
# IProjectBillingService (milestone-achieved + acceptance invoicing gates).
# SCHEMA CHANGE (3 new tables).
BRANCH="feat/b9.w5-14-billing-spine"
TITLE="feat(b9): billing/invoice/revenue recognition + milestone & acceptance gates — CLOSES B9 Wave 5 (PR-14)"
COMMIT_MSG=".ship/msgs/pr468-commit.txt"
PR_BODY=".ship/msgs/pr468-body.md"
FILES=(
  "Models/Projects/ProjectBilling.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530230823_B9Wave5Pr14BillingSpine.cs"
  "Migrations/20260530230823_B9Wave5Pr14BillingSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectBillingService.cs"
  "Services/Projects/ProjectBillingService.cs"
  "Services/Seeding/CooMotionDemoSeeder.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectBillingServiceTests.cs"
  ".ship/configs/pr468-b9-w5-pr14-billing-spine.sh"
)
