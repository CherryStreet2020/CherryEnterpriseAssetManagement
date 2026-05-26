#!/bin/bash
# Per-PR config for B6 Foundation Sprint PR-FS-1.5.1 — semantic hotfix for the
# PR-FS-1 / PR-FS-1.5 Part→FG default bug. Adds Source-aware classification +
# IItemSourceBackfillSeeder + Reclassify mode + admin page.

BRANCH="feat/b6-fs-1.5.1-hotfix-source-aware-itemgroup"
TITLE="feat(b6-fs-1.5.1): Source-aware ItemGroup resolver + Source backfill + Reclassify mode (hotfix for Part→FG default)"
COMMIT_MSG=".ship/msgs/fs151-commit.txt"
PR_BODY=".ship/msgs/fs151-body.md"

FILES=(
  "Services/Items/IItemGroupResolver.cs"
  "Services/Items/ItemGroupResolver.cs"
  "Services/Seeding/IItemGroupBackfillSeeder.cs"
  "Services/Seeding/ItemGroupBackfillSeeder.cs"
  "Services/Seeding/IItemSourceBackfillSeeder.cs"
  "Services/Seeding/ItemSourceBackfillSeeder.cs"
  "Pages/Admin/BackfillItemGroups.cshtml"
  "Pages/Admin/BackfillItemGroups.cshtml.cs"
  "Pages/Admin/BackfillItemSource.cshtml"
  "Pages/Admin/BackfillItemSource.cshtml.cs"
  "Program.cs"
  "tests/Abs.FixedAssets.Tests/Services/Items/ItemGroupClassificationTests.cs"
  "tests/Abs.FixedAssets.Tests/Services/Seeding/ItemGroupBackfillSeederTests.cs"
  "tests/Abs.FixedAssets.Tests/Services/Seeding/ItemSourceBackfillSeederTests.cs"
  ".ship/configs/b6-fs-1-5-1.sh"
)
