#!/bin/bash
# Per-PR config for Theme B11 R4-12 — dispatch board (CLOSES Wave R4).

BRANCH="feat/b11.r4-12-dispatch-board"
TITLE="B11 R4-12 — dispatch board (per-WC queue by dispatch rule) [CLOSES R4]"
COMMIT_MSG=".ship/msgs/pr-r412-commit.txt"
PR_BODY=".ship/msgs/pr-r412-body.md"

FILES=(
  "Services/Production/Scheduling/IDispatchBoardService.cs"
  "Services/Production/Scheduling/DispatchBoardService.cs"
  "Pages/Admin/DispatchBoardProbe.cshtml"
  "Pages/Admin/DispatchBoardProbe.cshtml.cs"
  "Program.cs"
  ".ship/configs/b11.r4-12-dispatch-board.sh"
)
