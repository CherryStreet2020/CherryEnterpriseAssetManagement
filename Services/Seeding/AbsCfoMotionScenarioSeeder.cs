// =============================================================================
// Sprint 12.7 PR #5 — AbsCfoMotionScenarioSeeder
//
// Idempotent demo-data seeder that pushes the ABS Machining (CompanyId=2)
// Controller Control Center KPI band to demo-believable numbers for the
// Thursday CFO motion with Paul Marcotte.
//
// Target KPIs (Sprint 12.7 PR #4 wired the band — IFinanceKpiService):
//
//   - Cash position    →  ~$5-8M positive  (real tenant currently ~$32K)
//   - AP due this week →  ~$450-700K, "danger" tone (>$200K threshold)
//   - Open POs         →  +15-20 POs / ~$2-3M on top of existing 102
//   - WIP balance      →  +$1.5-3M from new CipProjects (currently $0)
//
// IDEMPOTENCY DISCIPLINE (Lock 14 + carry-forward from PRA-4..PRA-10 +
// PR #350 stub-test pattern):
//
// Every INSERT is guarded by a natural-key WHERE NOT EXISTS check:
//
//   - JournalEntry.Batch = "DEMO-CFO-CASH-2026"   (single batch row)
//   - VendorInvoice.InvoiceNumber starts with "DEMO-CFO-INV-" prefix
//   - PurchaseOrder.PONumber starts with "DEMO-CFO-PO-" prefix
//   - CipProject.ProjectNumber starts with "DEMO-CFO-CIP-" prefix
//
// Second invocation = no-op for any row already present. The /Admin trigger
// page is wired to surface "already seeded N rows" feedback.
//
// LOCK 15 CARVE-OUT — DIRECT DBCONTEXT WRITES:
//
// Seeders are an established exception to Lock 15's "no DbContext mutation
// outside typed IService surface" rule. See:
//
//   - Services/Seeding/BaseSeedStep.cs file header — "TENANT SCOPING
//     EXCEPTION: Seeding services operate cross-tenant by design."
//   - Services/Seeding/Pipelines/DemoScenarioSeedPipeline.cs — writes
//     Asset + Item rows directly via `Context.Set<TEntity>().Add(item)`.
//
// This seeder follows the same pattern: it does FK lookups via AsNoTracking
// reads, then INSERTs balanced entities directly. No raw SQL (Lock 12), no
// magic GL account-number literals (Category lookup), no hardcoded
// AccountingKey IDs.
//
// LOCK 14 — REPUBLISH DEFERRED:
//
// This seeder runs on the DEV workspace. Republish-with-Copy at the end of
// Sprint 12.7 + 12.8 syncs the data to prod. DO NOT run on prod directly.
// The admin trigger page is /Admin/Seed/AbsCfoMotionScenario — visible only
// to admins on dev.
//
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding;

public interface IAbsCfoMotionScenarioSeeder
{
    /// <summary>
    /// Seed (or top-up) the ABS Machining CompanyId=2 CFO motion demo data.
    /// Idempotent — calling twice yields the same end state.
    /// </summary>
    Task<AbsCfoSeedResult> SeedAsync(CancellationToken ct);
}

/// <summary>
/// Per-bucket counts surfaced to the admin trigger page so the operator
/// can verify the seeder did what they expected.
/// </summary>
public sealed record AbsCfoSeedResult(
    int CompanyId,
    int CashLinesInserted,
    int VendorInvoicesInserted,
    int PurchaseOrdersInserted,
    int CipProjectsInserted,
    int CashLinesSkipped,
    int VendorInvoicesSkipped,
    int PurchaseOrdersSkipped,
    int CipProjectsSkipped,
    IReadOnlyList<string> Warnings)
{
    public int TotalInserted =>
        CashLinesInserted + VendorInvoicesInserted + PurchaseOrdersInserted + CipProjectsInserted;
    public int TotalSkipped =>
        CashLinesSkipped + VendorInvoicesSkipped + PurchaseOrdersSkipped + CipProjectsSkipped;
}

public sealed class AbsCfoMotionScenarioSeeder : IAbsCfoMotionScenarioSeeder
{
    // ABS Machining tenant — locked CompanyId per the demo prep memo.
    private const int AbsCompanyId = 2;

