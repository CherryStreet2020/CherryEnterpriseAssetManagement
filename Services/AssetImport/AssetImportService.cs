using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.AssetImport;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.AssetImport
{
    // ================================================================
    // Sprint 13.5 PR #337 — AssetImportService.
    //
    // Excel parsing pattern lifted from Services/MasterDataImportService
    // (same ClosedXML primitives). All MUTATIONS to the Assets table flow
    // through CommitBatchAsync — PageModels never touch Assets directly.
    //
    // Spec: docs/research/asset-import-pr337-spec-2026-05-25.md
    // ================================================================

    public class AssetImportService : IAssetImportService
    {
        private readonly AppDbContext _db;
        private readonly AuditService _audit;
        private readonly ILogger<AssetImportService> _log;

        // Required columns by canonical name. Lookup is case-insensitive
        // and whitespace-insensitive (NormalizeHeader collapses both).
        private static readonly string[] RequiredColumns =
        {
            "AssetNumber",
            "Description"
        };

        public AssetImportService(
            AppDbContext db,
            AuditService audit,
            ILogger<AssetImportService> log)
        {
            _db = db;
            _audit = audit;
            _log = log;
        }

        // -----------------------------------------------------------------
        // ParseAndStageAsync — read workbook, create batch, create rows,
        // then call ValidateRowsAsync internally so the batch transitions
        // to `Validated` before returning.
        // -----------------------------------------------------------------
        public async Task<AssetImportBatch> ParseAndStageAsync(
            Stream excelStream,
            string fileName,
            long fileSizeBytes,
            int companyId,
            int? organizationId,
            int? siteId,
            int userId,
            string? username,
            CancellationToken ct)
        {
            if (excelStream is null) throw new ArgumentNullException(nameof(excelStream));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName required", nameof(fileName));

            using var wb = new XLWorkbook(excelStream);
            var ws = wb.Worksheets.First();
            var headers = ReadHeaders(ws);
            var batch = new AssetImportBatch
            {
                CompanyId = companyId,
                OrganizationId = organizationId,
                SiteId = siteId,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                FileName = fileName.Length > 260 ? fileName.Substring(0, 260) : fileName,
                FileSizeBytes = fileSizeBytes,
                SheetName = ws.Name?.Length > 64 ? ws.Name.Substring(0, 64) : ws.Name,
                Status = AssetImportBatchStatus.Draft
            };

            var notesBuilder = new StringBuilder();

            if (headers.Count == 0)
            {
                notesBuilder.AppendLine("No header row detected — sheet appears empty.");
                batch.Notes = notesBuilder.ToString();
                _db.AssetImportBatches.Add(batch);
                await _db.SaveChangesAsync(ct);
                return await ValidateRowsAsync(batch.Id, ct);
            }

            // Surface unrecognized headers in batch.Notes so the operator knows
            // what was ignored.
            var recognized = AllSupportedColumns.Select(NormalizeHeader).ToHashSet();
            var unrecognized = headers
                .Where(h => !recognized.Contains(NormalizeHeader(h)))
                .ToList();
            if (unrecognized.Count > 0)
            {
                notesBuilder.AppendLine($"Ignored unrecognized columns: {string.Join(", ", unrecognized)}");
            }

            var missingRequired = RequiredColumns
                .Where(rc => !headers.Any(h => NormalizeHeader(h) == NormalizeHeader(rc)))
                .ToList();
            if (missingRequired.Count > 0)
            {
                notesBuilder.AppendLine($"Missing required columns: {string.Join(", ", missingRequired)}");
            }

            batch.Notes = notesBuilder.Length > 0 ? notesBuilder.ToString() : null;
            _db.AssetImportBatches.Add(batch);
            await _db.SaveChangesAsync(ct);

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            int rowsAdded = 0;
            for (int r = 2; r <= lastRow; r++)
            {
                var values = ReadRow(ws, r, headers.Count);
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                var map = BuildMap(headers, values);

                var row = new AssetImportRow
                {
                    BatchId = batch.Id,
                    RowNumber = r,
                    Status = AssetImportRowStatus.Pending,
                    AssetNumber = Trunc(Get(map, "AssetNumber"), 50),
                    Description = Trunc(Get(map, "Description"), 200),
                    LongDescription = Trunc(Get(map, "LongDescription"), 500),
                    Model = Trunc(Get(map, "Model"), 200),
                    SerialNumber = Trunc(Get(map, "SerialNumber"), 100),
                    TagNumber = Trunc(Get(map, "TagNumber"), 50),
                    ManufacturerName = Trunc(Get(map, "Manufacturer"), 200),
                    AcquisitionCost = TryDecimal(Get(map, "AcquisitionCost")),
                    ReplacementCost = TryDecimal(Get(map, "ReplacementCost")),
                    Currency = Trunc(Get(map, "Currency"), 3)?.ToUpperInvariant(),
                    PurchaseDate = TryDate(Get(map, "PurchaseDate")),
                    InServiceDate = TryDate(Get(map, "InServiceDate")),
                    FiscalPurchaseYear = TryInt(Get(map, "FiscalPurchaseYear")),
                    UsefulLifeMonths = TryInt(Get(map, "UsefulLifeMonths")),
                    ImageUrl = Trunc(Get(map, "ImageUrl"), 500),
                    LocationCode = Trunc(Get(map, "LocationCode"), 50),
                    DepartmentCode = Trunc(Get(map, "DepartmentCode"), 50),
                    SiteCode = Trunc(Get(map, "SiteCode"), 50),
                    StatusSource = Trunc(Get(map, "Status"), 20)
                };
                _db.AssetImportRows.Add(row);
                rowsAdded++;

                // Flush every 500 to keep change-tracker happy on large files
                if (rowsAdded % 500 == 0)
                {
                    await _db.SaveChangesAsync(ct);
                }
            }

            batch.RowCount = rowsAdded;
            await _db.SaveChangesAsync(ct);

            try
            {
                await _audit.LogAsync(
                    "AssetImportBatch.Created",
                    before: (object?)null,
                    after: new AuditSnapshot(batch.Id, batch.FileName, batch.RowCount, batch.Status.ToString()),
                    username: username,
                    description: $"Parsed {rowsAdded} rows from {fileName} for CompanyId={companyId}");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log failed for batch {BatchId} parse", batch.Id);
            }

            return await ValidateRowsAsync(batch.Id, ct);
        }

        // -----------------------------------------------------------------
        // ValidateRowsAsync — re-run validation across every row of the
        // batch. Idempotent. Resets row.Status from Pending/Valid/Error to
        // the freshly-computed value. Skips already-Committed rows.
        // -----------------------------------------------------------------
        public async Task<AssetImportBatch> ValidateRowsAsync(int batchId, CancellationToken ct)
        {
            var batch = await _db.AssetImportBatches
                .Include(b => b.Rows)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct)
                ?? throw new InvalidOperationException($"Batch {batchId} not found");

            if (batch.Status == AssetImportBatchStatus.Committed)
            {
                // Already committed — nothing to revalidate.
                return batch;
            }
            if (batch.Status == AssetImportBatchStatus.Discarded)
            {
                return batch;
            }

            // Preload AssetNumbers already in DB for this company to detect duplicates fast.
            var existingAssetNumbers = await _db.Assets
                .Where(a => a.CompanyId == batch.CompanyId)
                .Select(a => a.AssetNumber)
                .ToListAsync(ct);
            var existingSet = new HashSet<string>(existingAssetNumbers, StringComparer.OrdinalIgnoreCase);

            // Preload code lookups in scope.
            var manufacturers = await _db.Manufacturers
                .Select(m => new { m.Id, m.Code, m.Name })
                .ToListAsync(ct);
            var manufacturerByName = manufacturers
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var sites = await _db.Sites
                .Where(s => s.CompanyId == batch.CompanyId)
                .Select(s => new { s.Id, s.SiteCode })
                .ToListAsync(ct);
            var siteByCode = sites
                .Where(s => !string.IsNullOrEmpty(s.SiteCode))
                .GroupBy(s => s.SiteCode!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var departments = await _db.Departments
                .Where(d => d.CompanyId == batch.CompanyId)
                .Select(d => new { d.Id, d.Code })
                .ToListAsync(ct);
            var deptByCode = departments
                .Where(d => !string.IsNullOrEmpty(d.Code))
                .GroupBy(d => d.Code!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var locations = await _db.Locations
                .Where(l => l.CompanyId == batch.CompanyId)
                .Select(l => new { l.Id, l.Code })
                .ToListAsync(ct);
            var locByCode = locations
                .Where(l => !string.IsNullOrEmpty(l.Code))
                .GroupBy(l => l.Code!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            // Detect intra-batch duplicates.
            var intraBatchAssetNumbers = batch.Rows!
                .Where(r => !string.IsNullOrWhiteSpace(r.AssetNumber))
                .GroupBy(r => r.AssetNumber!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int validCount = 0;
            int errorCount = 0;
            foreach (var row in batch.Rows!)
            {
                if (row.Status == AssetImportRowStatus.Committed) continue;

                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(row.AssetNumber))
                {
                    errors.Add("AssetNumber: required");
                }
                else
                {
                    if (row.AssetNumber.Length > 50)
                        errors.Add("AssetNumber: exceeds 50 chars");
                    if (existingSet.Contains(row.AssetNumber))
                        errors.Add("AssetNumber: already exists for this Company");
                    if (intraBatchAssetNumbers.Contains(row.AssetNumber))
                        errors.Add("AssetNumber: duplicated within this batch");
                }

                if (string.IsNullOrWhiteSpace(row.Description))
                    errors.Add("Description: required");
                else if (row.Description.Length > 200)
                    errors.Add("Description: exceeds 200 chars");

                if (row.AcquisitionCost.HasValue && row.AcquisitionCost.Value < 0)
                    errors.Add("AcquisitionCost: must be >= 0");
                if (row.ReplacementCost.HasValue && row.ReplacementCost.Value < 0)
                    errors.Add("ReplacementCost: must be >= 0");

                if (!string.IsNullOrEmpty(row.Currency) && row.Currency.Length != 3)
                    errors.Add("Currency: must be exactly 3 letters");

                if (row.FiscalPurchaseYear.HasValue && (row.FiscalPurchaseYear < 1900 || row.FiscalPurchaseYear > 2100))
                    errors.Add("FiscalPurchaseYear: out of range (1900..2100)");

                if (row.UsefulLifeMonths.HasValue && (row.UsefulLifeMonths < 0 || row.UsefulLifeMonths > 1200))
                    errors.Add("UsefulLifeMonths: out of range (0..1200)");

                // Resolve FK lookups.
                row.ResolvedManufacturerId = null;
                if (!string.IsNullOrWhiteSpace(row.ManufacturerName)
                    && manufacturerByName.TryGetValue(row.ManufacturerName!, out var mfg))
                {
                    row.ResolvedManufacturerId = mfg.Id;
                }
                // (If manufacturer not found, we'll auto-create on commit. Not a validation error.)

                row.ResolvedSiteId = null;
                if (!string.IsNullOrWhiteSpace(row.SiteCode))
                {
                    if (siteByCode.TryGetValue(row.SiteCode!, out var sid)) row.ResolvedSiteId = sid;
                    else errors.Add($"SiteCode: '{row.SiteCode}' not found for CompanyId={batch.CompanyId}");
                }

                row.ResolvedDepartmentId = null;
                if (!string.IsNullOrWhiteSpace(row.DepartmentCode))
                {
                    if (deptByCode.TryGetValue(row.DepartmentCode!, out var did)) row.ResolvedDepartmentId = did;
                    else errors.Add($"DepartmentCode: '{row.DepartmentCode}' not found for CompanyId={batch.CompanyId}");
                }

                row.ResolvedLocationId = null;
                if (!string.IsNullOrWhiteSpace(row.LocationCode))
                {
                    if (locByCode.TryGetValue(row.LocationCode!, out var lid)) row.ResolvedLocationId = lid;
                    else errors.Add($"LocationCode: '{row.LocationCode}' not found for CompanyId={batch.CompanyId}");
                }

                // Parse status enum.
                row.ResolvedStatus = (int)AssetStatus.Active;
                if (!string.IsNullOrWhiteSpace(row.StatusSource))
                {
                    if (Enum.TryParse<AssetStatus>(row.StatusSource, ignoreCase: true, out var parsedStatus))
                        row.ResolvedStatus = (int)parsedStatus;
                    else
                        errors.Add($"Status: '{row.StatusSource}' is not a valid AssetStatus");
                }

                if (errors.Count == 0)
                {
                    row.Status = AssetImportRowStatus.Valid;
                    row.ValidationErrors = null;
                    validCount++;
                }
                else
                {
                    row.Status = AssetImportRowStatus.Error;
                    row.ValidationErrors = string.Join("\n", errors);
                    errorCount++;
                }
            }

            batch.ValidRowCount = validCount;
            batch.ErrorRowCount = errorCount;
            batch.ValidatedAt = DateTime.UtcNow;
            batch.Status = AssetImportBatchStatus.Validated;
            await _db.SaveChangesAsync(ct);
            return batch;
        }

        // -----------------------------------------------------------------
        // CommitBatchAsync — promote every Valid row into a real Asset.
        // Wraps in a transaction; on any per-row failure, rolls back and
        // marks the batch Failed (preserving rows for re-run).
        // -----------------------------------------------------------------
        public async Task<AssetImportBatch> CommitBatchAsync(int batchId, int userId, string? username, CancellationToken ct)
        {
            var batch = await _db.AssetImportBatches
                .Include(b => b.Rows)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct)
                ?? throw new InvalidOperationException($"Batch {batchId} not found");

            if (batch.Status == AssetImportBatchStatus.Committed)
                throw new InvalidOperationException($"Batch {batchId} is already Committed");
            if (batch.Status == AssetImportBatchStatus.Discarded)
                throw new InvalidOperationException($"Batch {batchId} was Discarded");
            if (batch.ValidRowCount == 0)
                throw new InvalidOperationException($"Batch {batchId} has 0 valid rows");

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            int committedCount = 0;
            int newManufacturers = 0;
            try
            {
                // Cache manufacturers we auto-create within this batch so
                // multiple rows asking for the same name reuse the new row.
                var newManufacturerByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in batch.Rows!.Where(r => r.Status == AssetImportRowStatus.Valid).OrderBy(r => r.RowNumber))
                {
                    var manufacturerId = row.ResolvedManufacturerId;
                    if (manufacturerId is null && !string.IsNullOrWhiteSpace(row.ManufacturerName))
                    {
                        if (newManufacturerByName.TryGetValue(row.ManufacturerName!, out var existingNewId))
                        {
                            manufacturerId = existingNewId;
                        }
                        else
                        {
                            var newMfg = new Manufacturer
                            {
                                Code = GenerateManufacturerCode(row.ManufacturerName!, _db, newManufacturerByName.Values.ToHashSet()),
                                Name = row.ManufacturerName!.Length > 100 ? row.ManufacturerName.Substring(0, 100) : row.ManufacturerName,
                                Active = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.Manufacturers.Add(newMfg);
                            await _db.SaveChangesAsync(ct);
                            manufacturerId = newMfg.Id;
                            newManufacturerByName[row.ManufacturerName!] = newMfg.Id;
                            newManufacturers++;
                        }
                    }

                    var asset = new Asset
                    {
                        AssetNumber = row.AssetNumber!.Trim(),
                        Description = row.Description!.Trim(),
                        LongDescription = row.LongDescription,
                        Model = row.Model,
                        SerialNumber = row.SerialNumber,
                        TagNumber = row.TagNumber,
                        ImageUrl = row.ImageUrl,
                        ManufacturerId = manufacturerId,
                        AcquisitionCost = row.AcquisitionCost ?? 0m,
                        ReplacementCost = row.ReplacementCost ?? 0m,
                        Currency = !string.IsNullOrEmpty(row.Currency) ? row.Currency! : "USD",
                        PurchaseDate = row.PurchaseDate,
                        InServiceDate = row.InServiceDate ?? DateTime.UtcNow.Date,
                        FiscalPurchaseYear = row.FiscalPurchaseYear ?? row.InServiceDate?.Year,
                        UsefulLifeMonths = row.UsefulLifeMonths ?? 0,
                        SiteId = row.ResolvedSiteId ?? batch.SiteId,
                        DepartmentId = row.ResolvedDepartmentId,
                        LocationId = row.ResolvedLocationId,
                        CompanyId = batch.CompanyId,
                        Status = (AssetStatus)(row.ResolvedStatus ?? (int)AssetStatus.Active),
                        Active = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = username
                    };

                    _db.Assets.Add(asset);
                    await _db.SaveChangesAsync(ct);

                    row.Status = AssetImportRowStatus.Committed;
                    row.CommittedAssetId = asset.Id;
                    committedCount++;
                }

                batch.Status = AssetImportBatchStatus.Committed;
                batch.CommittedAt = DateTime.UtcNow;
                batch.CommittedByUserId = userId;
                await _db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                batch.Status = AssetImportBatchStatus.Failed;
                batch.Notes = (batch.Notes ?? string.Empty) + $"\nCommit failed at {DateTime.UtcNow:o}: {ex.Message}";
                await _db.SaveChangesAsync(ct);
                _log.LogError(ex, "AssetImport batch {BatchId} commit failed", batch.Id);
                throw;
            }

            try
            {
                await _audit.LogAsync(
                    "AssetImportBatch.Committed",
                    before: (object?)null,
                    after: new AuditSnapshot(batch.Id, batch.FileName, committedCount, batch.Status.ToString()),
                    username: username,
                    description: $"Committed {committedCount} assets ({newManufacturers} new manufacturers) from batch {batch.Id}");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log failed for batch {BatchId} commit", batch.Id);
            }

            return batch;
        }

        // -----------------------------------------------------------------
        // DiscardBatchAsync — flip status to Discarded. Keeps rows for audit.
        // -----------------------------------------------------------------
        public async Task<AssetImportBatch> DiscardBatchAsync(int batchId, int userId, string? username, CancellationToken ct)
        {
            var batch = await _db.AssetImportBatches
                .FirstOrDefaultAsync(b => b.Id == batchId, ct)
                ?? throw new InvalidOperationException($"Batch {batchId} not found");

            if (batch.Status == AssetImportBatchStatus.Committed)
                throw new InvalidOperationException($"Batch {batchId} is Committed — cannot discard");

            batch.Status = AssetImportBatchStatus.Discarded;
            batch.DiscardedAt = DateTime.UtcNow;
            batch.DiscardedByUserId = userId;
            await _db.SaveChangesAsync(ct);

            try
            {
                await _audit.LogAsync(
                    "AssetImportBatch.Discarded",
                    before: (object?)null,
                    after: new AuditSnapshot(batch.Id, batch.FileName, batch.RowCount, batch.Status.ToString()),
                    username: username,
                    description: $"Discarded batch {batch.Id}");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log failed for batch {BatchId} discard", batch.Id);
            }

            return batch;
        }

        // -----------------------------------------------------------------
        // Reads
        // -----------------------------------------------------------------
        public async Task<IReadOnlyList<AssetImportBatch>> ListRecentAsync(int companyId, int limit, CancellationToken ct)
        {
            return await _db.AssetImportBatches
                .AsNoTracking()
                .Where(b => b.CompanyId == companyId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }

        public async Task<AssetImportBatch?> GetBatchAsync(int batchId, CancellationToken ct)
        {
            return await _db.AssetImportBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);
        }

        public async Task<IReadOnlyList<AssetImportRow>> GetRowsAsync(int batchId, CancellationToken ct)
        {
            return await _db.AssetImportRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.RowNumber)
                .ToListAsync(ct);
        }

        public async Task<AssetImportKpis> GetKpisAsync(int companyId, CancellationToken ct)
        {
            var batches = await _db.AssetImportBatches
                .AsNoTracking()
                .Where(b => b.CompanyId == companyId)
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int total = batches.Sum(b => b.Count);
            int committed = batches.FirstOrDefault(b => b.Status == AssetImportBatchStatus.Committed)?.Count ?? 0;
            int draft = batches.Where(b => b.Status == AssetImportBatchStatus.Draft || b.Status == AssetImportBatchStatus.Validated).Sum(b => b.Count);
            int failed = batches.Where(b => b.Status == AssetImportBatchStatus.Failed || b.Status == AssetImportBatchStatus.Discarded).Sum(b => b.Count);
            return new AssetImportKpis(total, committed, draft, failed);
        }

        // -----------------------------------------------------------------
        // GenerateTemplate — produce a starter .xlsx with header row + 1 sample row.
        // -----------------------------------------------------------------
        public byte[] GenerateTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Assets");
            int col = 1;
            foreach (var h in AllSupportedColumns)
            {
                ws.Cell(1, col).Value = h;
                ws.Cell(1, col).Style.Font.Bold = true;
                col++;
            }

            // One realistic sample row so operators see what shape to provide.
            ws.Cell(2, 1).Value = "DEMO-001";
            ws.Cell(2, 2).Value = "Sample Asset — replace this row";
            ws.Cell(2, 3).Value = "Long descriptive text (optional)";
            ws.Cell(2, 4).Value = "MAZAK";
            ws.Cell(2, 5).Value = "VARIAXIS-730";
            ws.Cell(2, 6).Value = "SN-12345";
            ws.Cell(2, 7).Value = "TAG-001";
            ws.Cell(2, 8).Value = 250000m;
            ws.Cell(2, 9).Value = 320000m;
            ws.Cell(2, 10).Value = "USD";
            ws.Cell(2, 11).Value = DateTime.UtcNow.Date.AddYears(-3);
            ws.Cell(2, 12).Value = DateTime.UtcNow.Date.AddYears(-3);
            ws.Cell(2, 13).Value = DateTime.UtcNow.Year - 3;
            ws.Cell(2, 14).Value = 84;
            ws.Cell(2, 15).Value = "/uploads/abs-assets/mazak-5axis.jpg";
            ws.Cell(2, 16).Value = "MISS-MAIN";
            ws.Cell(2, 17).Value = "MACH-SHOP";
            ws.Cell(2, 18).Value = "MISS";
            ws.Cell(2, 19).Value = "Active";

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private static readonly string[] AllSupportedColumns =
        {
            "AssetNumber",
            "Description",
            "LongDescription",
            "Manufacturer",
            "Model",
            "SerialNumber",
            "TagNumber",
            "AcquisitionCost",
            "ReplacementCost",
            "Currency",
            "PurchaseDate",
            "InServiceDate",
            "FiscalPurchaseYear",
            "UsefulLifeMonths",
            "ImageUrl",
            "LocationCode",
            "DepartmentCode",
            "SiteCode",
            "Status"
        };

        private static List<string> ReadHeaders(IXLWorksheet ws)
        {
            var headers = new List<string>();
            var row = ws.Row(1);
            for (int c = 1; c <= 50; c++)
            {
                var val = row.Cell(c).GetString().Trim();
                if (string.IsNullOrEmpty(val))
                {
                    // Allow occasional gaps but stop after 2 in a row.
                    var next = row.Cell(c + 1).GetString().Trim();
                    if (string.IsNullOrEmpty(next)) break;
                    headers.Add(""); // placeholder so column index stays aligned
                    continue;
                }
                headers.Add(val);
            }
            return headers;
        }

        private static List<string> ReadRow(IXLWorksheet ws, int rowNum, int colCount)
        {
            var values = new List<string>(colCount);
            for (int c = 1; c <= colCount; c++)
            {
                values.Add(ws.Cell(rowNum, c).GetString().Trim());
            }
            return values;
        }

        private static Dictionary<string, string> BuildMap(List<string> headers, List<string> values)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count && i < values.Count; i++)
            {
                if (string.IsNullOrEmpty(headers[i])) continue;
                map[NormalizeHeader(headers[i])] = values[i];
            }
            return map;
        }

        private static string NormalizeHeader(string h)
            => h.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

        private static string Get(Dictionary<string, string> map, string canonicalKey)
        {
            return map.TryGetValue(NormalizeHeader(canonicalKey), out var v) ? v : string.Empty;
        }

        private static string? Trunc(string? s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length > maxLen ? s.Substring(0, maxLen) : s;
        }

        private static decimal? TryDecimal(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = s.Replace("$", "").Replace(",", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static int? TryInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : null;
        }

        private static DateTime? TryDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }

        private static string GenerateManufacturerCode(string name, AppDbContext db, HashSet<int> excludedIds)
        {
            // Strategy: first word, alphanumeric only, uppercase, max 20 chars.
            // Disambiguate by suffixing -2, -3, ... if collision.
            var firstWord = new string(name.Trim().Split(new[] { ' ', '\t', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Where(char.IsLetterOrDigit).ToArray() ?? Array.Empty<char>())
                .ToUpperInvariant();
            if (string.IsNullOrEmpty(firstWord)) firstWord = "MFG";
            if (firstWord.Length > 18) firstWord = firstWord.Substring(0, 18);

            var existingCodes = db.Manufacturers
                .Where(m => m.Code.StartsWith(firstWord))
                .Select(m => m.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingCodes.Contains(firstWord)) return firstWord;
            for (int i = 2; i < 1000; i++)
            {
                var candidate = $"{firstWord}-{i}";
                if (candidate.Length > 20) candidate = candidate.Substring(0, 20);
                if (!existingCodes.Contains(candidate)) return candidate;
            }
            return $"{firstWord}-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()}";
        }

        // Flat DTO for AuditService (per hard-rules — never pass bidirectional EF entities).
        private sealed record AuditSnapshot(int BatchId, string FileName, int RowCount, string Status);
    }
}
