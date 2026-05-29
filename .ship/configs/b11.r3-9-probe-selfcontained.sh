#!/bin/bash
# Per-PR config for Theme B11 R3-9 follow-up — self-contained match worked example (probe-only).

BRANCH="fix/b11.r3-9-probe-selfcontained"
TITLE="B11 R3-9 follow-up — self-contained capability-match worked example"
COMMIT_MSG=".ship/msgs/pr438-commit.txt"
PR_BODY=".ship/msgs/pr438-body.md"

FILES=(
  "Pages/Admin/CapabilityMatchProbe.cshtml.cs"
  ".ship/configs/b11.r3-9-probe-selfcontained.sh"
)
