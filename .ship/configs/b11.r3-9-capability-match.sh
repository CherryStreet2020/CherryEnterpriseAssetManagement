#!/bin/bash
# Per-PR config for Theme B11 Wave R3-9 — capability-match resolver (CLOSES Wave R3). No schema change.

BRANCH="feat/b11.r3-9-capability-match"
TITLE="B11 R3-9 — ICapabilityMatchService eligible-resource resolver (CLOSES Wave R3)"
COMMIT_MSG=".ship/msgs/pr437-commit.txt"
PR_BODY=".ship/msgs/pr437-body.md"

FILES=(
  "Services/Production/ICapabilityMatchService.cs"
  "Services/Production/CapabilityMatchService.cs"
  "Program.cs"
  "Pages/Admin/CapabilityMatchProbe.cshtml"
  "Pages/Admin/CapabilityMatchProbe.cshtml.cs"
  ".ship/configs/b11.r3-9-capability-match.sh"
)
