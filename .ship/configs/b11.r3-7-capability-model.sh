#!/bin/bash
# Per-PR config for Theme B11 Wave R3-7 — capability model (OPENS Wave R3).

BRANCH="feat/b11.r3-7-capability-model"
TITLE="B11 R3-7 — capability model (Capability + ResourceCapability)"
COMMIT_MSG=".ship/msgs/pr435-commit.txt"
PR_BODY=".ship/msgs/pr435-body.md"

FILES=(
  "Models/Production/Capability.cs"
  "Models/Production/ResourceCapability.cs"
  "Models/Production/ProductionResource.cs"
  "Data/AppDbContext.cs"
  "Pages/Admin/CapabilityProbe.cshtml"
  "Pages/Admin/CapabilityProbe.cshtml.cs"
  "Migrations/20260529184621_B11_R3_7_CapabilityModel.cs"
  "Migrations/20260529184621_B11_R3_7_CapabilityModel.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  ".ship/configs/b11.r3-7-capability-model.sh"
)