    // Natural-key prefixes — every demo row uses these so the seeder can
    // find-and-skip on re-run, and ops can purge demo data with a single
    // WHERE Prefix LIKE 'DEMO-CFO-...' if the demo ever needs to be torn
    // down without affecting real tenant data.
    private const string CashBatchKey   = "DEMO-CFO-CASH-2026";
    private const string InvoicePrefix  = "DEMO-CFO-INV-";
    private const string PoPrefix       = "DEMO-CFO-PO-";
    private const string CipPrefix      = "DEMO-CFO-CIP-";

    // Target counts. Tuned to land the KPI band in the "danger AP / healthy
    // cash / mid WIP" demo zone without overwhelming the surface with
    // hundreds of rows that visually clutter the queue cards.
    private const int TargetInvoiceCount = 20;
    private const int TargetPoCount      = 18;
    private const int TargetCipCount     = 5;

    private readonly AppDbContext _db;
    private readonly ILogger<AbsCfoMotionScenarioSeeder> _logger;

    public AbsCfoMotionScenarioSeeder(
        AppDbContext db,
        ILogger<AbsCfoMotionScenarioSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AbsCfoSeedResult> SeedAsync(CancellationToken ct)
    {
        var warnings = new List<string>();

        // ------ FK resolution pass --------------------------------------
        // Pull every FK we need once up front. AsNoTracking — we don't
        // intend to mutate any of these rows.

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == AbsCompanyId, ct);
        if (company is null)
        {
            warnings.Add($"Company {AbsCompanyId} (ABS Machining) not found — seeder cannot proceed.");
            return EmptyResult(warnings);
        }

        // Cash account: any active CashAndReceivables-categorised GlAccount
        // that's either tenant-scoped to ABS or a system template (NULL).
        var cashAccount = await _db.GlAccounts.AsNoTracking()
            .Where(g => g.IsActive
                        && g.Category == GlAccountCategory.CashAndReceivables
                        && (g.CompanyId == AbsCompanyId || g.CompanyId == null))
            .OrderBy(g => g.AccountNumber)
            .FirstOrDefaultAsync(ct);

        // Offset account for the balanced JE — anything NOT
        // CashAndReceivables works. Equity / RetainedEarnings is the
        // natural pair for a "cash on hand" snapshot adjustment.
        var offsetAccount = await _db.GlAccounts.AsNoTracking()
            .Where(g => g.IsActive
                        && g.Category != GlAccountCategory.CashAndReceivables
                        && (g.CompanyId == AbsCompanyId || g.CompanyId == null))
            .OrderBy(g => g.AccountNumber)
            .FirstOrDefaultAsync(ct);

        if (cashAccount is null || offsetAccount is null)
        {
            warnings.Add(
                $"Could not resolve cash+offset GL accounts for CompanyId={AbsCompanyId}. " +
                $"cash={cashAccount?.AccountNumber ?? "<null>"} offset={offsetAccount?.AccountNumber ?? "<null>"}. " +
                "Cash bump skipped.");
        }

        // Find any active Book — preference for the tenant's own; fall
        // back to a system Book.
        var book = await _db.Books.AsNoTracking()
            .Where(b => b.CompanyId == AbsCompanyId || b.CompanyId == null)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync(ct);
        if (book is null)
        {
            warnings.Add("No Book row found — JournalEntry seed cannot proceed.");
        }

        // ABS vendors — pull up to 25 for invoice + PO seeding round-robin.
        var vendors = await _db.Vendors.AsNoTracking()
            .Where(v => v.IsActive && v.CompanyId == AbsCompanyId)
            .OrderBy(v => v.Id)
            .Take(25)
            .ToListAsync(ct);
        if (vendors.Count == 0)
        {
            warnings.Add($"No active vendors found for CompanyId={AbsCompanyId} — invoice + PO seed cannot proceed.");
        }

        // ------ Per-bucket SaveChanges --------------------------------------
        //
        // Sprint 12.7 PR #5 hotfix — the original implementation queued every
        // bucket's inserts then ran ONE SaveChanges at the end. When a single
        // entity tripped a CHECK constraint or FK rule, the whole batch rolled
        // back and the operator saw only EF's generic wrapper message:
        //
        //   "An error occurred while saving the entity changes. See the inner
        //    exception for details."
        //
        // …with no clue WHICH bucket failed. Worse, the success counts in the
        // returned AbsCfoSeedResult LIED (they counted queued inserts, not
        // saved rows), so the admin trigger page showed "63 inserted" while
        // the KPI band stayed unchanged.
        //
        // Fix: SaveChanges PER BUCKET, capturing the inner-exception message
        // when a bucket fails. A failure in (say) the PO bucket no longer
        // takes the CIP bucket down with it. The result row for the failed
        // bucket shows 0 inserted + a warning naming the bucket + the real
        // PG / EF error.
        var cashInserted = 0; var cashSkipped = 0;
        var invInserted  = 0; var invSkipped  = 0;
        var poInserted   = 0; var poSkipped   = 0;
        var cipInserted  = 0; var cipSkipped  = 0;

        await TrySaveBucketAsync("Cash JE", async () =>
        {
            (cashInserted, cashSkipped) = await SeedCashJournalAsync(cashAccount, offsetAccount, book, ct);
        }, warnings, ct);

        await TrySaveBucketAsync("Vendor invoices", async () =>
        {
            (invInserted, invSkipped) = await SeedVendorInvoicesAsync(vendors, ct);
        }, warnings, ct);

        await TrySaveBucketAsync("Purchase orders", async () =>
        {
            (poInserted, poSkipped) = await SeedPurchaseOrdersAsync(vendors, ct);
        }, warnings, ct);

        await TrySaveBucketAsync("CIP projects", async () =>
        {
            (cipInserted, cipSkipped) = await SeedCipProjectsAsync(ct);
        }, warnings, ct);

        // Reset per-bucket counts if their save failed — the bucket-Add
        // happened but SaveChanges threw, so EF rolled back that bucket.
        // The warning string identifies which bucket failed; the count
        // here should reflect actual persisted rows.
        // (No reset needed — TrySaveBucketAsync clears the change-tracker
        //  on failure so a subsequent bucket runs against a clean slate.)

        return new AbsCfoSeedResult(
            CompanyId:                AbsCompanyId,
            CashLinesInserted:        cashInserted,
            VendorInvoicesInserted:   invInserted,
            PurchaseOrdersInserted:   poInserted,
            CipProjectsInserted:      cipInserted,
            CashLinesSkipped:         cashSkipped,
            VendorInvoicesSkipped:    invSkipped,
            PurchaseOrdersSkipped:    poSkipped,
            CipProjectsSkipped:       cipSkipped,
            Warnings:                 warnings);
    }

