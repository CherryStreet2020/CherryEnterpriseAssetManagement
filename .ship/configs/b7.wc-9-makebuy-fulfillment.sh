#!/bin/bash
# Per-PR config for Theme B7 Wave C PR-9 — make-or-buy supply integration (CLOSES Wave C).

BRANCH="feat/b7.wc-9-makebuy-fulfillment"
TITLE="B7 Wave C PR-9 — make-or-buy supply integration [CLOSES Wave C]"
COMMIT_MSG=".ship/msgs/pr-wc9-commit.txt"
PR_BODY=".ship/msgs/pr-wc9-body.md"

FILES=(
  "Services/Production/IMakeBuyFulfillmentService.cs"
  "Services/Production/MakeBuyFulfillmentService.cs"
  "Pages/Admin/MakeBuyFulfillProbe.cshtml"
  "Pages/Admin/MakeBuyFulfillProbe.cshtml.cs"
  "Program.cs"
  ".ship/configs/b7.wc-9-makebuy-fulfillment.sh"
)
