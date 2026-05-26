// =============================================================================
// Sprint 12.7 PR #5 — CfoMotionDemoSeeder
//
// Idempotent demo-data seeder that pushes the Controller Control Center KPI
// band to demo-believable numbers for the CFO motion. Targets the seeded
// demo tenant identified by **CompanyCode**, never by hardcoded Id.
//
// The demo tenant is `PWH-CAN` ("PWH MANUFACTURING CANADA") — seeded by
// `Services/Seeding/SeedPackExecutor.SeedCompaniesAsync` alongside
// `PWH` (parent — "PRESTIGE WORLDWIDE HOLDINGS") + `PWH-USA`. The codebase
// is intentionally tenant-agnostic; no real customer names live anywhere in
// the data model, comments, demo prefixes, or display strings.
//
// IDEMPOTENCY DISCIPLINE (Lock 14 carry-forward):
//
// Every INSERT is guarded by a natural-key WHERE NOT EXISTS check. Demo
// rows are tagged with a stable prefix so ops can find-and-skip on re-run
// AND so a future tear-down query can purge them with a single
// `WHERE prefix LIKE 'DEMO-CFO-...'` without affecting real tenant data:
//
//   - `JournalEntry.Batch = "DEMO-CFO-CASH-2026"`   (single batch)
//   - `VendorInvoice.InvoiceNumber LIKE 'DEMO-CFO-INV-%'`
//   - `PurchaseOrder.PONumber LIKE 'DEMO-CFO-PO-%'`
//   - `CipProject.ProjectNumber LIKE 'DEMO-CFO-CIP-%'`
//
// Second invocation = no-op for any row already present. The admin trigger
// page surfaces "already seeded N rows" feedback.
//
// LOCK 15 CARVE-OUT — DIRECT DBCONTEXT WRITES (seeder exception, established
// in Services/Seeding/BaseSeedStep.cs file header).
//
// LOCK 14 — RUNS ON DEV ONLY. Republish-with-Copy syncs to prod at the end
// of the active sprint window. Never run against prod directly.
//
// LOCK 12 — typed EF Core only; no raw SQL anywhere in this file.
//
// LESSON ENCODED (post-PR #351 incident):
//   Never hardcode a CompanyId integer. The previous version hardcoded
//   `AbsCompanyId = 2`, which the seeder happily wrote to whichever tenant
//   ended up at Id=2 in the live database — a real demo tenant. Lookup by
//   CompanyCode is the only safe path because the code maps 1:1 to the
//   seeded tenant intent regardless of insertion order. The seeder also
//   defends in depth by VERIFYING the looked-up Company.Name matches the
//   expected demo placeholder string before doing any writes.
//
// LESSON ENCODED (per-bucket SaveChanges from PR #352):
//   Each bucket persists independently via TrySaveBucketAsync. A failure
//   in one bucket no longer rolls back the others, and the deepest inner
//   exception is surfaced in the result Warnings list so ops can act on
//   the actual PG / CHECK / FK error code.
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

public interface ICfoMotionDemoSeeder
{
    /// <summary>
    /// Seed (or top-up) the CFO motion demo data on the demo tenant
    /// (looked up by <c>CompanyCode = "PWH-CAN"</c>). Idempotent — calling
    /// twice yields the same end state.
    /// </summary>
    Task<CfoMotionDemoSeedResult> SeedAsync(CancellationToken ct);
}

