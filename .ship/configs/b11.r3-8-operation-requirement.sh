#!/bin/bash
# Per-PR config for Theme B11 Wave R3-8 — OperationCapabilityRequirement (retire the CSV).

BRANCH="feat/b11.r3-8-operation-requirement"
TITLE="B11 R3-8 — OperationCapabilityRequirement (retire CSV skill/tooling)"
COMMIT_MSG=".ship/msgs/pr436-commit.txt"
PR_BODY=".ship/msgs/pr436-body.md"

FILES=(
  "Models/Production/OperationCapabilityRequirement.cs"
  "Models/Production/RoutingOperation.cs"
  "Data/AppDbContext.cs"
  "Pages/Admin/OperationRequirementProbe.cshtml"
  "Pages/Admin/OperationRequirementProbe.cshtml.cs"
  "Migrations/20260529191654_B11_R3_8_OperationCapabilityRequirement.cs"
  "Migrations/20260529191654_B11_R3_8_OperationCapabilityRequirement.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/b11.r3-8-operation-requirement.sh"
)
