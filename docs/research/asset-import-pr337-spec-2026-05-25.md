# PR #337 — /Admin/AssetImport spec

**Date:** 2026-05-25
**Author:** Claude (Dean session)
**Status:** Approved by Dean (noon checkpoint, Option 1 of 5 — sequenced Asset Import → FAI UI)

## Why this PR exists

Dean tonight: *"I have no idea how to do that paste workflow"* — surfaced when staged
SQL (`.ship/scratch/abs_asset_enrichment.sql`, 25 ABS demo assets, $32.27M CAD) wouldn't
paste cleanly into Replit's Monaco SQL console via Chrome MCP. That's not a Dean
problem — that's a missing product feature. The structural answer is the bulk-import
UI Dean's been entitled to all along.

Per the 2026-05-25 demo-readiness audit, /Admin/DataImport already exists but is a
**code-defined seed pack runner** (one-click idempotent pipelines + CSV template
downloads). It cannot take an arbitrary tenant Excel of assets and create them with
preview-then-commit lifecycle. That gap is closed here.

## Scope (in-scope / out-of-scope)

**IN:**
- Two new entities: `AssetImportBatch` + `AssetImportRow` (tenant-trio compliant per BIC checklist).
- One typed EF migration (Lock 12).
- `IAssetImportService` with four methods: `ParseAndStageAsync` / `ValidateRowsAsync` / `CommitBatchAsync` / `DiscardBatchAsync`.
- Four Razor pages: `/Admin/AssetImport` (list batches), `/Admin/AssetImport/Upload`, `/Admin/AssetImport/Preview/{id}`, `/Admin/AssetImport/Detail/{id}`.
- Excel column mapping aligned to the ABS 25-asset payload — see §Excel column mapping below.
- Template download endpoint (`/Admin/AssetImport?handler=Template`) that generates a starter `.xlsx` with headers + 1 sample row.
- Manufacturer resolution: lookup by name; auto-create if no match (with audit log).
- Audit emission via `AuditService.LogAsync` on commit + discard.
- 3 unit tests on `IAssetImportService`.

