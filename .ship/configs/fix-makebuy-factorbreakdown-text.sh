#!/bin/bash
# Hotfix — MakeBuyDecision.FactorBreakdown jsonb → text (persist 22P02).

BRANCH="fix/makebuy-factorbreakdown-text"
TITLE="fix(b7): MakeBuyDecision FactorBreakdown jsonb -> text (persist 22P02)"
COMMIT_MSG=".ship/msgs/pr-mbjson-commit.txt"
PR_BODY=".ship/msgs/pr-mbjson-body.md"

FILES=(
  "Models/Production/MakeBuyDecision.cs"
  "Migrations/20260530003024_MakeBuyFactorBreakdownToText.cs"
  "Migrations/20260530003024_MakeBuyFactorBreakdownToText.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/fix-makebuy-factorbreakdown-text.sh"
)
