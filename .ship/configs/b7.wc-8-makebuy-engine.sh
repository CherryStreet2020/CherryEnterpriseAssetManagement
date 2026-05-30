#!/bin/bash
# Per-PR config for Theme B7 Wave C PR-8 — make-or-buy decision engine.

BRANCH="feat/b7.wc-8-makebuy-engine"
TITLE="B7 Wave C PR-8 — IMakeBuyDecisionService (F1-F6, F2 = real R4 Load%)"
COMMIT_MSG=".ship/msgs/pr-wc8-commit.txt"
PR_BODY=".ship/msgs/pr-wc8-body.md"

FILES=(
  "Services/Production/IMakeBuyDecisionService.cs"
  "Services/Production/MakeBuyDecisionService.cs"
  "Pages/Admin/MakeBuyEngineProbe.cshtml"
  "Pages/Admin/MakeBuyEngineProbe.cshtml.cs"
  "Program.cs"
  ".ship/configs/b7.wc-8-makebuy-engine.sh"
)