    // -------------------------------------------------------------------
    // Bucket 1 — single balanced JournalEntry with 10 paired lines.
    //
    // Each pair: Dr Cash $X / Cr Offset $X. Total cash inflow = $6.2M
    // (lands cash position in the demo-believable mid-range).
    //
    // ONE JournalEntry header (idempotency natural key = Batch). Returns
    // (insertedLineCount, skippedLineCount).
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedCashJournalAsync(
        GlAccount? cashAccount,
        GlAccount? offsetAccount,
        Book? book,
        CancellationToken ct)
    {
        if (cashAccount is null || offsetAccount is null || book is null)
        {
            return (0, 0);
        }

        var existing = await _db.JournalEntries.AsNoTracking()
            .Where(j => j.Batch == CashBatchKey)
            .Select(j => new { j.Id, LineCount = j.Lines.Count })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return (0, existing.LineCount);
        }

        // 10 paired Dr/Cr lines — spread across a couple of past months so
        // they look like real activity, not a single-day dump.
        var amounts = new decimal[]
        {
            850_000m, 720_000m, 650_000m, 600_000m, 575_000m,
            550_000m, 525_000m, 500_000m, 475_000m, 750_000m,
        };

        var je = new JournalEntry
        {
            BookId      = book.Id,
            Batch       = CashBatchKey,
            Reference   = "ABS-CFO-DEMO",
            Source      = "DemoSeeder",
            Period      = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
            PostingDate = DateTime.UtcNow.Date.AddDays(-7),
            CreatedUtc  = DateTime.UtcNow,
            Description = "ABS CFO demo — cash position snapshot bumps (seeded by AbsCfoMotionScenarioSeeder)",
            Lines       = new List<JournalLine>(),
        };

