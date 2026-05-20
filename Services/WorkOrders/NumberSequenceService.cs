using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.5 — Atomic WO-number generator implementation.
    //
    // SELECT FOR UPDATE pattern keyed by (Classification, Year, TenantId).
    // Postgres locks the row exclusively for the duration of the
    // transaction; any concurrent NextAsync for the same bucket waits
    // until this transaction commits, then sees the incremented
    // CurrentValue.
    //
    // Year rollover: if no row exists for the current year, NextAsync
    // creates one (initial CurrentValue=0, then increments to 1). Once
    // created, subsequent calls reuse it for the rest of the year.
    //
    // Tenant-override merge: a NULL-tenant (global) row provides the
    // Prefix/Padding/Separator config; a non-NULL-tenant row inherits
    // its config from the global row unless explicitly overridden.
    // The lookup tries (Classification, Year, tenantId) first, then
    // (Classification, Year, NULL) as fallback.
    public class NumberSequenceService : INumberSequenceService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<NumberSequenceService> _logger;

        public NumberSequenceService(
            AppDbContext db,
            ILogger<NumberSequenceService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<string> NextAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default)
        {
            var year = DateTime.UtcNow.Year;

            // Open an explicit transaction so SELECT FOR UPDATE has
            // somewhere to live. Postgres needs a TX to honor the row
            // lock; auto-commit defeats it.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Look up the row for (classification, year, tenantId).
            // Falls back to global (TenantId NULL) when no tenant row
            // exists.
            var row = await _db.Set<NumberSequence>()
                .FromSqlRaw(
                    @"SELECT * FROM ""NumberSequence""
                      WHERE ""Classification"" = {0}
                        AND ""Year"" = {1}
                        AND (""TenantId"" = {2} OR (""TenantId"" IS NULL AND {2} IS NULL))
                      ORDER BY ""TenantId"" NULLS LAST
                      LIMIT 1
                      FOR UPDATE",
                    (short)(int)classification, year, tenantId)
                .FirstOrDefaultAsync(ct);

            if (row == null)
            {
                // No row for this year yet — create one. First check if
                // a prior-year row exists for this classification to
                // inherit its formatting (Prefix, Padding, etc.).
                var template = await _db.Set<NumberSequence>()
                    .AsNoTracking()
                    .Where(r => r.Classification == classification
                             && (r.TenantId == tenantId || r.TenantId == null))
                    .OrderByDescending(r => r.TenantId == tenantId)  // tenant first
                    .ThenByDescending(r => r.Year)
                    .FirstOrDefaultAsync(ct);

                row = new NumberSequence
                {
                    Classification = classification,
                    Year = year,
                    Prefix = template?.Prefix ?? DefaultPrefix(classification),
                    CurrentValue = 0,
                    Padding = template?.Padding ?? 4,
                    YearSeparator = template?.YearSeparator ?? "-",
                    CounterSeparator = template?.CounterSeparator ?? "-",
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.Set<NumberSequence>().Add(row);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "NumberSequence: created new row for ({Cls}, {Year}, tenant={TenantId}) with prefix '{Prefix}'.",
                    classification, year, tenantId, row.Prefix);
            }

            // Increment + persist.
            row.CurrentValue += 1;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            // Format: Prefix{YearSep}Year{CounterSep}{Padded CurrentValue}
            var counter = row.CurrentValue.ToString().PadLeft(row.Padding, '0');
            var formatted = $"{row.Prefix}{row.YearSeparator}{row.Year}{row.CounterSeparator}{counter}";
            return formatted;
        }

        // Default prefixes per classification — matches the OrderTypeLabels
        // ShortCode (Services/Naming/OrderTypeLabels.cs) so the human-facing
        // record number lines up with the displayed label:
        //   Classification=Maintenance → "MO-2026-NNNN" + "Maintenance Order"
        //   Classification=Quality     → "QO-2026-NNNN" + "Quality Order"
        //   Classification=Engineering → "EO-2026-NNNN" + "Engineering Order"
        //   Classification=HSE         → "HSE-2026-NNNN" + "HSE Order"
        //   Classification=CIP         → "CIP-2026-NNNN" + "CIP Order"
        //
        // Override per-tenant via seeded NumberSequence rows with custom Prefix
        // (Joe/EVS can override Maintenance→"JO" for the metal-fab "Job Order"
        // convention; FSC/food can override CIP→"AFE" for Authorization For
        // Expenditure; etc.). The per-tenant lookup beats the global default.
        //
        // Production Orders live in the sibling ProductionOrder entity (not
        // routed through this service today). When the Production Control
        // Center sprint wires its numbering, the agreed default is "PRO-"
        // (avoids the PurchaseOrder "PO-" collision; per-tenant override
        // supports "JO"/"MFG"/"BO"/etc. for vertical-specific terms).
        //
        // ADR-025 sibling rename, 2026-05-20 (Dean's call).
        public const string ProductionOrderDefaultPrefix = "PRO";

        private static string DefaultPrefix(WorkOrderClassification cls) => cls switch
        {
            WorkOrderClassification.Maintenance  => "MO",
            WorkOrderClassification.Quality      => "QO",
            WorkOrderClassification.Engineering  => "EO",
            WorkOrderClassification.HSE          => "HSE",
            WorkOrderClassification.CIP          => "CIP",
            _ => "WO",
        };
    }
}
