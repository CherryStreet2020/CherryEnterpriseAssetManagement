#!/bin/bash
# Per-PR config — B9 Wave 1 PR-1: Project Command Center (the BIC money-shot).
# OPENS Theme B9 (Customer Project Manager). Read-only aggregation + page + tests.
# No schema change (reads existing CustomerProject/ProductionOrder/ProjectAmendment).
BRANCH="feat/b9.w1-1-project-command-center"
TITLE="feat(b9): project command center + promise-ready KPI band (OPENS B9)"
COMMIT_MSG=".ship/msgs/pr455-commit.txt"
PR_BODY=".ship/msgs/pr455-body.md"
FILES=(
  "Services/Projects/IProjectCommandCenterService.cs"
  "Services/Projects/ProjectCommandCenterService.cs"
  "Pages/CustomerProjects/CommandCenter.cshtml"
  "Pages/CustomerProjects/CommandCenter.cshtml.cs"
  "Pages/CustomerProjects/Details.cshtml"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/ProjectCommandCenterServiceTests.cs"
  "docs/research/b9-cascade-design.md"
  ".ship/configs/pr455-b9-w1-pr1-command-center.sh"
)
