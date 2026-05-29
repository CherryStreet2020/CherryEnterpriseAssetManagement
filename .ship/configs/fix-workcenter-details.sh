#!/bin/bash
# Per-PR config — fix WorkCenters Details 404 + surface under Master Files.

BRANCH="fix/workcenter-details-page"
TITLE="fix: WorkCenter Details page (404) + Master Files card"
COMMIT_MSG=".ship/msgs/pr-wcdet-commit.txt"
PR_BODY=".ship/msgs/pr-wcdet-body.md"

FILES=(
  "Pages/Admin/WorkCenters/Details.cshtml"
  "Pages/Admin/WorkCenters/Details.cshtml.cs"
  "Pages/Admin/Index.cshtml"
  ".ship/configs/fix-workcenter-details.sh"
)
