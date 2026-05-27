// =============================================================================
// B8 PR-PRO-8+ — ProductionCockpitDemoSeeder
//
// Idempotent seeder that populates THREE interconnected PRO scenarios with
// FULL B8 cockpit data: material transactions, operation transactions, WIP
// moves, completion/scrap/rework events, documents, ECR/ECO, CAR, labor
// entries, and supply link fields. Every row has TenantId + CompanyId for
// multi-tenant discipline, CreatedBy/PerformedBy for audit lineage,
// lot/serial/heat/cert for traceability.
//
// THREE SCENARIOS:
//   S1: DEMO-COCKPIT-PRO-3001 "5-Axis Bracket Assembly" (InProgress)
//       4/6 ops done, 1 scrap event, mixed readiness (2P/1W/1F)
//   S2: DEMO-COCKPIT-PRO-3002 "Turbine Shaft Support" (Completed)
//       All done, FAI passed, clean cost rollup $42K plan / $41.8K actual
//   S3: DEMO-COCKPIT-PRO-3003 "Valve Body Casting" (OnHold)
//       Quality hold at op 30, CAR raised, ECO in progress, 2 reversals
//
// IDEMPOTENCY: ProductionOrder.OrderNumber = "DEMO-COCKPIT-PRO-3001".
// TENANT GUARD: CompanyCode = "PWH-CAN" + Name starts with "PWH".
// LOCK 12: typed EF Core only; no raw SQL.
// LOCK 15: per-bucket SaveChanges.
// HARD LOCK: no customer names in code. No fake data — real mfg part numbers.
// CONTROL PLANE: raw AppDbContext writes acceptable in seeders (seeding is
//   cross-tenant by design — see BaseSeedStep.cs header comment).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Models.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding;

public interface IProductionCockpitDemoSeeder
{
    /// <summary>
    /// Seed three PRO cockpit demo scenarios on the demo tenant
    /// (CompanyCode = "PWH-CAN"). Idempotent — calling twice yields the
    /// same end state. Anchored on DEMO-COCKPIT-PRO-3001.
    /// </summary>
    Task<ProductionCockpitDemoSeedResult> SeedAsync(CancellationToken ct);
}

public sealed record ProductionCockpitDemoSeedResult(
    int CompanyId,
    string CompanyCode,
    bool AlreadySeeded,
    int ProductionOrdersCreated,
    int BomLinesCreated,
    int OperationsCreated,
    int MaterialTransactionsCreated,
    int OperationTransactionsCreated,
    int WipMovesCreated,
    int CompletionEventsCreated,
    int ScrapEventsCreated,
    int DocumentsCreated,
    int EcrEcoCreated,
    int CarsCreated,
    int LaborEntriesCreated,
    IReadOnlyList<string> Warnings)
{
    public int TotalRowsCreated =>
        ProductionOrdersCreated + BomLinesCreated + OperationsCreated +
        MaterialTransactionsCreated + OperationTransactionsCreated +
        WipMovesCreated + CompletionEventsCreated + ScrapEventsCreated +
        DocumentsCreated + EcrEcoCreated + CarsCreated + LaborEntriesCreated;
}

public sealed class ProductionCockpitDemoSeeder : IProductionCockpitDemoSeeder
{
    // ===== Tenant guard (same as CooMotionDemoSeeder) =====
    private const string DemoCompanyCode = "PWH-CAN";
    private const string DemoCompanyExpectedNamePrefix = "PWH";

    // ===== Idempotency anchor =====
    private const string AnchorOrderNumber = "DEMO-COCKPIT-PRO-3001";

    // ===== Demo operator / auditor names =====
    private const string Operator1 = "M. Rodriguez";
    private const string Operator2 = "J. Chen";
    private const string Inspector1 = "R. Patel";
    private const string Supervisor1 = "K. Johansson";
    private const string Engineer1 = "A. Nakamura";

