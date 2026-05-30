#!/bin/bash
# Per-PR config — B9 Wave 2 PR-5: quote-to-cash spine, estimate layer.
# ProjectEstimate / ProjectEstimateLine / ProjectEstimateSnapshot + the frozen
# snapshot the quote revision SourceEstimateSnapshotId now FKs to.
# SCHEMA CHANGE (3 new tables + additive FK on ProjectQuoteRevisions).
BRANCH="feat/b9.w2-5-estimate-spine"
TITLE="feat(b9): estimate spine — Estimate/Line/Snapshot + frozen cost model (B9 W2 PR-5)"
COMMIT_MSG=".ship/msgs/pr459-commit.txt"
PR_BODY=".ship/msgs/pr459-body.md"
FILES=(
  "Models/Projects/ProjectEstimate.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530171001_B9WaveW2_ProjectEstimateSpine.cs"
  "Migrations/20260530171001_B9WaveW2_ProjectEstimateSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectEstimateService.cs"
  "Services/Projects/ProjectEstimateService.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectEstimateServiceTests.cs"
  ".ship/configs/pr459-b9-w2-pr5-estimate-spine.sh"
)