**OUT (later PRs):**
- Idempotency keys via `IIdempotencyMediator` — defer to PR #339+ (the staged batch ID already gives a natural retry guard; full Stripe pattern is overkill for a long-running background commit).
- Background-job execution — commit runs in the request thread for now (1000-row cap).
- Multi-sheet workbooks — first worksheet only.
- Update-existing-by-AssetNumber — strict insert-only, conflict on existing AssetNumber = row error.
- Image upload from inside the import — `ImageUrl` is a URL string; assets reference photos already in `wwwroot/uploads/abs-assets/` (PR #336 shipped).
- Importing other entity types (Items, Vendors, Customers) — same shape, different PR.

## Entity shape

### `AssetImportBatch`
- `Id` int PK
- `CompanyId` int NOT NULL (tenant trio)
- `OrganizationId` int? (tenant trio)
- `SiteId` int? (tenant trio)
- `CreatedAt` UTC timestamp
- `CreatedByUserId` int NOT NULL FK → `Users.Id`
- `FileName` string(260)
- `FileSizeBytes` long
- `SheetName` string(64)
- `Status` `AssetImportBatchStatus` enum (Draft=0 / Validated=1 / Committed=2 / Failed=3 / Discarded=4)
- `RowCount` int default 0
- `ValidRowCount` int default 0
- `ErrorRowCount` int default 0
- `ValidatedAt` UTC?
- `CommittedAt` UTC?
- `CommittedByUserId` int?
- `Notes` string(2000)?
- Index: `(CompanyId, CreatedAt DESC)` for Index page
- CHECK: `Status` ∈ 0..4
- CHECK: `ValidRowCount + ErrorRowCount <= RowCount`
- CHECK: `RowCount >= 0`

### `AssetImportRow`
- `Id` long PK
- `BatchId` int NOT NULL FK → `AssetImportBatches.Id` ON DELETE CASCADE
- `RowNumber` int (excel row, 2-based)
- `Status` `AssetImportRowStatus` enum (Pending=0 / Valid=1 / Error=2 / Committed=3)
- `AssetNumber` string(50)?
- `Description` string(200)?
- `LongDescription` string(500)?
- `Model` string(200)?
- `SerialNumber` string(100)?
- `TagNumber` string(50)?
- `ManufacturerName` string(200)?  — resolved to FK on commit
- `ResolvedManufacturerId` int?
- `AcquisitionCost` decimal(18,2)?
- `ReplacementCost` decimal(18,2)?
- `Currency` string(3)?  — default "CAD" for ABS; falls back to "USD" if blank in Excel
- `PurchaseDate` date?
- `InServiceDate` date?
- `FiscalPurchaseYear` int?
- `UsefulLifeMonths` int?
- `ImageUrl` string(500)?
- `LocationCode` string(50)?  — resolved if matches a Location.Code
- `ResolvedLocationId` int?
- `DepartmentCode` string(50)?  — resolved if matches
- `ResolvedDepartmentId` int?
- `SiteCode` string(50)?  — resolved if matches
- `ResolvedSiteId` int?
- `StatusSource` string(50)?  — "Active" / "FullyDepreciated" / etc., parsed to AssetStatus enum
- `ResolvedStatus` int?  — the parsed enum value
- `ValidationErrors` text?  — newline-delimited list of "ColumnName: reason"
- `CommittedAssetId` int?  — populated on commit
- Index: `(BatchId, RowNumber)`

## Excel column mapping

Sheet name: `Assets` (first worksheet by default).
Required headers (any order, case-insensitive lookup):

| Column            | Required | Type          | Asset field                  |
|-------------------|----------|---------------|------------------------------|
| AssetNumber       | YES      | string ≤ 50   | `AssetNumber`                |
| Description       | YES      | string ≤ 200  | `Description`                |
| LongDescription   | no       | string ≤ 500  | `LongDescription`            |
| Manufacturer      | no       | string ≤ 200  | resolve → `ManufacturerId`   |
| Model             | no       | string ≤ 200  | `Model`                      |
| SerialNumber      | no       | string ≤ 100  | `SerialNumber`               |
| TagNumber         | no       | string ≤ 50   | `TagNumber`                  |
| AcquisitionCost   | no       | decimal       | `AcquisitionCost`            |
| ReplacementCost   | no       | decimal       | `ReplacementCost`            |
| Currency          | no       | string(3)     | `Currency` (default CAD)     |
| PurchaseDate      | no       | date          | `PurchaseDate`               |
| InServiceDate     | no       | date          | `InServiceDate` (defaults to today if blank — Required on Asset entity) |
| FiscalPurchaseYear| no       | int           | `FiscalPurchaseYear`         |
| UsefulLifeMonths  | no       | int           | `UsefulLifeMonths`           |
| ImageUrl          | no       | string ≤ 500  | `ImageUrl`                   |
| LocationCode      | no       | string ≤ 50   | resolve → `LocationId`       |
| DepartmentCode    | no       | string ≤ 50   | resolve → `DepartmentId`     |
| SiteCode          | no       | string ≤ 50   | resolve → `SiteId`           |
| Status            | no       | string ≤ 20   | parse → `AssetStatus` (default Active) |

Unknown headers are ignored (logged in `Notes` on the batch).

## Validation rules

Per row, in order:
1. `AssetNumber` non-empty + ≤ 50 chars + no whitespace-only.
2. `AssetNumber` not already present in `Assets` for the same `CompanyId` (UNIQUE constraint check).
3. `AssetNumber` not duplicated within the same batch.
4. `Description` non-empty + ≤ 200 chars.
5. `AcquisitionCost` parses as decimal ≥ 0 if provided.
6. `ReplacementCost` parses as decimal ≥ 0 if provided.
7. `Currency`, if provided, is exactly 3 letters.
8. `PurchaseDate` / `InServiceDate` parse as date if provided.
9. `FiscalPurchaseYear` parses as int 1900..2100 if provided.
10. `UsefulLifeMonths` parses as int 0..1200 if provided.
11. `Status`, if provided, matches one of: `Active`, `FullyDepreciated`, `Disposed`, `Transferred`, `WrittenOff`, `Impaired`, `Held` (case-insensitive). Default = `Active` (= 0).
12. `LocationCode`/`DepartmentCode`/`SiteCode`, if provided, must resolve to an existing row in scope (CompanyId match). If not, error.
13. `Manufacturer`, if provided, MAY auto-create a new `Manufacturer` row on commit (tracked in audit). Resolution to FK happens at validate time so the preview shows the right ID.

Row status:
- All checks pass → `Valid`
- Any check fails → `Error` with concatenated reasons in `ValidationErrors`

Batch status:
- After validate: `Validated` (regardless of errors — commit is still allowed if ≥ 1 valid row)
- After commit: `Committed`
- After discard: `Discarded`

## Service contract

```csharp
public interface IAssetImportService
{
    Task<AssetImportBatch> ParseAndStageAsync(
        Stream excelStream, string fileName, long fileSizeBytes,
        int companyId, int? organizationId, int? siteId,
        int userId, CancellationToken ct);

    Task<AssetImportBatch> ValidateRowsAsync(int batchId, CancellationToken ct);

    Task<AssetImportBatch> CommitBatchAsync(int batchId, int userId, CancellationToken ct);

    Task<AssetImportBatch> DiscardBatchAsync(int batchId, int userId, CancellationToken ct);
}
```

- `ParseAndStageAsync` opens the workbook, reads headers, creates `AssetImportBatch` (status = Draft), creates one `AssetImportRow` per non-blank row, calls `ValidateRowsAsync` internally so the batch transitions to `Validated` before returning.
- `CommitBatchAsync` wraps the insert-many in a transaction. For each `Valid` row: creates `Manufacturer` if needed, then creates `Asset` with stamped tenant trio + resolved FKs + `CreatedBy` = userid. Updates row to `Committed` and stores `CommittedAssetId`. On any failure in the loop: rollback, batch status = `Failed`.
- `DiscardBatchAsync` flips batch status to `Discarded`. Rows are kept (audit).

## Page surfaces

- `/Admin/AssetImport` (Index)
  - Cockpit primitives: `_CockpitPageHeader` + `_CockpitKpiBand` (KPIs: Total batches, Committed, Draft, Failed)
  - List of recent batches (CreatedAt DESC, paged 50)
  - Buttons: "Upload Excel" → /Upload, "Download template" → ?handler=Template
  - Deep link from /Admin/DataImport's CSV Templates section

- `/Admin/AssetImport/Upload`
  - File-upload form (`.xlsx` only, max 10 MB)
  - Picker for CompanyId scope (default = current tenant)
  - Submit POST → calls `ParseAndStageAsync` → redirects to /Preview/{id}

- `/Admin/AssetImport/Preview/{id}`
  - Header: filename, total rows, valid/error counts, status chip
  - Tabbed (`_CockpitTabShell`): "All / Valid / Errors"
  - Per-row table: row#, AssetNumber, Description, Manufacturer (with resolved badge), AcquisitionCost, InServiceDate, errors-if-any
  - Buttons: "Commit valid rows" (disabled if 0 valid), "Re-validate", "Discard batch"

- `/Admin/AssetImport/Detail/{id}`
  - Read-only view of a Committed/Discarded/Failed batch
  - Same tabbed table; "Committed asset" column links to /Assets/Detail/{id}

## CHERRY025 control-plane

PageModels READ DbContext for display (list of batches, batch detail, row list). All
MUTATIONS go through `IAssetImportService`. Pages will need either:
- `[ControlPlaneExempt("reads batch/row state; mutations via IAssetImportService")]` on PageModel class, OR
- Move the read queries onto the service and have PageModels only call service methods.

Decision: option B (move reads onto service too). Cleaner.

## Audit emission

- `ParseAndStageAsync` → `AuditService.LogAsync(entityType=AssetImportBatch, action=Created, entityId=batch.Id, description=$"Parsed {row.Count} rows from {fileName}")`
- `CommitBatchAsync` → one log per batch (`action=Committed, description=$"Committed {n} assets"`) + one log per created Asset (`entityType=Asset, action=ImportCreated, refId=batch.Id`)
- `DiscardBatchAsync` → `action=Discarded`

## Tests

- `Tests/Services/AssetImport/AssetImportServiceTests.cs`
  - `ParseAndStage_HappyPath_CreatesBatchWithValidatedStatus`
  - `Commit_CreatesAssetsAndStampsTenantTrio`
  - `ReCommit_AfterFirstCommit_ThrowsOrIsRejected` (idempotency guard)

## Files touched

- NEW: `Models/AssetImport/AssetImportBatch.cs`
- NEW: `Models/AssetImport/AssetImportRow.cs`
- NEW: `Services/AssetImport/IAssetImportService.cs`
- NEW: `Services/AssetImport/AssetImportService.cs`
- NEW: `Migrations/{ts}_AddAssetImportTables_PR337.cs` + Designer.cs
- MODIFIED: `Migrations/AppDbContextModelSnapshot.cs` (auto by `dotnet ef migrations add`)
- MODIFIED: `Data/AppDbContext.cs` (+2 DbSet + OnModelCreating CHECKs)
- MODIFIED: `Program.cs` (DI registration)
- NEW: `Pages/Admin/AssetImport/Index.cshtml` + `.cs`
- NEW: `Pages/Admin/AssetImport/Upload.cshtml` + `.cs`
- NEW: `Pages/Admin/AssetImport/Preview.cshtml` + `.cs`
- NEW: `Pages/Admin/AssetImport/Detail.cshtml` + `.cs`
- NEW: `Pages/Admin/AssetImport/_AssetImportKpis.cshtml`
- MODIFIED: `Pages/Admin/DataImport.cshtml` (add "Bulk asset import" link to CSV Templates section)
- NEW: `wwwroot/data/templates/AssetImport.xlsx` (regenerated by handler at request time — no static file needed actually; will generate inline)
- NEW: `tests/Abs.FixedAssets.Tests/Services/AssetImport/AssetImportServiceTests.cs`

## Ship target

- Branch: `feature/pr337-asset-import`
- Commit subject (≤72 chars): `feat(admin): /Admin/AssetImport — Excel upload, preview, commit (PR #337)`
- E2E (Lock 9): after merge + Republish, generate ABS 25-asset .xlsx from the staged SQL,
  upload via the new feature, screenshot preview, commit, verify 25 new Asset rows on
  industryos.app via Replit My Data SQL console.
