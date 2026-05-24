#!/bin/bash
# Sprint 13.5 PRA-5e.1 — Extract shared GlPostingHelpers.ResolveAccountAndKeyAsync
# Cleanup-pass refactor. 3 inline copies (Ap/Receiving/Cip) consolidated into
# one extension method on IGlAccountResolver.

BRANCH="sprint-13.5/pra-5e1-extract-glposting-helpers"
TITLE="refactor(masters): PRA-5e.1 extract shared GlPostingHelpers.ResolveAccountAndKeyAsync"
COMMIT_MSG="$SCRIPT_DIR/msgs/pra5e1-commit.txt"
PR_BODY="$SCRIPT_DIR/msgs/pra5e1-body.md"
SHIP_COMMENT="$SCRIPT_DIR/msgs/pra5e1-comment.md"

FILES=(
  "Services/Posting/GlPostingHelpers.cs"
  "Services/AccountsPayable/ApPostingService.cs"
  "Services/Receiving/ReceivingPostingService.cs"
  "Services/Cip/CipCapitalizationService.cs"
  ".ship/configs/pra5e1.sh"
)
