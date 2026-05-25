#!/bin/bash
# Per-PR config for PR #337 (GitHub PR# pending) — /Admin/AssetImport bulk Excel upload.
# File name uses pr-338 to disambiguate from the existing pr-337-abs-demo-photos config.
BRANCH="feature/pr337-asset-import"
TITLE="PR #337 — /Admin/AssetImport: Excel upload, preview, commit"
COMMIT_MSG=".ship/msgs/pr-337-asset-import-commit.txt"
PR_BODY=".ship/msgs/pr-337-asset-import-body.md"

FILES=(
  "Models/AssetImport/AssetImportBatch.cs"
  "Models/AssetImport/AssetImportRow.cs"
  "Services/AssetImport/IAssetImportService.cs"
  "Services/AssetImport/AssetImportService.cs"
  "Data/AppDbContext.cs"
  "Program.cs"
  "Pages/Admin/AssetImport/Index.cshtml"
  "Pages/Admin/AssetImport/Index.cshtml.cs"
  "Pages/Admin/AssetImport/Upload.cshtml"
  "Pages/Admin/AssetImport/Upload.cshtml.cs"
  "Pages/Admin/AssetImport/Preview.cshtml"
  "Pages/Admin/AssetImport/Preview.cshtml.cs"
  "Pages/Admin/AssetImport/Detail.cshtml"
  "Pages/Admin/AssetImport/Detail.cshtml.cs"
  "Pages/Admin/DataImport.cshtml"
  "Migrations/20260525163613_AddAssetImportTables_PR337.cs"
  "Migrations/20260525163613_AddAssetImportTables_PR337.Designer.cs"
  "Migrations/AppDbContextModelSnapshot.cs"
  "tests/Abs.FixedAssets.Tests/AssetImportServiceTests.cs"
  "docs/research/asset-import-pr337-spec-2026-05-25.md"
)