    // ===== Realistic shift timestamps (May 2026, first shift 6:00-14:30) =====
    private static readonly DateTime Day1Start = new(2026, 5, 12, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2Start = new(2026, 5, 13, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3Start = new(2026, 5, 14, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day4Start = new(2026, 5, 15, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day5Start = new(2026, 5, 16, 6, 0, 0, DateTimeKind.Utc);

    // ===== DI =====
    private readonly AppDbContext _db;
    private readonly ILogger<ProductionCockpitDemoSeeder> _logger;

    public ProductionCockpitDemoSeeder(
        AppDbContext db,
        ILogger<ProductionCockpitDemoSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProductionCockpitDemoSeedResult> SeedAsync(CancellationToken ct)
    {
        var w = new List<string>();
        int prosCreated = 0, bomLines = 0, ops = 0, matTx = 0, opTx = 0;
        int wipMoves = 0, completions = 0, scraps = 0, docs = 0;
        int ecrEco = 0, cars = 0, laborEntries = 0;

        // ───── 1. Tenant resolution + safety guard ─────
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyCode == DemoCompanyCode, ct);

        if (company is null)
        {
            w.Add($"Demo tenant '{DemoCompanyCode}' not found. Run the system seed first.");
            return EmptyResult(0, DemoCompanyCode, w);
        }

        if (string.IsNullOrEmpty(company.Name) ||
            !company.Name.StartsWith(DemoCompanyExpectedNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            w.Add($"Company '{DemoCompanyCode}' name='{company.Name}' doesn't match expected prefix '{DemoCompanyExpectedNamePrefix}'. Refusing write.");
            return EmptyResult(company.Id, company.CompanyCode, w);
        }

        var tenantId = company.Id;
        var siteId = await ResolveSiteIdAsync(tenantId, ct);

        // ───── 2. Idempotency check ─────
        var anchorExists = await _db.Set<ProductionOrder>().AsNoTracking()
            .AnyAsync(o => o.CompanyId == tenantId && o.OrderNumber == AnchorOrderNumber, ct);

        if (anchorExists)
        {
            // Check if transactions exist — if so, fully seeded
            var txCount = await _db.Set<ProductionMaterialTransaction>().AsNoTracking()
                .CountAsync(t => t.CompanyId == tenantId &&
                    t.TransactionNumber.StartsWith("DEMO-MTX-"), ct);
            if (txCount > 0)
            {
                return new ProductionCockpitDemoSeedResult(
                    tenantId, company.CompanyCode, AlreadySeeded: true,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, w);
            }
            w.Add("Anchor PRO exists but transaction layers missing — resuming.");
        }

        _logger.LogInformation(
            "ProductionCockpitDemoSeeder: seeding 3 scenarios on tenant {Code} (Id={Id})",
            company.CompanyCode, tenantId);

        // ───── 3. Ensure demo Items exist ─────
        var items = await EnsureDemoItemsAsync(tenantId, ct);
        w.Add($"Demo items resolved: {items.Count} items");

        // ───── 4. Seed S1 — InProgress scenario ─────
        var s1 = await SeedScenario1Async(tenantId, siteId, items, ct);
        prosCreated += s1.pros; bomLines += s1.bom; ops += s1.ops;
        matTx += s1.matTx; opTx += s1.opTx; wipMoves += s1.wip;
        scraps += s1.scraps; laborEntries += s1.labor;
        docs += s1.docs;

        // ───── 5. Seed S2 — Completed scenario ─────
        var s2 = await SeedScenario2Async(tenantId, siteId, items, ct);
        prosCreated += s2.pros; bomLines += s2.bom; ops += s2.ops;
        matTx += s2.matTx; opTx += s2.opTx; wipMoves += s2.wip;
        completions += s2.completions; laborEntries += s2.labor;

        // ───── 6. Seed S3 — OnHold scenario ─────
        var s3 = await SeedScenario3Async(tenantId, siteId, items, ct);
        prosCreated += s3.pros; bomLines += s3.bom; ops += s3.ops;
        matTx += s3.matTx; opTx += s3.opTx;
        ecrEco += s3.ecrEco; cars += s3.cars; laborEntries += s3.labor;

        _logger.LogInformation(
            "ProductionCockpitDemoSeeder: complete. {Total} rows created.",
            prosCreated + bomLines + ops + matTx + opTx + wipMoves +
            completions + scraps + docs + ecrEco + cars + laborEntries);

        return new ProductionCockpitDemoSeedResult(
            tenantId, company.CompanyCode, AlreadySeeded: false,
            prosCreated, bomLines, ops, matTx, opTx, wipMoves,
            completions, scraps, docs, ecrEco, cars, laborEntries, w);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SCENARIO 1 — InProgress (5-Axis Bracket Assembly)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int pros, int bom, int ops, int matTx, int opTx,
        int wip, int scraps, int labor, int docs)>
        SeedScenario1Async(int tenantId, int siteId,
            Dictionary<string, int> items, CancellationToken ct)
    {
        int pros = 0, bomCount = 0, opsCount = 0, matTxCount = 0;
        int opTxCount = 0, wipCount = 0, scrapCount = 0, laborCount = 0, docCount = 0;

        // ── PRO header ──
        var pro = await FindOrCreateProAsync(tenantId, siteId,
            "DEMO-COCKPIT-PRO-3001", "5-Axis Bracket Assembly — Lot 2026-Q3-142",
            ProductionOrderStatus.InProgress, ProductionType.JobShop,
            releasedQty: 25, completedQty: 0,
            plannedCost: 68_500m, actualCost: 52_340m,
            scheduledStart: Day1Start, scheduledEnd: Day5Start.AddDays(5),
            actualStart: Day1Start,
            holdReason: null, drawingRev: "Rev C", wiRev: "WI-2026-003",
            ct: ct);
        if (pro.created) pros++;

        // ── BOM lines (6 components) ──
        var bomSpecs = new[]
        {
            ("AL-7075-T6-PLT", "7075-T6 Aluminum Plate 1.5\" × 12\" × 24\"", 25m, 25m, 186.40m, "EA", "H-2026-AL-1142", "LOT-AL-7075-Q3-001"),
            ("TI-6AL4V-ROD",   "Ti-6Al-4V Rod 2\" OD × 8\"",                 50m, 50m, 342.80m, "EA", "H-2026-TI-0893", "LOT-TI-6AL-Q3-001"),
            ("PIN-17-4PH-025", "17-4PH Stainless Pin 0.250\" × 2.5\"",      100m, 80m,  12.60m, "EA", "H-2026-SS-0447", "LOT-SS-174-Q3-001"),
            ("AN365-428A",     "Self-Locking Nut AN365-428A (MS21044)",      200m,200m,   0.84m, "EA", null,              "LOT-HW-STD-2026"),
            ("NAS1149F0463P",  "Flat Washer NAS1149F0463P",                  200m,200m,   0.42m, "EA", null,              "LOT-HW-STD-2026"),
            ("AMS-QQ-P-35",   "Chemical Film Kit per AMS-QQ-P-35 Type I",     5m,  3m,  48.90m, "KIT", null,             "LOT-CHEM-Q3-001"),
        };

        foreach (var (pn, desc, qtyReq, qtyIssued, unitCost, uom, heat, lot) in bomSpecs)
        {
            var itemId = items.GetValueOrDefault(pn);
            if (itemId == 0) continue;

            var bom = await FindOrCreateBomLineAsync(tenantId, pro.id, itemId,
                pn, qtyReq, qtyIssued, unitCost, uom, heat, lot,
                qtyIssued >= qtyReq ? BomLineStatus.Issued : BomLineStatus.PartiallyIssued,
                ct);
            if (bom.created) bomCount++;

            // Material transaction for each issued BOM line
            if (qtyIssued > 0)
            {
                var txCreated = await CreateMaterialTransactionAsync(tenantId, pro.id, bom.id, itemId,
                    $"DEMO-MTX-S1-{pn}", qtyIssued >= qtyReq ? MaterialTransactionType.Issue : MaterialTransactionType.PartialIssue,
                    qtyIssued, unitCost, uom, heat, lot,
                    "WH-MAIN", "BIN-A01", Operator1, Day1Start.AddHours(2), ct);
                if (txCreated) matTxCount++;
            }
        }

        // ── Routing operations (6 ops: 4 complete, 1 in-progress, 1 not started) ──
        var opSpecs = new[]
        {
            (seq: 10, name: "CNC Rough Mill",     status: ProductionOperationStatus.Completed, setupMins: 45m, runMins: 180m, oper: Operator1),
            (seq: 20, name: "Heat Treat (Aging)",  status: ProductionOperationStatus.Completed, setupMins: 15m, runMins: 480m, oper: Operator2),
            (seq: 30, name: "CNC Finish Mill",     status: ProductionOperationStatus.Completed, setupMins: 60m, runMins: 240m, oper: Operator1),
            (seq: 40, name: "CMM Inspection",      status: ProductionOperationStatus.Completed, setupMins: 20m, runMins:  90m, oper: Inspector1),
            (seq: 50, name: "Final Assembly",       status: ProductionOperationStatus.Running,   setupMins: 30m, runMins:   0m, oper: Operator2),
            (seq: 60, name: "Final Inspect + Pack", status: ProductionOperationStatus.Released,  setupMins:  0m, runMins:   0m, oper: (string?)null),
        };

        foreach (var (seq, name, status, setupMins, runMins, oper) in opSpecs)
        {
            var op = await FindOrCreateOperationAsync(tenantId, pro.id,
                seq, name, status, setupMins, runMins, oper, ct);
            if (op.created) opsCount++;

            // Operation transactions for completed + running ops
            if (status == ProductionOperationStatus.Completed)
            {
                var dayOffset = (seq / 10) - 1;
                var startTime = Day1Start.AddDays(dayOffset).AddHours(1);
                var endTime = startTime.AddMinutes((double)(setupMins + runMins));

                opTxCount += await CreateOpTransactionPairAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S1-{seq}", oper!, startTime, endTime, 25m, ct);

                // WIP auto-advance move
                if (seq < 50)
                {
                    var moveCreated = await CreateWipMoveAsync(tenantId, pro.id, op.id,
                        seq, seq + 10, WipMoveType.AutoAdvance, 25m, oper!, endTime, ct);
                    if (moveCreated) wipCount++;
                }
            }
            else if (status == ProductionOperationStatus.Running)
            {
                // Started but not complete
                var txCreated = await CreateSingleOpTransactionAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S1-{seq}-START", OperationTransactionType.Start,
                    oper!, Day4Start.AddHours(1), 0m, ct);
                if (txCreated) opTxCount++;

                // Setup complete
                var setupTx = await CreateSingleOpTransactionAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S1-{seq}-SETUP", OperationTransactionType.CompleteSetup,
                    oper!, Day4Start.AddHours(1).AddMinutes((double)setupMins), 0m, ct);
                if (setupTx) opTxCount++;
            }

            // Labor entries for completed ops
            if (status == ProductionOperationStatus.Completed && oper != null)
            {
                var dayOffset = (seq / 10) - 1;
                var laborCreated = await CreateLaborEntryAsync(tenantId, pro.id, op.id,
                    oper, Day1Start.AddDays(dayOffset).AddHours(1),
                    Day1Start.AddDays(dayOffset).AddHours(1).AddMinutes((double)(setupMins + runMins)),
                    setupMins, runMins, ct);
                if (laborCreated) laborCount++;
            }
        }

        // ── Scrap event on Op 20 (bad heat treat lot) ──
        var op20 = await _db.Set<ProductionOperation>().FirstOrDefaultAsync(
            o => o.ProductionOrderId == pro.id && o.SequenceNumber == 20 && o.CompanyIdSnapshot == tenantId, ct);
        if (op20 != null)
        {
            var scrapCreated = await CreateScrapEventAsync(tenantId, pro.id, op20.Id,
                scrapQty: 3m, goodQty: 22m,
                "Heat lot H-2026-TI-0893 out of spec — hardness 39 HRC vs 36±2 required",
                ScrapDisposition.Scrap, ScrapResponsibleArea.Vendor,
                CostTreatment.VendorChargeback, Inspector1, Day2Start.AddHours(6), ct);
            if (scrapCreated) scrapCount++;
        }

        // ── Document: Drawing ──
        var drawCreated = await CreateDocumentAsync(tenantId,
            "DWG-BKT-3001", "5-Axis Bracket Assembly Drawing", "Rev C",
            DocumentType.Drawing, DocumentStatus.Released,
            Engineer1, Day1Start.AddDays(-10), ct);
        if (drawCreated) docCount++;

        return (pros, bomCount, opsCount, matTxCount, opTxCount, wipCount, scrapCount, laborCount, docCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SCENARIO 2 — Completed (Turbine Shaft Support)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int pros, int bom, int ops, int matTx, int opTx,
        int wip, int completions, int labor)>
        SeedScenario2Async(int tenantId, int siteId,
            Dictionary<string, int> items, CancellationToken ct)
    {
        int pros = 0, bomCount = 0, opsCount = 0, matTxCount = 0;
        int opTxCount = 0, wipCount = 0, completionCount = 0, laborCount = 0;

        var pro = await FindOrCreateProAsync(tenantId, siteId,
            "DEMO-COCKPIT-PRO-3002", "Turbine Shaft Support — Ship Lot SL-412",
            ProductionOrderStatus.Completed, ProductionType.JobShop,
            releasedQty: 10, completedQty: 10,
            plannedCost: 42_000m, actualCost: 41_800m,
            scheduledStart: Day1Start.AddDays(-14), scheduledEnd: Day1Start.AddDays(-2),
            actualStart: Day1Start.AddDays(-14), actualEnd: Day1Start.AddDays(-3),
            holdReason: null, drawingRev: "Rev B", wiRev: "WI-2026-002",
            ct);
        if (pro.created) pros++;

        var bomSpecs = new[]
        {
            ("BAR-4340-2OD", "4340 Alloy Steel Bar 2\" OD × 12\"",    10m, 10m, 94.60m, "EA", "H-2026-4340-0761", "LOT-4340-Q2-001"),
            ("BRG-6205-2RS", "Ball Bearing 6205-2RS SKF",             20m, 20m, 28.40m, "EA", null,                "LOT-BRG-SKF-2026"),
            ("SEL-TC-30",    "TC-30 Viton Shaft Seal 30mm ID",        10m, 10m,  4.25m, "EA", null,                "LOT-SEL-Q2-001"),
            ("FAS-M8-50",    "M8 × 50 Grade 10.9 Hex Bolt DIN 931",  80m, 80m,  0.68m, "EA", null,                "LOT-HW-M8-2026"),
        };

        foreach (var (pn, desc, qtyReq, qtyIssued, unitCost, uom, heat, lot) in bomSpecs)
        {
            var itemId = items.GetValueOrDefault(pn);
            if (itemId == 0) continue;

            var bom = await FindOrCreateBomLineAsync(tenantId, pro.id, itemId,
                pn, qtyReq, qtyIssued, unitCost, uom, heat, lot,
                BomLineStatus.Consumed, ct);
            if (bom.created) bomCount++;

            var txCreated = await CreateMaterialTransactionAsync(tenantId, pro.id, bom.id, itemId,
                $"DEMO-MTX-S2-{pn}", MaterialTransactionType.Issue,
                qtyIssued, unitCost, uom, heat, lot,
                "WH-MAIN", "BIN-B01", Operator1, Day1Start.AddDays(-12), ct);
            if (txCreated) matTxCount++;
        }

        var opSpecs = new[]
        {
            (seq: 10, name: "Bar Cut to Length",   setupMins: 15m, runMins: 60m),
            (seq: 20, name: "CNC Turning",         setupMins: 45m, runMins: 360m),
            (seq: 30, name: "Cylindrical Grind",   setupMins: 30m, runMins: 180m),
            (seq: 40, name: "Press-Fit Assembly",   setupMins: 20m, runMins: 120m),
            (seq: 50, name: "Inspect + FAI + Pack", setupMins: 10m, runMins: 90m),
        };

        foreach (var (seq, name, setupMins, runMins) in opSpecs)
        {
            var op = await FindOrCreateOperationAsync(tenantId, pro.id,
                seq, name, ProductionOperationStatus.Completed, setupMins, runMins, Operator1, ct);
            if (op.created) opsCount++;

            opTxCount += await CreateOpTransactionPairAsync(tenantId, pro.id, op.id,
                $"DEMO-OTX-S2-{seq}", Operator1,
                Day1Start.AddDays(-13 + (seq / 10)), Day1Start.AddDays(-13 + (seq / 10)).AddMinutes((double)(setupMins + runMins)),
                10m, ct);

            if (seq < 50)
            {
                var moveCreated = await CreateWipMoveAsync(tenantId, pro.id, op.id,
                    seq, seq + 10, WipMoveType.AutoAdvance, 10m, Operator1,
                    Day1Start.AddDays(-13 + (seq / 10)).AddMinutes((double)(setupMins + runMins)), ct);
                if (moveCreated) wipCount++;
            }

            var laborCreated = await CreateLaborEntryAsync(tenantId, pro.id, op.id,
                Operator1,
                Day1Start.AddDays(-13 + (seq / 10)).AddHours(1),
                Day1Start.AddDays(-13 + (seq / 10)).AddHours(1).AddMinutes((double)(setupMins + runMins)),
                setupMins, runMins, ct);
            if (laborCreated) laborCount++;
        }

        // Completion event
        var lastOp = await _db.Set<ProductionOperation>().FirstOrDefaultAsync(
            o => o.ProductionOrderId == pro.id && o.SequenceNumber == 50 && o.CompanyIdSnapshot == tenantId, ct);
        if (lastOp != null)
        {
            var compCreated = await CreateCompletionEventAsync(tenantId, pro.id, lastOp.Id,
                goodQty: 10m, scrapQty: 0m, reworkQty: 0m,
                Inspector1, Day1Start.AddDays(-3), ct);
            if (compCreated) completionCount++;
        }

        return (pros, bomCount, opsCount, matTxCount, opTxCount, wipCount, completionCount, laborCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SCENARIO 3 — OnHold (Valve Body Casting)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int pros, int bom, int ops, int matTx, int opTx,
        int ecrEco, int cars, int labor)>
        SeedScenario3Async(int tenantId, int siteId,
            Dictionary<string, int> items, CancellationToken ct)
    {
        int pros = 0, bomCount = 0, opsCount = 0, matTxCount = 0;
        int opTxCount = 0, ecrEcoCount = 0, carCount = 0, laborCount = 0;

        var pro = await FindOrCreateProAsync(tenantId, siteId,
            "DEMO-COCKPIT-PRO-3003", "Valve Body Casting — Engineering Hold",
            ProductionOrderStatus.OnHold, ProductionType.JobShop,
            releasedQty: 15, completedQty: 0,
            plannedCost: 34_200m, actualCost: 12_860m,
            scheduledStart: Day1Start, scheduledEnd: Day5Start.AddDays(7),
            actualStart: Day1Start,
            holdReason: HoldReason.Quality,
            drawingRev: "Rev A", wiRev: "WI-2026-005",
            ct: ct);
        if (pro.created) pros++;

        var bomSpecs = new[]
        {
            ("CAST-GI-VB12",   "Gray Iron Casting Class 40 Valve Body Blank",  15m, 15m, 124.80m, "EA", "H-2026-GI-0558", "LOT-GI-VB-Q3-001"),
            ("BUSH-BRZ-1ID",   "C93200 Bronze Bushing 1\" ID × 1.5\" OD",     30m, 30m,  18.60m, "EA", null,              "LOT-BRZ-Q3-001"),
            ("ORING-VIT-KIT",  "Viton O-Ring Kit FKM-75A (6 sizes)",           15m,  0m,  22.40m, "KIT", null,             "LOT-VIT-Q3-001"),
            ("FAS-SS-KIT-M6",  "A4-80 Stainless Fastener Kit M6 × 20-40mm",   15m,  0m,   8.90m, "KIT", null,             "LOT-SS-M6-2026"),
            ("INHIB-VCI-330",  "VCI-330 Corrosion Inhibitor Spray 12 oz",       5m,  0m,  14.20m, "EA",  null,             "LOT-VCI-Q3-001"),
        };

        foreach (var (pn, desc, qtyReq, qtyIssued, unitCost, uom, heat, lot) in bomSpecs)
        {
            var itemId = items.GetValueOrDefault(pn);
            if (itemId == 0) continue;

            var status = qtyIssued > 0 ? BomLineStatus.Issued : BomLineStatus.NotRequiredYet;
            var bom = await FindOrCreateBomLineAsync(tenantId, pro.id, itemId,
                pn, qtyReq, qtyIssued, unitCost, uom, heat, lot, status, ct);
            if (bom.created) bomCount++;

            if (qtyIssued > 0)
            {
                // Original issue
                var txCreated = await CreateMaterialTransactionAsync(tenantId, pro.id, bom.id, itemId,
                    $"DEMO-MTX-S3-{pn}", MaterialTransactionType.Issue,
                    qtyIssued, unitCost, uom, heat, lot,
                    "WH-MAIN", "BIN-C01", Operator2, Day1Start.AddHours(3), ct);
                if (txCreated) matTxCount++;

                // Reversal after quality hold
                var revCreated = await CreateMaterialTransactionAsync(tenantId, pro.id, bom.id, itemId,
                    $"DEMO-MTX-S3-{pn}-REV", MaterialTransactionType.ReverseIssue,
                    qtyIssued, unitCost, uom, heat, lot,
                    "BIN-C01", "WH-MAIN", Supervisor1, Day3Start.AddHours(2),
                    ct, isReversal: true,
                    reason: "Material returned to inventory pending ECO resolution — quality hold on PRO-3003");
                if (revCreated) matTxCount++;
            }
        }

        var opSpecs = new[]
        {
            (seq: 10, name: "CNC Rough Machine",   status: ProductionOperationStatus.Completed, setupMins: 30m, runMins: 240m),
            (seq: 20, name: "CNC Bore + Thread",    status: ProductionOperationStatus.Completed, setupMins: 45m, runMins: 180m),
            (seq: 30, name: "Inspect — Bore Runout", status: ProductionOperationStatus.Paused,   setupMins:  0m, runMins:   0m),
            (seq: 40, name: "Assembly + Seal Test",  status: ProductionOperationStatus.Released,  setupMins:  0m, runMins:   0m),
        };

        foreach (var (seq, name, status, setupMins, runMins) in opSpecs)
        {
            var oper = status == ProductionOperationStatus.Completed ? Operator2 : null;
            var op = await FindOrCreateOperationAsync(tenantId, pro.id,
                seq, name, status, setupMins, runMins, oper, ct);
            if (op.created) opsCount++;

            if (status == ProductionOperationStatus.Completed)
            {
                var dayIdx = (seq / 10) - 1;
                opTxCount += await CreateOpTransactionPairAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S3-{seq}", Operator2,
                    Day1Start.AddDays(dayIdx).AddHours(1),
                    Day1Start.AddDays(dayIdx).AddHours(1).AddMinutes((double)(setupMins + runMins)),
                    15m, ct);

                var laborCreated = await CreateLaborEntryAsync(tenantId, pro.id, op.id,
                    Operator2,
                    Day1Start.AddDays(dayIdx).AddHours(1),
                    Day1Start.AddDays(dayIdx).AddHours(1).AddMinutes((double)(setupMins + runMins)),
                    setupMins, runMins, ct);
                if (laborCreated) laborCount++;
            }
            else if (status == ProductionOperationStatus.Paused)
            {
                // Started then paused for quality hold
                var startTx = await CreateSingleOpTransactionAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S3-{seq}-START", OperationTransactionType.Start,
                    Inspector1, Day2Start.AddHours(6), 0m, ct);
                if (startTx) opTxCount++;

                var pauseTx = await CreateSingleOpTransactionAsync(tenantId, pro.id, op.id,
                    $"DEMO-OTX-S3-{seq}-PAUSE", OperationTransactionType.Pause,
                    Inspector1, Day2Start.AddHours(7),  0m, ct);
                if (pauseTx) opTxCount++;
            }
        }

        // ── CAR (Corrective Action Request) ──
        var carCreated = await CreateCarAsync(tenantId, pro.id,
            "DEMO-CAR-3003-001",
            "Bore runout exceeded 0.003\" TIR tolerance on 12 of 15 pieces — tool deflection at depth",
            Inspector1, Day2Start.AddHours(8), ct);
        if (carCreated) carCount++;

        // ── ECR + ECO ──
        var ecrCreated = await CreateEcrEcoAsync(tenantId,
            "DEMO-ECR-3003-001", "Adjust bore feed rate and add spring-pass to eliminate deflection",
            "DEMO-ECO-3003-001",
            Engineer1, Day3Start, ct);
        if (ecrCreated) ecrEcoCount++;

        return (pros, bomCount, opsCount, matTxCount, opTxCount, ecrEcoCount, carCount, laborCount);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Ensure demo Items exist
    // ═══════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, int>> EnsureDemoItemsAsync(int tenantId, CancellationToken ct)
    {
        var partNumbers = new[]
        {
            // S1 components
            ("AL-7075-T6-PLT", "7075-T6 Aluminum Plate 1.5\" × 12\" × 24\"", 186.40m),
            ("TI-6AL4V-ROD",   "Ti-6Al-4V Rod 2\" OD × 8\"",                  342.80m),
            ("PIN-17-4PH-025", "17-4PH Stainless Pin 0.250\" × 2.5\"",         12.60m),
            ("AN365-428A",     "Self-Locking Nut AN365-428A (MS21044)",          0.84m),
            ("NAS1149F0463P",  "Flat Washer NAS1149F0463P",                      0.42m),
            ("AMS-QQ-P-35",   "Chemical Film Kit per AMS-QQ-P-35 Type I",       48.90m),
            // S2 components
            ("BAR-4340-2OD",  "4340 Alloy Steel Bar 2\" OD × 12\"",             94.60m),
            ("BRG-6205-2RS",  "Ball Bearing 6205-2RS SKF",                       28.40m),
            ("SEL-TC-30",     "TC-30 Viton Shaft Seal 30mm ID",                   4.25m),
            ("FAS-M8-50",     "M8 × 50 Grade 10.9 Hex Bolt DIN 931",             0.68m),
            // S3 components
            ("CAST-GI-VB12",  "Gray Iron Casting Class 40 Valve Body Blank",    124.80m),
            ("BUSH-BRZ-1ID",  "C93200 Bronze Bushing 1\" ID × 1.5\" OD",        18.60m),
            ("ORING-VIT-KIT", "Viton O-Ring Kit FKM-75A (6 sizes)",              22.40m),
            ("FAS-SS-KIT-M6", "A4-80 Stainless Fastener Kit M6 × 20-40mm",       8.90m),
            ("INHIB-VCI-330", "VCI-330 Corrosion Inhibitor Spray 12 oz",         14.20m),
        };

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pn, desc, cost) in partNumbers)
        {
            var existing = await _db.Items.FirstOrDefaultAsync(
                i => i.PartNumber == pn && i.CompanyId == tenantId, ct);
            if (existing != null)
            {
                result[pn] = existing.Id;
                continue;
            }

            var item = new Item
            {
                PartNumber = pn,
                Description = desc,
                StandardCost = cost,
                ListPrice = cost * 1.5m,
                PurchaseUOM = "EA",
                Status = ItemStatus.Active,
                Type = ItemType.Part,
                CompanyId = tenantId,
                Source = ItemMasterSource.Internal,
                ReorderPoint = 5,
                ReorderQuantity = 25,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Items.Add(item);
            await _db.SaveChangesAsync(ct);
            result[pn] = item.Id;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Find or create ProductionOrder
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int id, bool created)> FindOrCreateProAsync(
        int tenantId, int siteId, string orderNumber, string title,
        ProductionOrderStatus status, ProductionType prodType,
        decimal releasedQty, decimal completedQty,
        decimal plannedCost, decimal actualCost,
        DateTime scheduledStart, DateTime scheduledEnd,
        DateTime actualStart, DateTime? actualEnd = null,
        HoldReason? holdReason = null,
        string? drawingRev = null, string? wiRev = null,
        CancellationToken ct = default)
    {
        var existing = await _db.Set<ProductionOrder>()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.CompanyId == tenantId, ct);
        if (existing != null) return (existing.Id, false);

        var pro = new ProductionOrder
        {
            OrderNumber = orderNumber,
            Title = title,
            Status = status,
            Type = prodType,
            CompanyId = tenantId,
            LocationId = siteId > 0 ? siteId : null,
            QuantityOrdered = releasedQty,
            QuantityReleased = releasedQty,
            QuantityCompleted = completedQty,
            MaterialCost = actualCost * 0.38m,
            LaborCost = actualCost * 0.28m,
            OverheadCost = actualCost * 0.30m,
            SubcontractCost = actualCost * 0.04m,
            ActualCost = actualCost,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            ActualStart = actualStart,
            ActualEnd = actualEnd,
            HoldReason = holdReason,
            DrawingRevision = drawingRev,
            WorkInstructionsRevision = wiRev,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "DemoSeeder",
        };
        _db.Set<ProductionOrder>().Add(pro);
        await _db.SaveChangesAsync(ct);
        return (pro.Id, true);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Find or create BOM line (ProductionMaterialStructure)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int id, bool created)> FindOrCreateBomLineAsync(
        int tenantId, int proId, int itemId, string partNumber,
        decimal qtyRequired, decimal qtyIssued, decimal unitCost,
        string uom, string? heatNumber, string? lotNumber,
        BomLineStatus status, CancellationToken ct)
    {
        var existing = await _db.Set<ProductionMaterialStructure>()
            .FirstOrDefaultAsync(b => b.ProductionOrderId == proId
                && b.ChildItemId == itemId && b.CompanyId == tenantId, ct);
        if (existing != null) return (existing.Id, false);

        var bom = new ProductionMaterialStructure
        {
            ProductionOrderId = proId,
            ChildItemId = itemId,
            CompanyId = tenantId,
            QuantityPer = qtyRequired,
            FrozenStandardCost = unitCost,
            FrozenExtendedCost = qtyRequired * unitCost,
            IssuedQuantity = qtyIssued,
            SupplyQuantityRequired = qtyRequired,
            LineStatus = status,
            HeatNumber = heatNumber,
            IssuedLotNumber = lotNumber,
            IsCritical = heatNumber != null,
            IsLotControlled = lotNumber != null,
            CreatedAt = DateTime.UtcNow,
            CapturedBy = "DemoSeeder",
        };
        _db.Set<ProductionMaterialStructure>().Add(bom);
        await _db.SaveChangesAsync(ct);
        return (bom.Id, true);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Find or create ProductionOperation
    // ═══════════════════════════════════════════════════════════════════

    private async Task<(int id, bool created)> FindOrCreateOperationAsync(
        int tenantId, int proId, int sequence, string name,
        ProductionOperationStatus status, decimal setupMins, decimal runMins,
        string? operatorName, CancellationToken ct)
    {
        var existing = await _db.Set<ProductionOperation>()
            .FirstOrDefaultAsync(o => o.ProductionOrderId == proId
                && o.SequenceNumber == sequence, ct);
        if (existing != null) return (existing.Id, false);

        var op = new ProductionOperation
        {
            ProductionOrderId = proId,
            SequenceNumber = sequence,
            Description = name,
            Status = status,
            CompanyIdSnapshot = tenantId,
            ActualSetupMins = setupMins,
            ActualRunMins = runMins,
            PlannedSetupMins = setupMins * 1.1m,
            PlannedRunMins = runMins * 1.05m,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Set<ProductionOperation>().Add(op);
        await _db.SaveChangesAsync(ct);
        return (op.Id, true);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create material transaction with FULL LINEAGE
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateMaterialTransactionAsync(
        int tenantId, int proId, int bomLineId, int itemId,
        string txNumber, MaterialTransactionType txType,
        decimal qty, decimal unitCost, string uom,
        string? heatNumber, string? lotNumber,
        string fromWh, string toBin, string performer,
        DateTime txDate, CancellationToken ct,
        bool isReversal = false, string? reason = null)
    {
        var exists = await _db.Set<ProductionMaterialTransaction>()
            .AnyAsync(t => t.TransactionNumber == txNumber && t.CompanyId == tenantId, ct);
        if (exists) return false;

        var tx = new ProductionMaterialTransaction
        {
            TransactionNumber = txNumber,
            TransactionType = txType,
            Status = MaterialTransactionStatus.Posted,
            TransactionDateUtc = txDate,
            ProductionOrderId = proId,
            BomLineId = bomLineId,
            ItemId = itemId,
            Quantity = qty,
            Uom = uom,
            ActualUnitCost = unitCost,
            ExtendedCost = qty * unitCost,
            CostBucket = CostBucket.Material,
            FromWarehouse = fromWh,
            ToBin = toBin,
            HeatNumber = heatNumber,
            LotNumber = lotNumber,
            IsReversal = isReversal,
            ReasonCode = reason != null ? "QUALITY-HOLD" : null,
            ReasonDescription = reason,
            PerformedBy = performer,
            CreatedBy = performer,
            CreatedAt = txDate,
            TenantId = tenantId,
            CompanyId = tenantId,
        };
        _db.Set<ProductionMaterialTransaction>().Add(tx);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create operation transaction pair (Start + Complete)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> CreateOpTransactionPairAsync(
        int tenantId, int proId, int opId,
        string txPrefix, string performer,
        DateTime startTime, DateTime endTime,
        decimal completedQty, CancellationToken ct)
    {
        int count = 0;
        var s = await CreateSingleOpTransactionAsync(tenantId, proId, opId,
            $"{txPrefix}-START", OperationTransactionType.Start,
            performer, startTime, 0m, ct);
        if (s) count++;

        var c = await CreateSingleOpTransactionAsync(tenantId, proId, opId,
            $"{txPrefix}-COMP", OperationTransactionType.Complete,
            performer, endTime, completedQty, ct);
        if (c) count++;
        return count;
    }

    private async Task<bool> CreateSingleOpTransactionAsync(
        int tenantId, int proId, int opId,
        string txNumber, OperationTransactionType txType,
        string performer, DateTime txDate,
        decimal completedQty, CancellationToken ct)
    {
        var exists = await _db.Set<ProductionOperationTransaction>()
            .AnyAsync(t => t.TransactionNumber == txNumber && t.CompanyId == tenantId, ct);
        if (exists) return false;

        var tx = new ProductionOperationTransaction
        {
            TransactionNumber = txNumber,
            TransactionType = txType,
            ProductionOrderId = proId,
            OperationId = opId,
            GoodQuantity = completedQty,
            PerformedBy = performer,
            TransactionDateUtc = txDate,
            CreatedBy = performer,
            CreatedAt = txDate,
            TenantId = tenantId,
            CompanyId = tenantId,
        };
        _db.Set<ProductionOperationTransaction>().Add(tx);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create WIP move with lineage
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateWipMoveAsync(
        int tenantId, int proId, int fromOpId,
        int fromSeq, int toSeq, WipMoveType moveType,
        decimal qty, string performer, DateTime moveDate,
        CancellationToken ct)
    {
        var moveNum = $"DEMO-WIP-{proId}-{fromSeq}-{toSeq}";
        var exists = await _db.Set<ProductionWipMove>()
            .AnyAsync(m => m.MoveNumber == moveNum && m.CompanyId == tenantId, ct);
        if (exists) return false;

        // Resolve ToOperationId
        var toOp = await _db.Set<ProductionOperation>()
            .FirstOrDefaultAsync(o => o.ProductionOrderId == proId
                && o.SequenceNumber == toSeq && o.CompanyIdSnapshot == tenantId, ct);

        var move = new ProductionWipMove
        {
            MoveNumber = moveNum,
            ProductionOrderId = proId,
            FromOperationId = fromOpId,
            ToOperationId = toOp?.Id ?? 0,
            MoveType = moveType,
            Quantity = qty,
            FromSequenceNumber = fromSeq,
            ToSequenceNumber = toSeq,
            MovedBy = performer,
            MovedAtUtc = moveDate,
            CreatedBy = performer,
            CreatedAt = moveDate,
            TenantId = tenantId,
            CompanyId = tenantId,
        };
        _db.Set<ProductionWipMove>().Add(move);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create scrap event with 5-dimensional analysis
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateScrapEventAsync(
        int tenantId, int proId, int opId,
        decimal scrapQty, decimal goodQty, string rootCause,
        ScrapDisposition disposition, ScrapResponsibleArea responsible,
        CostTreatment costTreatment, string performer, DateTime eventDate,
        CancellationToken ct)
    {
        var eventNum = $"DEMO-SCRAP-{proId}-{opId}";
        var exists = await _db.Set<ProductionScrapEvent>()
            .AnyAsync(s => s.ScrapNumber == eventNum && s.CompanyId == tenantId, ct);
        if (exists) return false;

        var scrap = new ProductionScrapEvent
        {
            ScrapNumber = eventNum,
            ProductionOrderId = proId,
            DetectedAtOperationId = opId,
            ScrapQuantity = scrapQty,
            Notes = rootCause,
            Disposition = disposition,
            ResponsibleArea = responsible,
            CostTreatment = costTreatment,
            RecordedBy = performer,
            ScrapRecordedAtUtc = eventDate,
            CreatedBy = performer,
            CreatedAt = eventDate,
            TenantId = tenantId,
            CompanyId = tenantId,
        };
        _db.Set<ProductionScrapEvent>().Add(scrap);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create completion event
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateCompletionEventAsync(
        int tenantId, int proId, int opId,
        decimal goodQty, decimal scrapQty, decimal reworkQty,
        string performer, DateTime eventDate, CancellationToken ct)
    {
        var eventNum = $"DEMO-COMP-{proId}-{opId}";
        var exists = await _db.Set<ProductionCompletionEvent>()
            .AnyAsync(c => c.CompletionNumber == eventNum && c.CompanyId == tenantId, ct);
        if (exists) return false;

        var comp = new ProductionCompletionEvent
        {
            CompletionNumber = eventNum,
            ProductionOrderId = proId,
            OperationId = opId,
            GoodQuantity = goodQty,
            ScrapQuantity = scrapQty,
            ReworkQuantity = reworkQty,
            CompletedBy = performer,
            CompletedAtUtc = eventDate,
            CreatedBy = performer,
            CreatedAt = eventDate,
            TenantId = tenantId,
            CompanyId = tenantId,
        };
        _db.Set<ProductionCompletionEvent>().Add(comp);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create labor entry
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateLaborEntryAsync(
        int tenantId, int proId, int opId,
        string employeeName, DateTime clockIn, DateTime clockOut,
        decimal setupMins, decimal runMins, CancellationToken ct)
    {
        var exists = await _db.Set<LaborEntry>()
            .AnyAsync(l => l.ProductionOperationId == opId
                && l.CompanyId == tenantId, ct);
        if (exists) return false;

        var totalMins = setupMins + runMins;
        var labor = new LaborEntry
        {
            ProductionOperationId = opId,
            ClockInAt = clockIn,
            ClockOutAt = clockOut,
            DurationMins = totalMins,
            CompanyId = tenantId,
            CreatedAt = clockIn,
            Notes = $"Operator: {employeeName} — Setup {setupMins}m + Run {runMins}m",
        };
        _db.Set<LaborEntry>().Add(labor);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create document
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateDocumentAsync(
        int tenantId, string docNumber, string title, string revision,
        DocumentType docType, DocumentStatus docStatus,
        string author, DateTime created, CancellationToken ct)
    {
        var exists = await _db.Set<Document>()
            .AnyAsync(d => d.DocumentNumber == docNumber && d.CompanyId == tenantId, ct);
        if (exists) return false;

        var doc = new Document
        {
            DocumentNumber = docNumber,
            Title = title,
            DocumentType = docType,
            Status = docStatus,
            CompanyId = tenantId,
            CreatedBy = author,
            CreatedAt = created,
        };
        _db.Set<Document>().Add(doc);
        await _db.SaveChangesAsync(ct);

        // Add initial version
        var version = new DocumentVersion
        {
            DocumentId = doc.Id,
            RevisionCode = revision,
            VersionNumber = 1,
            Status = docStatus,
            CompanyId = tenantId,
            CreatedBy = author,
            CreatedAt = created,
        };
        _db.Set<DocumentVersion>().Add(version);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create CAR (Corrective Action Request)
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateCarAsync(
        int tenantId, int proId, string carNumber,
        string description, string reporter, DateTime created,
        CancellationToken ct)
    {
        var exists = await _db.Set<CorrectiveActionRequest>()
            .AnyAsync(c => c.CarNumber == carNumber && c.CompanyId == tenantId, ct);
        if (exists) return false;

        var car = new CorrectiveActionRequest
        {
            CarNumber = carNumber,
            Description = description,
            Source = CarSource.ProcessFailure,
            Severity = CarSeverity.Major,
            Status = CarStatus.UnderInvestigation,
            ProductionOrderId = proId,
            IssuedBy = reporter,
            TenantId = tenantId,
            CompanyId = tenantId,
            CreatedBy = reporter,
            CreatedAt = created,
        };
        _db.Set<CorrectiveActionRequest>().Add(car);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Create ECR + ECO pair
    // ═══════════════════════════════════════════════════════════════════

    private async Task<bool> CreateEcrEcoAsync(
        int tenantId, string ecrNumber, string ecrDescription,
        string ecoNumber, string engineer, DateTime created,
        CancellationToken ct)
    {
        var exists = await _db.Set<EngineeringChangeRequest>()
            .AnyAsync(e => e.EcrNumber == ecrNumber && e.CompanyId == tenantId, ct);
        if (exists) return false;

        var ecr = new EngineeringChangeRequest
        {
            EcrNumber = ecrNumber,
            Title = "Adjust bore feed rate to eliminate tool deflection",
            Description = ecrDescription,
            ChangeReason = ChangeReason.QualityIssue,
            Urgency = ChangeUrgency.Expedited,
            Status = EcrStatus.Approved,
            RequestedBy = Engineer1,
            CompanyId = tenantId,
            CreatedBy = engineer,
            CreatedAt = created,
        };
        _db.Set<EngineeringChangeRequest>().Add(ecr);
        await _db.SaveChangesAsync(ct);

        var eco = new EngineeringChangeOrder
        {
            EcoNumber = ecoNumber,
            Title = "ECO: Modify bore operation parameters — PRO-3003",
            SourceEcrId = ecr.Id,
            Status = EcoStatus.Implemented,
            CompanyId = tenantId,
            CreatedBy = engineer,
            CreatedAt = created.AddHours(4),
        };
        _db.Set<EngineeringChangeOrder>().Add(eco);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Resolve site ID
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> ResolveSiteIdAsync(int tenantId, CancellationToken ct)
    {
        // Try Location first (ProductionOrder.LocationId FK), fall back to Site
        var location = await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.CompanyId == tenantId, ct);
        if (location != null) return location.Id;

        var site = await _db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == tenantId, ct);
        return site?.Id ?? 0;
    }

    private static ProductionCockpitDemoSeedResult EmptyResult(
        int companyId, string code, List<string> warnings) =>
        new(companyId, code, AlreadySeeded: false,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, warnings);
}
