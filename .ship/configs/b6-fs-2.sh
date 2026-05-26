#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-2 — ItemSite per-Site override entity
# (SAP MARC equivalent) + IItemSiteResolver service + /Admin/ItemSiteProbe page.

BRANCH="feat/b6-fs-2-itemsite-per-site-overrides"
TITLE="feat(b6-fs-2): ItemSite per-Site override entity + IItemSiteResolver + /Admin/ItemSiteProbe"
COMMIT_MSG=".ship/msgs/fs2-commit.txt"
PR_BODY=".ship/msgs/fs2-body.md"

FILES=(
  "Models/Masters/ItemSite.cs"
  "Models/Item.cs"
  "Data/AppDbContext.cs"
  "Migrations/20260526201506_AddItemSiteFsPr2.cs"
  "Migrations/20260526201506_AddItemSiteFsPr2.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "Services/Items/IItemSiteResolver.cs"
  "Services/Items/ItemSiteResolver.cs"
  "Pages/Admin/ItemSiteProbe.cshtml"
  "Pages/Admin/ItemSiteProbe.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemSiteResolverTests.cs"
  ".ship/configs/b6-fs-2.sh"
)