/// <summary>
/// Per-bucket counts surfaced to the admin trigger page.
/// </summary>
public sealed record CfoMotionDemoSeedResult(
    int CompanyId,
    string CompanyCode,
    string CompanyName,
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

public sealed class CfoMotionDemoSeeder : ICfoMotionDemoSeeder
{
    // Demo tenant — seeded by SeedPackExecutor as one of the PWH-* placeholder
    // companies. Lookup by CompanyCode (NOT by hardcoded Id) — the seeded
    // tenant's database Id can shift depending on insertion order.
    private const string DemoCompanyCode = "PWH-CAN";

    // Belt-and-braces defense — verify the looked-up Company has the
    // expected placeholder Name + CompanyCode before writing. If a future
    // seed pack changes the demo placeholder, this guard fires and the
    // operator sees a clear warning rather than silent pollution.
    private const string DemoCompanyExpectedNamePrefix = "PWH";

    // Natural-key prefixes. Stable so re-runs are idempotent AND so future
    // tear-down purges find them by prefix.
    private const string CashBatchKey  = "DEMO-CFO-CASH-2026";
    private const string InvoicePrefix = "DEMO-CFO-INV-";
    private const string PoPrefix      = "DEMO-CFO-PO-";
    private const string CipPrefix     = "DEMO-CFO-CIP-";

    // Target counts. Tuned to land the KPI band in the "danger AP / healthy
    // cash / mid WIP" demo zone without flooding the queues with visual clutter.
    private const int TargetInvoiceCount = 20;
    private const int TargetPoCount      = 18;
    private const int TargetCipCount     = 5;

    private readonly AppDbContext _db;
    private readonly ILogger<CfoMotionDemoSeeder> _logger;

    public CfoMotionDemoSeeder(AppDbContext db, ILogger<CfoMotionDemoSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CfoMotionDemoSeedResult> SeedAsync(CancellationToken ct)
    {
        var warnings = new List<string>();

        // ------ FK resolution pass ------------------------------------------
        // Look up the demo tenant by CompanyCode. Verify the result matches
        // the expected placeholder shape — if a future seed pack changes the
        // demo company structure, this guard fires.

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyCode == DemoCompanyCode, ct);

        if (company is null)
        {
            warnings.Add(
                $"Demo tenant CompanyCode='{DemoCompanyCode}' not found in Companies table. " +
                "Run the SeedPackExecutor (or equivalent placeholder seed) first.");
            return EmptyResult(0, DemoCompanyCode, "<unknown>", warnings);
        }

        if (string.IsNullOrEmpty(company.Name) || !company.Name.StartsWith(DemoCompanyExpectedNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"Demo tenant CompanyCode='{DemoCompanyCode}' resolves to Company.Name='{company.Name}' " +
                $"which does NOT start with the expected '{DemoCompanyExpectedNamePrefix}' prefix. " +
                "Refusing to write demo data to a tenant that may not be a placeholder. " +
                "If you've intentionally renamed PWH-* tenants, update DemoCompanyExpectedNamePrefix.");
            return EmptyResult(company.Id, company.CompanyCode, company.Name, warnings);
        }

        var tenantId = company.Id;

        // Cash account: any active CashAndReceivables-categorised GlAccount
        // that's tenant-scoped to the demo tenant OR a system template (NULL).
        var cashAccount = await _db.GlAccounts.AsNoTracking()
            .Where(g => g.IsActive
                        && g.Category == GlAccountCategory.CashAndReceivables
                        && (g.CompanyId == tenantId || g.CompanyId == null))
            .OrderBy(g => g.AccountNumber)
            .FirstOrDefaultAsync(ct);

        // Offset account for the balanced JE — anything NOT CashAndReceivables.
        var offsetAccount = await _db.GlAccounts.AsNoTracking()
            .Where(g => g.IsActive
                        && g.Category != GlAccountCategory.CashAndReceivables
                        && (g.CompanyId == tenantId || g.CompanyId == null))
            .OrderBy(g => g.AccountNumber)
            .FirstOrDefaultAsync(ct);

        if (cashAccount is null || offsetAccount is null)
        {
            warnings.Add(
                $"Could not resolve cash+offset GL accounts for tenant '{DemoCompanyCode}' (Id={tenantId}). " +
                $"cash={cashAccount?.AccountNumber ?? "<null>"} offset={offsetAccount?.AccountNumber ?? "<null>"}. " +
                "Cash bump skipped.");
        }

        var book = await _db.Books.AsNoTracking()
            .Where(b => b.CompanyId == tenantId || b.CompanyId == null)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync(ct);
        if (book is null)
        {
            warnings.Add("No Book row found — JournalEntry seed cannot proceed.");
        }

        var vendors = await _db.Vendors.AsNoTracking()
            .Where(v => v.IsActive && v.CompanyId == tenantId)
            .OrderBy(v => v.Id)
            .Take(25)
            .ToListAsync(ct);
        if (vendors.Count == 0)
        {
            warnings.Add(
                $"No active vendors found for tenant '{DemoCompanyCode}' (Id={tenantId}) — " +
                "invoice + PO seed cannot proceed.");
        }

        // ------ Per-bucket SaveChanges --------------------------------------
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
            (invInserted, invSkipped) = await SeedVendorInvoicesAsync(tenantId, vendors, ct);
        }, warnings, ct);

        await TrySaveBucketAsync("Purchase orders", async () =>
        {
            (poInserted, poSkipped) = await SeedPurchaseOrdersAsync(tenantId, vendors, ct);
        }, warnings, ct);

        await TrySaveBucketAsync("CIP projects", async () =>
        {
            (cipInserted, cipSkipped) = await SeedCipProjectsAsync(tenantId, ct);
        }, warnings, ct);

        return new CfoMotionDemoSeedResult(
            CompanyId:               tenantId,
            CompanyCode:             company.CompanyCode,
            CompanyName:             company.Name,
            CashLinesInserted:       cashInserted,
            VendorInvoicesInserted:  invInserted,
            PurchaseOrdersInserted:  poInserted,
            CipProjectsInserted:     cipInserted,
            CashLinesSkipped:        cashSkipped,
            VendorInvoicesSkipped:   invSkipped,
            PurchaseOrdersSkipped:   poSkipped,
            CipProjectsSkipped:      cipSkipped,
            Warnings:                warnings);
    }

    // -------------------------------------------------------------------
    // Bucket 1 — single balanced JournalEntry with 10 paired Dr/Cr lines.
    // Total cash inflow ~$6.2M to land Cash Position in the demo-believable
    // mid range. Posted to the demo tenant's Book; tenant scope flows
    // through Book.CompanyId.
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedCashJournalAsync(
        GlAccount? cashAccount, GlAccount? offsetAccount, Book? book, CancellationToken ct)
    {
        if (cashAccount is null || offsetAccount is null || book is null) return (0, 0);

        var existing = await _db.JournalEntries.AsNoTracking()
            .Where(j => j.Batch == CashBatchKey)
            .Select(j => new { j.Id, LineCount = j.Lines.Count })
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return (0, existing.LineCount);

        var amounts = new decimal[]
        {
            850_000m, 720_000m, 650_000m, 600_000m, 575_000m,
            550_000m, 525_000m, 500_000m, 475_000m, 750_000m,
        };

        var je = new JournalEntry
        {
            BookId      = book.Id,
            Batch       = CashBatchKey,
            Reference   = "CFO-DEMO",
            Source      = "DemoSeeder",
            Period      = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
            PostingDate = DateTime.UtcNow.Date.AddDays(-7),
            CreatedUtc  = DateTime.UtcNow,
            Description = "Demo seeder — cash position snapshot bumps (CFO motion demo).",
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
    // Bucket 2 — vendor invoices (~$540K total outstanding, danger band).
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedVendorInvoicesAsync(
        int tenantId, IReadOnlyList<Vendor> vendors, CancellationToken ct)
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
                InvoiceNumber  = $"{InvoicePrefix}{(existingCount + i + 1):D5}",
                VendorId       = vendor.Id,
                CompanyId      = tenantId,
                Status         = status,
                MatchStatus    = InvoiceMatchStatus.FullyMatched,
                InvoiceDate    = today.AddDays(-(i + 5)),
                ReceivedDate   = today.AddDays(-(i + 4)),
                DueDate        = today.AddDays((i % 7) + 1),
                PaymentTerms   = PaymentTerms.Net30,
                Currency       = "CAD",
                Subtotal       = amount,
                TaxAmount      = 0m,
                ShippingAmount = 0m,
                Total          = amount,
                AmountPaid     = amountPaid,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    // -------------------------------------------------------------------
    // Bucket 3 — open POs (~$2.4M committed across 18 POs).
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedPurchaseOrdersAsync(
        int tenantId, IReadOnlyList<Vendor> vendors, CancellationToken ct)
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
                PONumber       = $"{PoPrefix}{(existingCount + i + 1):D4}",
                POType         = POType.Standard,
                Status         = status,
                VendorId       = vendor.Id,
                CompanyId      = tenantId,
                OrderDate      = today.AddDays(-(i + 2)),
                RequiredDate   = today.AddDays((i % 14) + 7),
                PromiseDate    = today.AddDays((i % 14) + 10),
                Currency       = "CAD",
                Subtotal       = amount,
                TaxAmount      = 0m,
                ShippingAmount = 0m,
                Total          = amount,
                Notes          = "Demo seeder PO (CFO motion demo).",
                ApprovedAt     = (status == POStatus.Approved || status == POStatus.Sent || status == POStatus.PartiallyReceived)
                                   ? today.AddDays(-(i + 1)) : null,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    // -------------------------------------------------------------------
    // Bucket 4 — CIP projects (5 active, ~$2.4M total in WIP).
    // Project names are generic plant-improvement labels — no real
    // customer or location references.
    // -------------------------------------------------------------------
    private async Task<(int inserted, int skipped)> SeedCipProjectsAsync(int tenantId, CancellationToken ct)
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
            "CMM Suite Expansion (Demo)",
            "Paint Booth Phase 2 (Demo)",
            "5-Axis CNC Build-Out (Demo)",
            "Welding Cell Robotics (Demo)",
            "Material Handling Refresh (Demo)",
        };

        int inserted = 0;
        for (int i = 0; i < need; i++)
        {
            _db.CipProjects.Add(new CipProject
            {
                ProjectNumber           = $"{CipPrefix}{(existingCount + i + 1):D3}",
                Name                    = projectNames[i % projectNames.Length],
                Description             = "Demo seeder CIP project (CFO motion demo).",
                Status                  = CipProjectStatus.Active,
                StartDate               = DateTime.UtcNow.Date.AddMonths(-(i + 1)),
                EstimatedCompletionDate = DateTime.UtcNow.Date.AddMonths(i + 3),
                BudgetAmount            = costs[i % costs.Length] * 1.15m,
                TotalCosts              = costs[i % costs.Length],
                CommittedCosts          = costs[i % costs.Length] * 0.30m,
                Currency                = "CAD",
                CompanyId               = tenantId,
                IsCapitalized           = false,
                CreatedAt               = DateTime.UtcNow,
            });
            inserted++;
        }

        return (inserted, existingCount);
    }

    private static CfoMotionDemoSeedResult EmptyResult(
        int companyId, string companyCode, string companyName, IReadOnlyList<string> warnings) =>
        new(companyId, companyCode, companyName, 0, 0, 0, 0, 0, 0, 0, 0, warnings);

    /// <summary>
    /// Per-bucket SaveChanges + inner-exception walking. A failure in one
    /// bucket does NOT take the others down; the deepest exception in the
    /// chain (typically the NpgsqlException with the actual PG error code)
    /// is surfaced in the Warnings list. Failed bucket's queued entities
    /// are detached from the change tracker so the next bucket runs against
    /// a clean state.
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
            var deepest = ex;
            while (deepest.InnerException is not null) deepest = deepest.InnerException;

            _logger.LogError(ex,
                "CfoMotionDemoSeeder bucket {Bucket} failed: {Message}",
                bucketName, deepest.Message);

            warnings.Add($"Bucket '{bucketName}' failed: {deepest.GetType().Name}: {deepest.Message}");

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
