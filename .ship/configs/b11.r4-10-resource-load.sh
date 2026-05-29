#!/bin/bash
# Per-PR config for Theme B11 R4-10 — resource load profile + calendar engine (OPENS Wave R4).

BRANCH="feat/b11.r4-10-resource-load"
TITLE="B11 R4-10 — resource load + calendar engine (real Load% + drum)"
COMMIT_MSG=".ship/msgs/pr-r410-commit.txt"
PR_BODY=".ship/msgs/pr-r410-body.md"

FILES=(
  "Services/Production/IResourceLoadService.cs"
  "Services/Production/ResourceLoadService.cs"
  "Pages/Admin/ResourceLoadProbe.cshtml"
  "Pages/Admin/ResourceLoadProbe.cshtml.cs"
  "Program.cs"
  ".ship/configs/b11.r4-10-resource-load.sh"
)
