#!/bin/bash
# PR #324 — Build hotfix: exclude reference/repos clones from MSBuild Compile glob.
# Prerequisite for PRA-5b — pre-flight build verification surfaced 2293 errors
# in reference/repos/npgsql/efcore.pg/QueryBaseline.cs (raw-string-literal
# syntax incompatible with our C# language version). The reference repos are
# meant for grep-before-infer only, never compiled.

BRANCH="fix/exclude-reference-repos-from-csproj"
TITLE="fix(build): exclude reference/repos clones from Compile glob"
COMMIT_MSG="$SCRIPT_DIR/msgs/pr324-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pr324-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pr324-comment.md"

FILES=(
  "Abs.FixedAssets.csproj"
  ".ship/configs/pr324.sh"
)
