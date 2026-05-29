#!/bin/bash
# Per-PR config — WorkCenter create/edit form.

BRANCH="feat/workcenter-create-edit-form"
TITLE="feat: WorkCenter create/edit form (Master Files CRUD)"
COMMIT_MSG=".ship/msgs/pr-wcedit-commit.txt"
PR_BODY=".ship/msgs/pr-wcedit-body.md"

FILES=(
  "Services/Production/IWorkCenterService.cs"
  "Pages/Admin/WorkCenters/Edit.cshtml"
  "Pages/Admin/WorkCenters/Edit.cshtml.cs"
  "Pages/Admin/WorkCenters/Index.cshtml"
  "Pages/Admin/WorkCenters/Details.cshtml"
  ".ship/configs/wc-create-edit-form.sh"
)
