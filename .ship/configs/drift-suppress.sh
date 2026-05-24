#!/bin/bash
# Infra fix — Suppress PendingModelChangesWarning in all environments so
# MigrateAsync() can run on prod. Unblocks setting AUTO_MIGRATE=true.

BRANCH="fix/infra-suppress-pending-model-changes-warning"
TITLE="fix(infra): suppress PendingModelChangesWarning in all environments"
COMMIT_MSG="$SCRIPT_DIR/msgs/drift-suppress-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/drift-suppress-body.md"

FILES=(
  "Program.cs"
  ".ship/configs/drift-suppress.sh"
)