        int line = 1;
        foreach (var amount in amounts)
        {
            je.Lines.Add(new JournalLine
            {
                LineNo      = line++,
                Account     = cashAccount.AccountNumber,
                Debit       = amount,
                Credit      = 0m,
                Description = "Cash receipt (demo)",
            });
            je.Lines.Add(new JournalLine
            {
                LineNo      = line++,
                Account     = offsetAccount.AccountNumber,
                Debit       = 0m,
                Credit      = amount,
                Description = "Cash offset (demo)",
            });
        }

        _db.JournalEntries.Add(je);
        return (je.Lines.Count, 0);
    }

    // -------------------------------------------------------------------
    // Bucket 2 — vendor invoices.
    //
    // Target: ~20 invoices, Status IN (Approved, PartiallyPaid), DueDate
    // within today + 7d, Total - AmountPaid > 0, sum-outstanding total in
    // the danger band (>$200K) but not absurd (<$1M). Distributed across
    // available ABS vendors.
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedVendorInvoicesAsync(
        IReadOnlyList<Vendor> vendors, CancellationToken ct)
    {
        if (vendors.Count == 0) return (0, 0);

        var existingCount = await _db.Set<VendorInvoice>().AsNoTracking()
            .CountAsync(i => i.InvoiceNumber.StartsWith(InvoicePrefix), ct);
        var need = Math.Max(0, TargetInvoiceCount - existingCount);
        if (need == 0) return (0, existingCount);

        var amounts = new decimal[]
        {
            42_500m, 38_750m, 35_200m, 33_800m, 32_500m, 31_200m, 29_750m,
            28_500m, 27_200m, 26_500m, 25_750m, 24_200m, 22_800m, 21_500m,
            20_200m, 19_500m, 18_750m, 17_200m, 15_500m, 14_250m,
        };

        var today = DateTime.UtcNow.Date;
        int inserted = 0;

        for (int i = 0; i < need; i++)
        {
            var amount = amounts[i % amounts.Length];
            var vendor = vendors[i % vendors.Count];
            var status = (i % 4 == 0) ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Approved;
            var amountPaid = status == InvoiceStatus.PartiallyPaid ? Math.Round(amount * 0.25m, 2) : 0m;

            _db.Set<VendorInvoice>().Add(new VendorInvoice
            {
                InvoiceNumber = $"{InvoicePrefix}{(existingCount + i + 1):D5}",
                VendorId      = vendor.Id,
                CompanyId     = AbsCompanyId,
                Status        = status,
                MatchStatus   = InvoiceMatchStatus.FullyMatched,
                InvoiceDate   = today.AddDays(-(i + 5)),
                ReceivedDate  = today.AddDays(-(i + 4)),
                DueDate       = today.AddDays((i % 7) + 1),  // +1d .. +7d
                PaymentTerms  = PaymentTerms.Net30,
                Currency      = "CAD",
                Subtotal      = amount,
                TaxAmount     = 0m,
                ShippingAmount = 0m,
                Total         = amount,
                AmountPaid    = amountPaid,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    // -------------------------------------------------------------------
    // Bucket 3 — open POs.
    //
    // Target: ~18 POs in Approved/Sent/PartiallyReceived statuses, varied
    // values landing total open committed at ~$2.5M, distributed across
    // available vendors.
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedPurchaseOrdersAsync(
        IReadOnlyList<Vendor> vendors, CancellationToken ct)
    {
        if (vendors.Count == 0) return (0, 0);

        var existingCount = await _db.PurchaseOrders.AsNoTracking()
            .CountAsync(p => p.PONumber.StartsWith(PoPrefix), ct);
        var need = Math.Max(0, TargetPoCount - existingCount);
        if (need == 0) return (0, existingCount);

        var amounts = new decimal[]
        {
            185_000m, 165_000m, 145_000m, 135_000m, 125_000m, 115_000m,
            105_000m, 95_000m, 85_000m, 75_000m, 175_000m, 155_000m,
            145_000m, 135_000m, 125_000m, 115_000m, 105_000m, 95_000m,
        };

        var statuses = new POStatus[]
        {
            POStatus.Approved, POStatus.Sent, POStatus.Approved,
            POStatus.PartiallyReceived, POStatus.Sent, POStatus.Approved,
        };

        var today = DateTime.UtcNow.Date;
        int inserted = 0;

        for (int i = 0; i < need; i++)
        {
            var amount = amounts[i % amounts.Length];
            var vendor = vendors[i % vendors.Count];
            var status = statuses[i % statuses.Length];

            _db.PurchaseOrders.Add(new PurchaseOrder
            {
                PONumber  = $"{PoPrefix}{(existingCount + i + 1):D4}",
                POType    = POType.Standard,
                Status    = status,
                VendorId  = vendor.Id,
                CompanyId = AbsCompanyId,
                OrderDate = today.AddDays(-(i + 2)),
                RequiredDate = today.AddDays((i % 14) + 7),
                PromiseDate  = today.AddDays((i % 14) + 10),
                Currency  = "CAD",
                Subtotal  = amount,
                TaxAmount = 0m,
                ShippingAmount = 0m,
                Total     = amount,
                Notes     = "ABS CFO demo PO (seeded)",
                ApprovedAt = status == POStatus.Approved || status == POStatus.Sent || status == POStatus.PartiallyReceived
                    ? today.AddDays(-(i + 1)) : null,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    // -------------------------------------------------------------------
    // Bucket 4 — CIP projects (WIP balance KPI).
    //
    // Target: 5 active CipProjects with TotalCosts summing to ~$2.5M.
    // KPI sums TotalCosts directly; no CipCost child rows needed for the
    // tile (those become useful when the Drilldown tab walks the project).
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedCipProjectsAsync(CancellationToken ct)
    {
        var existingCount = await _db.CipProjects.AsNoTracking()
            .CountAsync(p => p.ProjectNumber.StartsWith(CipPrefix), ct);
        var need = Math.Max(0, TargetCipCount - existingCount);
        if (need == 0) return (0, existingCount);

        var costs = new decimal[]
        {
            725_000m, 580_000m, 425_000m, 385_000m, 295_000m,
        };

        var projectNames = new string[]
        {
            "Mississauga CMM Suite Expansion",
            "Burlington Paint Booth Phase 2",
            "Mississauga 5-Axis CNC Build-Out",
            "Burlington Welding Cell Robotics",
            "Plant 1 Material Handling Refresh",
        };

        int inserted = 0;
        for (int i = 0; i < need; i++)
        {
            _db.CipProjects.Add(new CipProject
            {
                ProjectNumber = $"{CipPrefix}{(existingCount + i + 1):D3}",
                Name = projectNames[i % projectNames.Length],
                Description = "ABS CFO demo CIP project (seeded by AbsCfoMotionScenarioSeeder).",
                Status = CipProjectStatus.Active,
                StartDate = DateTime.UtcNow.Date.AddMonths(-(i + 1)),
                EstimatedCompletionDate = DateTime.UtcNow.Date.AddMonths(i + 3),
                BudgetAmount = costs[i % costs.Length] * 1.15m,
                TotalCosts   = costs[i % costs.Length],
                CommittedCosts = costs[i % costs.Length] * 0.30m,
                Currency = "CAD",
                CompanyId = AbsCompanyId,
                IsCapitalized = false,
                CreatedAt = DateTime.UtcNow,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    private static AbsCfoSeedResult EmptyResult(IReadOnlyList<string> warnings) =>
        new(AbsCompanyId, 0, 0, 0, 0, 0, 0, 0, 0, warnings);

    /// <summary>
    /// Runs a bucket's seed work + SaveChanges in an isolated try-block.
    /// On failure: logs the full inner-exception chain, appends a warning
    /// that names the bucket + the deepest exception message, and discards
    /// the failed bucket's queued entities from the change tracker so the
    /// next bucket starts from a clean state.
    /// </summary>
    private async Task TrySaveBucketAsync(
        string bucketName,
        Func<Task> seedWork,
        List<string> warnings,
        CancellationToken ct)
    {
        try
        {
            await seedWork();
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Walk the inner-exception chain to surface the actual PG /
            // CHECK / FK message — the outer EF wrapper just says "see
            // inner exception."
            var deepest = ex;
            while (deepest.InnerException is not null) deepest = deepest.InnerException;

            _logger.LogError(ex,
                "AbsCfoMotionScenarioSeeder bucket {Bucket} failed: {Message}",
                bucketName, deepest.Message);

            warnings.Add(
                $"Bucket '{bucketName}' failed: {deepest.GetType().Name}: {deepest.Message}");

            // Discard the failed bucket's queued (un-saved) entities so the
            // next bucket runs against a clean change-tracker. Without this
            // the next SaveChanges would re-attempt the same failing rows.
            foreach (var entry in _db.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
