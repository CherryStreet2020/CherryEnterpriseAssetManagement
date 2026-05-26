#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-7 — 18-column Item Master expansion
# + IItemMasterReader projection service + ItemGroupResolver IsSellable
# tightening + /Admin/ItemMasterExpansionProbe.
#
# THIS IS THE LAST MAIN-PATH PR. Closes the B6 Foundation Sprint at 7/7 main + 1 inserted + 1 hotfix.

BRANCH="feat/b6-fs-7-itemmaster-18col-expansion"
TITLE="feat(b6-fs-7): Item Master 18-column expansion + IsSellable resolver tightening + IItemMasterReader + admin probe — CLOSES B6 Foundation Sprint"
COMMIT_MSG=".ship/msgs/fs7-commit.txt"
PR_BODY=".ship/msgs/fs7-body.md"

FILES=(
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526222644_AddItemMasterExpansionFsPr7.cs"
  "Migrations/20260526222644_AddItemMasterExpansionFsPr7.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/IItemGroupResolver.cs"
  "Services/Items/ItemGroupResolver.cs"
  "Services/Items/IItemMasterReader.cs"
  "Services/Items/ItemMasterReader.cs"
  "Services/Seeding/ItemGroupBackfillSeeder.cs"
  "Pages/Admin/ItemMasterExpansionProbe.cshtml"
  "Pages/Admin/ItemMasterExpansionProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemMasterReaderTests.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemGroupClassificationTests.cs"
  ".ship/configs/b6-fs-7.sh"
)
