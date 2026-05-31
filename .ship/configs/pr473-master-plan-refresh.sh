#!/bin/bash
# Per-PR config — docs-only Master Plan refresh through B9 close (session 33).
# NO schema, NO code. Docs-only → merge --admin (no Replit pull/restart/E2E needed).
BRANCH="chore/master-plan-refresh-b9-close"
TITLE="chore(master-plan): refresh through B9 close — B7/B11/B9 complete, B10 is the frontier"
COMMIT_MSG=".ship/msgs/pr473-commit.txt"
PR_BODY=".ship/msgs/pr473-body.md"
FILES=(
  "docs/research/MASTER_PLAN.md"
  ".ship/configs/pr473-master-plan-refresh.sh"
)
