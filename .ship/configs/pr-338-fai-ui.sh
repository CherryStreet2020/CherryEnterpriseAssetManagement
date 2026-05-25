#!/bin/bash
# Per-PR config for PR #338 — FAI UI (AS9102 First Article Inspection).
BRANCH="feature/pr338-fai-ui"
TITLE="PR #338 — FAI UI: list, create, characteristic, sign-off"
COMMIT_MSG=".ship/msgs/pr-338-fai-ui-commit.txt"
PR_BODY=".ship/msgs/pr-338-fai-ui-body.md"

FILES=(
  "Services/Quality/IFaiService.cs"
  "Services/Quality/FaiService.cs"
  "Pages/Quality/Fai/Index.cshtml"
  "Pages/Quality/Fai/Index.cshtml.cs"
  "Pages/Quality/Fai/Create.cshtml"
  "Pages/Quality/Fai/Create.cshtml.cs"
  "Pages/Quality/Fai/Detail.cshtml"
  "Pages/Quality/Fai/Detail.cshtml.cs"
  "Pages/CustomerProjects/Details.cshtml"
  "Pages/CustomerProjects/Details.cshtml.cs"
  "Program.cs"
  "docs/research/fai-ui-pr338-spec-2026-05-25.md"
)
