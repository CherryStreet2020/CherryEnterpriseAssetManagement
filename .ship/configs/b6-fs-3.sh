#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-3 — ItemStandardCostElement
# (SAP Cost Component Split equivalent) + IItemStandardCostService + admin probe.

BRANCH="feat/b6-fs-3-itemstandardcostelement"
TITLE="feat(b6-fs-3): ItemStandardCostElement (SAP Cost Component Split) + IItemStandardCostService + /Admin/ItemStandardCostProbe"
COMMIT_MSG=".ship/msgs/fs3-commit.txt"
PR_BODY=".ship/msgs/fs3-body.md"

FILES=(
  "Models/Masters/ItemStandardCostElement.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526204540_AddItemStandardCostElementFsPr3.cs"
  "Migrations/20260526204540_AddItemStandardCostElementFsPr3.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/IItemStandardCostService.cs"
  "Services/Items/ItemStandardCostService.cs"
  "Pages/Admin/ItemStandardCostProbe.cshtml"
  "Pages/Admin/ItemStandardCostProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemStandardCostServiceTests.cs"
  ".ship/configs/b6-fs-3.sh"
)
