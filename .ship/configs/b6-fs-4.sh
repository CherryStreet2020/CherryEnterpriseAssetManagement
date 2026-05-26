#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-4 — CostLayer FIFO/LIFO/Average
# inventory-valuation layers (SAP MM stock-with-values equivalent) + ICostLayerService
# + /Admin/CostLayerProbe page.

BRANCH="feat/b6-fs-4-costlayer-fifo-lifo-average"
TITLE="feat(b6-fs-4): CostLayer (FIFO/LIFO/Average inventory valuation) + ICostLayerService + /Admin/CostLayerProbe"
COMMIT_MSG=".ship/msgs/fs4-commit.txt"
PR_BODY=".ship/msgs/fs4-body.md"

FILES=(
  "Models/Masters/CostLayer.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526211151_AddCostLayerFsPr4.cs"
  "Migrations/20260526211151_AddCostLayerFsPr4.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/ICostLayerService.cs"
  "Services/Items/CostLayerService.cs"
  "Pages/Admin/CostLayerProbe.cshtml"
  "Pages/Admin/CostLayerProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/CostLayerServiceTests.cs"
  ".ship/configs/b6-fs-4.sh"
)
