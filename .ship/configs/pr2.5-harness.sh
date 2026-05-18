#!/bin/bash
# Sprint 11 PR #2.5 — ship-workflow harness
# Run from the repo root:
#   bash .ship/run.sh full .ship/configs/pr2.5-harness.sh

BRANCH="chore/sprint-11-ship-harness"
TITLE="chore(sprint-11): single-command ship-workflow harness (.ship/)"

COMMIT_MSG="$SCRIPT_DIR/msgs/pr2.5-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pr2.5-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pr2.5-comment.md"

# No production code touched — skip the local dotnet build for this one.
SKIP_BUILD=1

FILES=(
  ".ship/run.sh"
  ".ship/lib.sh"
  ".ship/README.md"
  ".ship/.gitignore"
  ".ship/configs/pr2.5-harness.sh"
)
