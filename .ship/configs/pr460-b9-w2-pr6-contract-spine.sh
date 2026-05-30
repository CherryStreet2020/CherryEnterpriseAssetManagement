#!/bin/bash
# Per-PR config — B9 Wave 2 PR-6 (CLOSES Wave 2): quote-to-cash spine, contract/award.
# ProjectContract / ProjectContractLine / ProjectCustomerPO + contract-review gate +
# award validation (winning revision -> baseline). SCHEMA CHANGE (3 new tables).
BRANCH="feat/b9.w2-6-contract-spine"
TITLE="feat(b9): contract/award spine — review gate + winning-revision baseline — CLOSES B9 W2"
COMMIT_MSG=".ship/msgs/pr460-commit.txt"
PR_BODY=".ship/msgs/pr460-body.md"
FILES=(
  "Models/Projects/ProjectContract.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260530173854_B9WaveW2_ProjectContractSpine.cs"
  "Migrations/20260530173854_B9WaveW2_ProjectContractSpine.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Projects/IProjectContractService.cs"
  "Services/Projects/ProjectContractService.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectContractServiceTests.cs"
  ".ship/configs/pr460-b9-w2-pr6-contract-spine.sh"
)
