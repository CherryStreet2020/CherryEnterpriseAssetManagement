#!/bin/bash
# Per-PR config for Theme B11 R4-11 — finite scheduler (the real engine).

BRANCH="feat/b11.r4-11-finite-scheduler"
TITLE="B11 R4-11 — finite scheduler (calendar + capacity + alternates)"
COMMIT_MSG=".ship/msgs/pr-r411-commit.txt"
PR_BODY=".ship/msgs/pr-r411-body.md"

FILES=(
  "Services/Production/Scheduling/WorkingTimeEngine.cs"
  "Services/Production/Scheduling/IFiniteSchedulingService.cs"
  "Services/Production/Scheduling/FiniteSchedulingService.cs"
  "Pages/Admin/FiniteScheduleProbe.cshtml"
  "Pages/Admin/FiniteScheduleProbe.cshtml.cs"
  "Program.cs"
  ".ship/configs/b11.r4-11-finite-scheduler.sh"
)
