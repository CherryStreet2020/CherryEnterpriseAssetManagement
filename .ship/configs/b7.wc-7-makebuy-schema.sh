#!/bin/bash
# Per-PR config for Theme B7 Wave C PR-7 — make-or-buy decision schema.

BRANCH="feat/b7.wc-7-makebuy-schema"
TITLE="B7 Wave C PR-7 — MakeBuyDecision + policy schema (OPENS Wave C)"
COMMIT_MSG=".ship/msgs/pr-wc7-commit.txt"
PR_BODY=".ship/msgs/pr-wc7-body.md"

FILES=(
  "Models/Production/MakeBuyDecision.cs"
  "Data/AppDbContext.cs"
  "Pages/Admin/MakeBuyDecisionProbe.cshtml"
  "Pages/Admin/MakeBuyDecisionProbe.cshtml.cs"
  "Migrations/20260529222856_B7WaveC_MakeBuyDecision.cs"
  "Migrations/20260529222856_B7WaveC_MakeBuyDecision.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/b7.wc-7-makebuy-schema.sh"
)
