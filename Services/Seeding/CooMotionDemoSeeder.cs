// =============================================================================
// Sprint 12.8 PR #5c.1 — CooMotionDemoSeeder (skeleton)
//
// Idempotent demo-data seeder that creates a 10-level precision-machining
// scenario for the COO motion (the production-side sibling to the CFO motion
// shipped in PR #353).
//
// SCOPE OF THIS PR (#5c.1 — skeleton):
//   - Locations (2): PWH-CAN-MAIN + PWH-CAN-NORTH (the cross-plant beat)
//   - CustomerProject (1): "Precision Bracket Assembly — Q3 2026 (Demo)"
//                          + 4 ProjectPhases (Engineering / Procurement /
//                          Production / Delivery)
//   - MaterialStructures (10) + MaterialStructureLines (parent BOM with 9
//     sub-assembly references; each sub-BOM with 1-3 component lines)
//   - Routings (10) + RoutingOperations (3-5 ops each, ONE op of type
//     Subcontract on PRO-2026-1007 to "Heritage Heat Treat (Demo)")
//   - ProductionOrders (10): 1 parent + 9 children via ADR-028
//     ParentProductionOrderId FK. 7 orders at PWH-CAN-MAIN, last 3 at
//     PWH-CAN-NORTH. Pre-stamped MaterialCost / LaborCost / OverheadCost /
//     SubcontractCost / ActualCost columns (Sprint 12.8 PR #349 schema).
//   - ProductionOperations: created by calling the existing
//     IProductionOperationService.ReleaseFromRoutingAsync for each PRO
//     (snapshot discipline — same path the floor uses at release time).
//   - Status mix on ProductionOperations: 30% Completed / 40% Running+InSetup
//     / 30% Scheduled — the "in-flight feel" for the demo.
//   - Backward-schedule: call IBackwardSchedulingService.BackwardScheduleAsync
//     against the parent PRO to stamp Scheduled/Planned dates on children.
//
// SCOPE OF FUTURE PR #5c.2 (lineage, NOT this PR):
//   - LaborEntry rows (clock-in/out on Completed + Running ops)
//   - WorkOrderPart kit rows
//   - JournalEntry / JournalLine / AccountingKey rows tagging facility WIP/OH
//
// IDEMPOTENCY ANCHOR: CustomerProject.Code = "DEMO-COO-PROJ-001". If a
// CustomerProject with this code already exists on the demo tenant, the
// seeder treats the entire tree as already-seeded and skips every bucket.
// This is the only natural-key we need because every other entity in the
// tree FK-resolves through the CustomerProject.
//
// SAFETY GUARDS (carried forward from PR #353):
//   1. Lookup demo tenant by CompanyCode = "PWH-CAN" (NEVER by hardcoded Id).
//   2. Verify Company.Name starts with "PWH" before any write. If the
//      placeholder lineage has been changed, refuse to run.
//
// LOCK 14: dev-only. Republish-with-Copy syncs dev DB → prod DB at end of
// sprint. NEVER run against prod directly.
//
// LOCK 12: typed EF Core only; no raw SQL in this file.
//
// LOCK 15: per-bucket SaveChanges + inner-exception walking (carry-forward
// from PR #352).
//
// Source-of-truth design:
//   - docs/research/abs-thursday-demo-data-design-2026-05-26.md
//   - docs/research/pr5c-design-2026-05-26.md (customer-name carve-out layer)
//   - HARD LOCK: never customer names in code/comments/strings — the prior
//     research memo's Rolls-Royce / Mississauga / Burlington references are
//     historical / on-disk only. This NET-NEW code is fully tenant-generic.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services.Production;
using Abs.FixedAssets.Services.Production.BackwardScheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding;

public interface ICooMotionDemoSeeder
{
    /// <summary>
    /// Seed the COO motion demo scenario on the demo tenant (looked up by
    /// <c>CompanyCode = "PWH-CAN"</c>). Idempotent — calling twice yields the
    /// same end state. The whole tree is anchored on the
    /// <c>CustomerProject.Code = "DEMO-COO-PROJ-001"</c> row.
    /// </summary>
    Task<CooMotionDemoSeedResult> SeedAsync(CancellationToken ct);
}

/// <summary>
/// Per-bucket result surfaced to the admin trigger page.
/// </summary>
public sealed record CooMotionDemoSeedResult(
    int CompanyId,
    string CompanyCode,
    string CompanyName,
    bool AlreadySeeded,
    int LocationsCreated,
    int CustomerProjectsCreated,
    int ProjectPhasesCreated,
    int MaterialStructuresCreated,
    int MaterialStructureLinesCreated,
    int RoutingsCreated,
    int RoutingOperationsCreated,
    int ProductionOrdersCreated,
    int ProductionOperationsCreated,
    int OperationsStatusStamped,
    int BackwardScheduledOperations,
    IReadOnlyList<string> Warnings)
{
    public int TotalRowsCreated =>
        LocationsCreated +
        CustomerProjectsCreated +
        ProjectPhasesCreated +
        MaterialStructuresCreated +
        MaterialStructureLinesCreated +
        RoutingsCreated +
        RoutingOperationsCreated +
        ProductionOrdersCreated +
        ProductionOperationsCreated;
}

public sealed class CooMotionDemoSeeder : ICooMotionDemoSeeder
{
    // ----- Tenant guard constants (carry-forward from CfoMotionDemoSeeder) -----
    private const string DemoCompanyCode = "PWH-CAN";
    private const string DemoCompanyExpectedNamePrefix = "PWH";

    // ----- Site / Location codes -----
    // Two Locations: primary site (where most ops run) and the secondary
    // site (the cross-plant beat for the COO walkthrough — operations that
    // require capabilities only available at the second plant).
    private const string PrimaryLocationCode   = "PWH-CAN-MAIN";
    private const string PrimaryLocationName   = "Main Plant (Demo)";
    private const string SecondaryLocationCode = "PWH-CAN-NORTH";
    private const string SecondaryLocationName = "North Plant (Demo)";

    // ----- Idempotency anchor -----
    // If a CustomerProject with this code exists on the demo tenant, the
    // whole tree is treated as already-seeded.
    private const string DemoProjectCode = "DEMO-COO-PROJ-001";
    private const string DemoProjectName = "Precision Bracket Assembly — Q3 2026 (Demo)";

    // ----- Backward-schedule anchor -----
    // Ship date for the demo scenario. The backward scheduler walks each
    // child's ScheduledEnd backward through Routing operations to compute
    // PlannedStart / PlannedEnd. Same shape Sprint 14 will eventually use.
    private static readonly DateTime ShipDate = new DateTime(2026, 8, 15);

    // ----- The 10 ProductionOrders -----
    // OrderNumber follows the existing convention ("PRO-YYYY-NNNN"). The
    // tenant scope + idempotency anchor is the parent CustomerProject; we
    // don't tag the order numbers with DEMO-COO-* because the existing
    // convention is the right one for natural-key-style lookups elsewhere
    // in the app.
    //
    // Cost split (rolls up at parent):
    //   Parent (PRO-2026-1000): MaterialCost $52k, LaborCost $48k, OverheadCost $72k, SubcontractCost $5k → ActualCost ~$177k
    //   Children sum a bit less than parent (parent rollup includes assembly OH).
    //
    // Site split: 7 at PrimaryLocation, 3 at SecondaryLocation (the last
    // three indices) — that's the cross-plant beat for the walkthrough.
    private static readonly (string OrderNumber, string Title, bool IsParent, bool IsSecondary,
        decimal MaterialCost, decimal LaborCost, decimal OverheadCost, decimal SubcontractCost)[]
        OrderTemplates =
    {
        ("PRO-2026-1000", "Precision Bracket Assembly (parent) (Demo)",         IsParent: true,  IsSecondary: false, 52_000m, 48_000m, 72_000m, 5_000m),
        ("PRO-2026-1001", "Bracket Body — 5-Axis Milled (Demo)",                IsParent: false, IsSecondary: false, 12_000m, 11_000m, 14_500m, 0m),
        ("PRO-2026-1002", "Mounting Plate — 3-Axis Milled (Demo)",              IsParent: false, IsSecondary: false,  7_500m,  6_800m,  7_900m, 0m),
        ("PRO-2026-1003", "Reinforcement Rib (set of 4) (Demo)",                IsParent: false, IsSecondary: false,  6_000m,  5_400m,  6_400m, 0m),
        ("PRO-2026-1004", "Pivot Sleeve — Turned (Demo)",                       IsParent: false, IsSecondary: false,  4_800m,  4_200m,  4_900m, 0m),
        ("PRO-2026-1005", "Cap Bolt Subassembly (Demo)",                        IsParent: false, IsSecondary: false,  3_200m,  2_800m,  3_100m, 0m),
        ("PRO-2026-1006", "Bushing Set — Turned (Demo)",                        IsParent: false, IsSecondary: false,  3_800m,  3_300m,  3_700m, 0m),
        ("PRO-2026-1007", "Heat-Treated Pivot Pin (subcontract) (Demo)",        IsParent: false, IsSecondary: true,   3_500m,  3_100m,  3_600m, 5_000m),
        ("PRO-2026-1008", "Welded Stiffener Frame (Demo)",                      IsParent: false, IsSecondary: true,   9_000m,  8_200m,  9_500m, 0m),
        ("PRO-2026-1009", "Final FAI + Pack (Demo)",                            IsParent: false, IsSecondary: true,   4_500m,  4_100m,  4_800m, 0m),
    };

    // ----- The 4 ProjectPhases -----
    private static readonly (string Code, string Name)[] PhaseTemplates =
    {
        ("ENG",  "Engineering"),
        ("PROC", "Procurement"),
        ("PROD", "Production"),
        ("DEL",  "Delivery"),
    };

    // ----- Per-order operation templates -----
    // Each ProductionOrder gets a Routing with this many ops (mix of
    // OperationType.Run / Setup / Inspect / Move / Wait / Subcontract).
    // Order index 7 (PRO-2026-1007) has a Subcontract op.
    private static readonly int[] OperationCountsPerOrder =
    {
        4, // parent (1000) — final assembly + FAI + pack
        5, // 1001 — Bracket Body
        4, // 1002 — Mounting Plate
        3, // 1003 — Reinforcement Rib
        3, // 1004 — Pivot Sleeve
        3, // 1005 — Cap Bolt Subassembly
        3, // 1006 — Bushing Set
        4, // 1007 — Heat-Treated Pivot Pin (incl. Subcontract op)
        4, // 1008 — Welded Stiffener Frame
        3, // 1009 — Final FAI + Pack
    };

    // ----- Subcontract vendor placeholder name -----
    // Generic — not a real supplier. Pattern: "Demo " prefix so search
    // surfaces find it cleanly and a non-existent-vendor INSERT creates one.
    private const string SubcontractVendorName = "Heritage Heat Treat (Demo)";

    // ----- DI -----
    private readonly AppDbContext _db;
    private readonly IProductionOperationService _productionOps;
    private readonly IBackwardSchedulingService _scheduler;
    private readonly ILogger<CooMotionDemoSeeder> _logger;

    public CooMotionDemoSeeder(
        AppDbContext db,
        IProductionOperationService productionOps,
        IBackwardSchedulingService scheduler,
        ILogger<CooMotionDemoSeeder> logger)
    {
        _db = db;
        _productionOps = productionOps;
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task<CooMotionDemoSeedResult> SeedAsync(CancellationToken ct)
    {
        var warnings = new List<string>();

        // ---------- Tenant resolution + safety guard ----------
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyCode == DemoCompanyCode, ct);

        if (company is null)
        {
            warnings.Add(
                $"Demo tenant CompanyCode='{DemoCompanyCode}' not found in Companies table. " +
                "Run the SeedPackExecutor (or equivalent placeholder seed) first.");
            return EmptyResult(0, DemoCompanyCode, "<unknown>", warnings);
        }

        if (string.IsNullOrEmpty(company.Name) ||
            !company.Name.StartsWith(DemoCompanyExpectedNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"Demo tenant CompanyCode='{DemoCompanyCode}' resolves to Company.Name='{company.Name}' " +
                $"which does NOT start with the expected '{DemoCompanyExpectedNamePrefix}' prefix. " +
                "Refusing to write demo data to a tenant that may not be a placeholder. " +
                "If you've intentionally renamed PWH-* tenants, update DemoCompanyExpectedNamePrefix.");
            return EmptyResult(company.Id, company.CompanyCode, company.Name, warnings);
        }

        var tenantId = company.Id;

        // ---------- Idempotency check ----------
        var existingProject = await _db.Set<CustomerProject>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == tenantId && p.Code == DemoProjectCode, ct);
        if (existingProject is not null)
        {
            return new CooMotionDemoSeedResult(
                tenantId, company.CompanyCode, company.Name,
                AlreadySeeded: true,
                LocationsCreated: 0, CustomerProjectsCreated: 0, ProjectPhasesCreated: 0,
                MaterialStructuresCreated: 0, MaterialStructureLinesCreated: 0,
                RoutingsCreated: 0, RoutingOperationsCreated: 0,
                ProductionOrdersCreated: 0, ProductionOperationsCreated: 0,
                OperationsStatusStamped: 0, BackwardScheduledOperations: 0,
                Warnings: warnings);
        }

        // ---------- Locate prerequisite data ----------
        // Need: WorkCenters (the WorkCenter the RoutingOperations point at),
        // Items (the OutputItemId on each MaterialStructure + ProductionOrder
        // header), and a fallback Item for component lines.
        var workCenters = await _db.WorkCenters.AsNoTracking()
            .Where(w => w.CompanyId == tenantId)
            .OrderBy(w => w.Id)
            .ToListAsync(ct);
        if (workCenters.Count == 0)
        {
            warnings.Add(
                $"No WorkCenters found for tenant '{DemoCompanyCode}'. " +
                "RoutingOperations require WorkCenterId — seeder cannot proceed.");
            return EmptyResult(tenantId, company.CompanyCode, company.Name, warnings);
        }

        var items = await _db.Items.AsNoTracking()
            .Where(i => i.CompanyId == tenantId)
            .OrderBy(i => i.Id)
            .Take(20)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            warnings.Add(
                $"No Items found for tenant '{DemoCompanyCode}'. " +
                "ProductionOrder.ItemId and MaterialStructure.OutputItemId require an Item — " +
                "seeder cannot proceed.");
            return EmptyResult(tenantId, company.CompanyCode, company.Name, warnings);
        }

        // ---------- Per-bucket seeding ----------
        var locationsCreated = 0;
        var customerProjectsCreated = 0;
        var projectPhasesCreated = 0;
        var materialStructuresCreated = 0;
        var materialStructureLinesCreated = 0;
        var routingsCreated = 0;
        var routingOperationsCreated = 0;
        var productionOrdersCreated = 0;
        var productionOperationsCreated = 0;
        var operationsStatusStamped = 0;
        var backwardScheduledOperations = 0;

        // Bucket 1: Locations
        Location? primaryLocation = null;
        Location? secondaryLocation = null;
        await TrySaveBucketAsync("Locations", async () =>
        {
            (primaryLocation, secondaryLocation, locationsCreated) =
                await EnsureLocationsAsync(tenantId, ct);
        }, warnings, ct);

        if (primaryLocation is null || secondaryLocation is null)
        {
            warnings.Add("Location resolution failed — cannot continue.");
            return BuildResult(tenantId, company,
                locationsCreated, customerProjectsCreated, projectPhasesCreated,
                materialStructuresCreated, materialStructureLinesCreated,
                routingsCreated, routingOperationsCreated,
                productionOrdersCreated, productionOperationsCreated,
                operationsStatusStamped, backwardScheduledOperations, warnings);
        }

        // Bucket 2: CustomerProject + ProjectPhases
        CustomerProject? customerProject = null;
        await TrySaveBucketAsync("CustomerProject + Phases", async () =>
        {
            (customerProject, customerProjectsCreated, projectPhasesCreated) =
                await SeedCustomerProjectAsync(tenantId, ct);
        }, warnings, ct);

        if (customerProject is null)
        {
            warnings.Add("CustomerProject creation failed — cannot continue.");
            return BuildResult(tenantId, company,
                locationsCreated, customerProjectsCreated, projectPhasesCreated,
                materialStructuresCreated, materialStructureLinesCreated,
                routingsCreated, routingOperationsCreated,
                productionOrdersCreated, productionOperationsCreated,
                operationsStatusStamped, backwardScheduledOperations, warnings);
        }

        // Bucket 3: MaterialStructures + lines
        var structuresByOrderIndex = new Dictionary<int, MaterialStructure>();
        await TrySaveBucketAsync("MaterialStructures + Lines", async () =>
        {
            (structuresByOrderIndex, materialStructuresCreated, materialStructureLinesCreated) =
                await SeedMaterialStructuresAsync(tenantId, primaryLocation.Id, items, ct);
        }, warnings, ct);

        // Bucket 4: Routings + RoutingOperations
        var routingsByOrderIndex = new Dictionary<int, Routing>();
        await TrySaveBucketAsync("Routings + Operations", async () =>
        {
            (routingsByOrderIndex, routingsCreated, routingOperationsCreated) =
                await SeedRoutingsAsync(
                    tenantId, primaryLocation.Id, secondaryLocation.Id,
                    workCenters, items, ct);
        }, warnings, ct);

        // Bucket 5: ProductionOrders (parent + children)
        var ordersByIndex = new Dictionary<int, ProductionOrder>();
        await TrySaveBucketAsync("ProductionOrders", async () =>
        {
            (ordersByIndex, productionOrdersCreated) =
                await SeedProductionOrdersAsync(
                    tenantId, customerProject.Id,
                    primaryLocation.Id, secondaryLocation.Id,
                    items, structuresByOrderIndex, ct);
        }, warnings, ct);

        // Bucket 6: ProductionOperations via ReleaseFromRoutingAsync
        // Snapshot the routing into per-order operations using the same
        // service the floor uses at release time. Saves us replicating the
        // snapshot logic in the seeder.
        await TrySaveBucketAsync("ProductionOperations via Release", async () =>
        {
            productionOperationsCreated = await ReleaseProductionOperationsAsync(
                ordersByIndex, routingsByOrderIndex, warnings, ct);
        }, warnings, ct);

        // Bucket 7: Stamp 30/40/30 status mix on the just-released operations
        await TrySaveBucketAsync("Operation status mix", async () =>
        {
            operationsStatusStamped = await StampOperationStatusMixAsync(
                ordersByIndex, ct);
        }, warnings, ct);

        // Bucket 8: BackwardSchedule the parent — stamps ScheduledStart/End
        // on each child + PlannedStart/End on the released operations.
        await TrySaveBucketAsync("BackwardSchedule", async () =>
        {
            if (ordersByIndex.TryGetValue(0, out var parent))
            {
                // Seed an end-of-window ScheduledEnd on the parent so the
                // scheduler has an anchor to walk back from.
                var trackedParent = await _db.ProductionOrders
                    .FirstAsync(p => p.Id == parent.Id, ct);
                trackedParent.ScheduledEnd ??= ShipDate;
                await _db.SaveChangesAsync(ct);

                var outcome = await _scheduler.BackwardScheduleAsync(parent.Id, ct);
                if (outcome.IsSuccess)
                {
                    backwardScheduledOperations = outcome.Value.OperationsStamped;
                }
                else
                {
                    warnings.Add(
                        $"BackwardSchedule reported: {outcome.Error}. " +
                        "Demo data still landed but Planned dates may be unset on children.");
                }
            }
        }, warnings, ct);

        return BuildResult(tenantId, company,
            locationsCreated, customerProjectsCreated, projectPhasesCreated,
            materialStructuresCreated, materialStructureLinesCreated,
            routingsCreated, routingOperationsCreated,
            productionOrdersCreated, productionOperationsCreated,
            operationsStatusStamped, backwardScheduledOperations, warnings);
    }

    // -------------------------------------------------------------------------
    // Bucket 1 — Locations
    // -------------------------------------------------------------------------
    private async Task<(Location? primary, Location? secondary, int created)>
        EnsureLocationsAsync(int tenantId, CancellationToken ct)
    {
        int created = 0;

        var primary = await _db.Locations
            .FirstOrDefaultAsync(l => l.CompanyId == tenantId && l.Code == PrimaryLocationCode, ct);
        if (primary is null)
        {
            primary = new Location
            {
                Code = PrimaryLocationCode,
                Name = PrimaryLocationName,
                Description = "Primary precision-machining site for the COO motion demo.",
                Type = LocationType.Building,
                CompanyId = tenantId,
                IsActive = true,
                HierarchyLevel = 0,
            };
            _db.Locations.Add(primary);
            created++;
        }

        var secondary = await _db.Locations
            .FirstOrDefaultAsync(l => l.CompanyId == tenantId && l.Code == SecondaryLocationCode, ct);
        if (secondary is null)
        {
            secondary = new Location
            {
                Code = SecondaryLocationCode,
                Name = SecondaryLocationName,
                Description = "Secondary site (the cross-plant beat for the COO walkthrough).",
                Type = LocationType.Building,
                CompanyId = tenantId,
                IsActive = true,
                HierarchyLevel = 0,
            };
            _db.Locations.Add(secondary);
            created++;
        }

        return (primary, secondary, created);
    }

    // -------------------------------------------------------------------------
    // Bucket 2 — CustomerProject + 4 ProjectPhases
    // -------------------------------------------------------------------------
    private async Task<(CustomerProject? project, int projectCreated, int phasesCreated)>
        SeedCustomerProjectAsync(int tenantId, CancellationToken ct)
    {
        // Code = DemoProjectCode is the idempotency anchor (already-checked).
        var project = new CustomerProject
        {
            CompanyId = tenantId,
            Code = DemoProjectCode,
            Name = DemoProjectName,
            Description = "Hand-crafted demo scenario for the COO motion walkthrough. " +
                          "Targets the seeded PWH-CAN placeholder tenant. " +
                          "No real customer / OEM / program references.",
            Status = CustomerProjectStatus.Active,
            Mode = CustomerProjectMode.Standard,
            CostingMode = CustomerProjectCostingMode.Aggregate,
            RevenueMode = CustomerProjectRevenueMode.CompletedContract,
            PrimaryCustomerId = null,                     // Internal-R&D mode (no named customer)
            Currency = "CAD",
            TargetStartDate = ShipDate.AddMonths(-3),
            TargetEndDate = ShipDate,
            ProjectManagerName = "Demo Project Manager",
            CreatedBy = "DemoSeeder",
        };
        _db.Set<CustomerProject>().Add(project);
        await _db.SaveChangesAsync(ct);             // Flush early so phase FK can resolve

        int phaseSort = 0;
        foreach (var (code, name) in PhaseTemplates)
        {
            _db.Set<ProjectPhase>().Add(new ProjectPhase
            {
                CustomerProjectId = project.Id,
                Code = code,
                Name = name,
                Description = $"{name} phase for the demo project.",
                SortOrder = phaseSort++,
                CreatedBy = "DemoSeeder",
            });
        }

        return (project, projectCreated: 1, phasesCreated: PhaseTemplates.Length);
    }

    // -------------------------------------------------------------------------
    // Bucket 3 — MaterialStructures + Lines
    // -------------------------------------------------------------------------
    private async Task<(Dictionary<int, MaterialStructure>, int structures, int lines)>
        SeedMaterialStructuresAsync(int tenantId, int primaryLocationId,
            IReadOnlyList<Item> items, CancellationToken ct)
    {
        var byIndex = new Dictionary<int, MaterialStructure>();
        int structures = 0, lines = 0;

        // One BOM per order. Parent BOM (index 0) has 9 sub-assembly lines
        // referencing the OutputItem of children #1-9. Each child BOM has
        // 1-3 raw-component lines.
        for (int i = 0; i < OrderTemplates.Length; i++)
        {
            var outputItem = items[i % items.Count];
            var ms = new MaterialStructure
            {
                CompanyId = tenantId,
                LocationId = null,
                IsSiteWideTemplate = true,
                StructureNumber = $"DEMO-COO-BOM-{(i + 1):D3}",
                Name = $"BOM — {OrderTemplates[i].Title}",
                StructureType = StructureType.Bom,
                Status = MaterialStructureStatus.Approved,
                Revision = "A",
                OutputItemId = outputItem.Id,
                ApprovedAt = DateTime.UtcNow.AddMonths(-1),
                ApprovedBy = "DemoSeeder",
                CreatedBy = "DemoSeeder",
            };
            _db.Set<MaterialStructure>().Add(ms);
            structures++;
            byIndex[i] = ms;
        }
        await _db.SaveChangesAsync(ct);

        // Now create the lines. Need the child structures' OutputItemIds
        // for the parent BOM's references.
        for (int i = 0; i < OrderTemplates.Length; i++)
        {
            var ms = byIndex[i];
            int sequence = 10;

            if (i == 0)
            {
                // Parent BOM: 9 lines referencing children's output items.
                for (int childIdx = 1; childIdx < OrderTemplates.Length; childIdx++)
                {
                    var childItemId = byIndex[childIdx].OutputItemId ?? items[childIdx % items.Count].Id;
                    _db.Set<MaterialStructureLine>().Add(new MaterialStructureLine
                    {
                        MaterialStructureId = ms.Id,
                        ItemId = childItemId,
                        LineKind = LineKind.Component,
                        Sequence = sequence,
                        Quantity = 1m,
                        Uom = "EA",
                        ScrapPercent = 0m,
                    });
                    lines++;
                    sequence += 10;
                }
            }
            else
            {
                // Child BOMs: 1-3 raw-material lines from the items list.
                // Vary the count by (i % 3) + 1 to get a mix of single / dual / triple component BOMs.
                int lineCount = (i % 3) + 1;
                for (int l = 0; l < lineCount; l++)
                {
                    var rawItem = items[(i + l + 10) % items.Count];
                    _db.Set<MaterialStructureLine>().Add(new MaterialStructureLine
                    {
                        MaterialStructureId = ms.Id,
                        ItemId = rawItem.Id,
                        LineKind = LineKind.Component,
                        Sequence = sequence,
                        Quantity = (decimal)(l + 1),
                        Uom = l == 0 ? "EA" : "LB",
                        ScrapPercent = l == 0 ? 0m : 2m,
                    });
                    lines++;
                    sequence += 10;
                }
            }
        }

        return (byIndex, structures, lines);
    }

    // -------------------------------------------------------------------------
    // Bucket 4 — Routings + RoutingOperations
    // -------------------------------------------------------------------------
    private async Task<(Dictionary<int, Routing>, int routings, int operations)>
        SeedRoutingsAsync(
            int tenantId, int primaryLocationId, int secondaryLocationId,
            IReadOnlyList<WorkCenter> workCenters,
            IReadOnlyList<Item> items,
            CancellationToken ct)
    {
        var byIndex = new Dictionary<int, Routing>();
        int routings = 0, operations = 0;

        for (int i = 0; i < OrderTemplates.Length; i++)
        {
            var tpl = OrderTemplates[i];
            var locationId = tpl.IsSecondary ? secondaryLocationId : primaryLocationId;
            var outputItem = items[i % items.Count];

            var routing = new Routing
            {
                CompanyId = tenantId,
                Code = $"DEMO-COO-ROUT-{(i + 1):D3}",
                RevisionNumber = "A",
                Name = $"Routing — {tpl.Title}",
                Description = $"Standard routing for {tpl.Title}. Demo data.",
                ItemId = outputItem.Id,
                Type = RoutingType.Discrete,
                Status = RoutingStatus.Released,
                LocationId = locationId,
                IsSiteWideTemplate = false,
                EffectiveFrom = DateTime.UtcNow.AddMonths(-2),
                ApprovedBy = "DemoSeeder",
                ApprovedAt = DateTime.UtcNow.AddMonths(-1),
                LotBaseSize = 1m,
                UnitOfMeasure = "EA",
                IsDefault = true,
                CreatedBy = "DemoSeeder",
            };
            _db.Set<Routing>().Add(routing);
            routings++;
            byIndex[i] = routing;
        }
        await _db.SaveChangesAsync(ct);

        // Now add operations to each routing.
        for (int i = 0; i < OrderTemplates.Length; i++)
        {
            var tpl = OrderTemplates[i];
            var routing = byIndex[i];
            var locationId = tpl.IsSecondary ? secondaryLocationId : primaryLocationId;
            int opCount = OperationCountsPerOrder[i];

            for (int o = 0; o < opCount; o++)
            {
                var wc = workCenters[(i + o) % workCenters.Count];
                var opType = ComputeOperationType(i, o, opCount);
                var description = ComputeOperationDescription(opType, o, opCount, tpl.Title);

                _db.Set<RoutingOperation>().Add(new RoutingOperation
                {
                    RoutingId = routing.Id,
                    SequenceNumber = (o + 1) * 10,
                    LocationIdSnapshot = locationId,
                    WorkCenterId = wc.Id,
                    OperationType = opType,
                    Description = description,
                    SetupTimeMins = opType == ProductionOperationType.Setup ? 30m : 15m,
                    RunTimePerUnitMins = opType == ProductionOperationType.Run ? 45m :
                                          opType == ProductionOperationType.Subcontract ? 0m : 10m,
                    QueueTimeMins = 30m,
                    MoveTimeMins = 10m,
                    WaitTimeMins = opType == ProductionOperationType.Wait ? 60m : 0m,
                    YieldPct = 100m,
                    IsParallel = false,
                    IsOptional = false,
                    Instructions = $"Demo instruction for {description}.",
                    CreatedBy = "DemoSeeder",
                });
                operations++;
            }
        }

        return (byIndex, routings, operations);
    }

    private static ProductionOperationType ComputeOperationType(int orderIndex, int opIndex, int opCount)
    {
        // PRO-2026-1007 (orderIndex==7) gets a Subcontract op at position 2 — the heat-treat step.
        if (orderIndex == 7 && opIndex == 2) return ProductionOperationType.Subcontract;
        // First op of each routing is Setup.
        if (opIndex == 0) return ProductionOperationType.Setup;
        // Last op is Inspect.
        if (opIndex == opCount - 1) return ProductionOperationType.Inspect;
        // Otherwise Run.
        return ProductionOperationType.Run;
    }

    private static string ComputeOperationDescription(ProductionOperationType opType,
        int opIndex, int opCount, string orderTitle)
    {
        return opType switch
        {
            ProductionOperationType.Setup       => $"Setup — tooling + fixtures for op {opIndex + 1}",
            ProductionOperationType.Subcontract => "Heat-treat at subcontract vendor",
            ProductionOperationType.Inspect     => "Final inspection (in-process FAI)",
            ProductionOperationType.Move        => "Move to next workcenter",
            ProductionOperationType.Wait        => "Wait — cure / cool",
            _                                   => $"Run op {opIndex + 1} of {opCount}",
        };
    }

    // -------------------------------------------------------------------------
    // Bucket 5 — ProductionOrders (parent + children, with cost stamps)
    // -------------------------------------------------------------------------
    private async Task<(Dictionary<int, ProductionOrder>, int created)>
        SeedProductionOrdersAsync(
            int tenantId, int customerProjectId,
            int primaryLocationId, int secondaryLocationId,
            IReadOnlyList<Item> items,
            IReadOnlyDictionary<int, MaterialStructure> structuresByIndex,
            CancellationToken ct)
    {
        var byIndex = new Dictionary<int, ProductionOrder>();
        int created = 0;

        // Create parent first so child FK can resolve.
        var parentTpl = OrderTemplates[0];
        var parentOutputItem = items[0];
        var parent = new ProductionOrder
        {
            CompanyId = tenantId,
            OrderNumber = parentTpl.OrderNumber,
            Type = ProductionType.JobShop,
            Status = ProductionOrderStatus.InProgress,
            Title = parentTpl.Title,
            Description = "Top-level assembly. Demo seeder.",
            ItemId = parentOutputItem.Id,
            LocationId = primaryLocationId,
            QuantityOrdered = 1m,
            QuantityCompleted = 0m,
            QuantityScrapped = 0m,
            Uom = "EA",
            MaterialCost = parentTpl.MaterialCost,
            LaborCost = parentTpl.LaborCost,
            OverheadCost = parentTpl.OverheadCost,
            SubcontractCost = parentTpl.SubcontractCost,
            ActualCost = parentTpl.MaterialCost + parentTpl.LaborCost +
                         parentTpl.OverheadCost + parentTpl.SubcontractCost,
            Priority = 50,
            MaterialStructureId = structuresByIndex.GetValueOrDefault(0)?.Id,
            CustomerProjectId = customerProjectId,
            ProjectPostingMode = ProjectPostingMode.FinishedItem,
            ScheduledEnd = ShipDate,
            CreatedBy = "DemoSeeder",
        };
        _db.Set<ProductionOrder>().Add(parent);
        await _db.SaveChangesAsync(ct);
        byIndex[0] = parent;
        created++;

        // Create children, linking ParentProductionOrderId → parent.Id
        for (int i = 1; i < OrderTemplates.Length; i++)
        {
            var tpl = OrderTemplates[i];
            var outputItem = items[i % items.Count];
            var locationId = tpl.IsSecondary ? secondaryLocationId : primaryLocationId;

            var child = new ProductionOrder
            {
                CompanyId = tenantId,
                OrderNumber = tpl.OrderNumber,
                Type = ProductionType.JobShop,
                Status = i % 3 == 0 ? ProductionOrderStatus.Completed :
                         i % 3 == 1 ? ProductionOrderStatus.InProgress :
                                      ProductionOrderStatus.Released,
                Title = tpl.Title,
                Description = $"Sub-assembly under {parentTpl.OrderNumber}. Demo seeder.",
                ItemId = outputItem.Id,
                LocationId = locationId,
                QuantityOrdered = 1m,
                QuantityCompleted = i % 3 == 0 ? 1m : 0m,
                QuantityScrapped = 0m,
                Uom = "EA",
                MaterialCost = tpl.MaterialCost,
                LaborCost = tpl.LaborCost,
                OverheadCost = tpl.OverheadCost,
                SubcontractCost = tpl.SubcontractCost,
                ActualCost = tpl.MaterialCost + tpl.LaborCost + tpl.OverheadCost + tpl.SubcontractCost,
                Priority = 50,
                MaterialStructureId = structuresByIndex.GetValueOrDefault(i)?.Id,
                ParentProductionOrderId = parent.Id,
                CustomerProjectId = customerProjectId,
                ProjectPostingMode = ProjectPostingMode.FinishedItem,
                CreatedBy = "DemoSeeder",
            };
            _db.Set<ProductionOrder>().Add(child);
            byIndex[i] = child;
            created++;
        }
        return (byIndex, created);
    }

    // -------------------------------------------------------------------------
    // Bucket 6 — ProductionOperations via the existing release service
    // -------------------------------------------------------------------------
    private async Task<int> ReleaseProductionOperationsAsync(
        IReadOnlyDictionary<int, ProductionOrder> ordersByIndex,
        IReadOnlyDictionary<int, Routing> routingsByIndex,
        List<string> warnings,
        CancellationToken ct)
    {
        int operationsCreated = 0;

        for (int i = 0; i < OrderTemplates.Length; i++)
        {
            if (!ordersByIndex.TryGetValue(i, out var order)) continue;
            if (!routingsByIndex.TryGetValue(i, out var routing)) continue;

            var result = await _productionOps.ReleaseFromRoutingAsync(
                new ReleaseFromRoutingRequest(
                    ProductionOrderId: order.Id,
                    RoutingId: routing.Id,
                    ReleasedBy: "DemoSeeder"),
                ct);

            if (result.IsSuccess)
            {
                operationsCreated += result.Value.Count;
            }
            else
            {
                warnings.Add($"ReleaseFromRouting for {order.OrderNumber} failed: {result.Error}");
            }
        }

        return operationsCreated;
    }

    // -------------------------------------------------------------------------
    // Bucket 7 — Stamp 30/40/30 Completed / Running+InSetup / Scheduled mix
    // -------------------------------------------------------------------------
    private async Task<int> StampOperationStatusMixAsync(
        IReadOnlyDictionary<int, ProductionOrder> ordersByIndex,
        CancellationToken ct)
    {
        int stamped = 0;

        // Walk operations grouped by ProductionOrder. Within each order:
        //   first ~30% of ops by SequenceNumber → Completed
        //   middle ~40% → mix of Running + InSetup
        //   last ~30% → Scheduled (default — but we set explicitly)
        foreach (var (i, order) in ordersByIndex.OrderBy(kv => kv.Key))
        {
            var ops = await _db.Set<ProductionOperation>()
                .Where(op => op.ProductionOrderId == order.Id)
                .OrderBy(op => op.SequenceNumber)
                .ToListAsync(ct);

            if (ops.Count == 0) continue;

            int completedCount = (int)Math.Floor(ops.Count * 0.30);
            int runningCount   = (int)Math.Floor(ops.Count * 0.40);
            // Ensure at least 1 in each bucket where possible.
            if (ops.Count >= 3)
            {
                completedCount = Math.Max(1, completedCount);
                runningCount = Math.Max(1, runningCount);
            }

            int idx = 0;
            for (int c = 0; c < completedCount && idx < ops.Count; c++, idx++)
            {
                ops[idx].Status = ProductionOperationStatus.Completed;
                ops[idx].CompletedQty = 1m;
                ops[idx].ActualStart = DateTime.UtcNow.AddDays(-(7 - idx));
                ops[idx].ActualEnd   = DateTime.UtcNow.AddDays(-(7 - idx)).AddHours(2);
                ops[idx].ActualRunMins = ops[idx].PlannedRunMins;
                stamped++;
            }
            for (int r = 0; r < runningCount && idx < ops.Count; r++, idx++)
            {
                ops[idx].Status = (r % 2 == 0)
                    ? ProductionOperationStatus.Running
                    : ProductionOperationStatus.InSetup;
                ops[idx].ActualStart = DateTime.UtcNow.AddHours(-(r + 1) * 2);
                stamped++;
            }
            for (; idx < ops.Count; idx++)
            {
                ops[idx].Status = ProductionOperationStatus.Scheduled;
                stamped++;
            }
        }

        return stamped;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private CooMotionDemoSeedResult BuildResult(
        int tenantId, Company company,
        int locations, int projects, int phases,
        int ms, int msLines, int routings, int routingOps,
        int orders, int operations, int statusStamped, int scheduled,
        IReadOnlyList<string> warnings) =>
        new(tenantId, company.CompanyCode, company.Name,
            AlreadySeeded: false,
            LocationsCreated: locations,
            CustomerProjectsCreated: projects,
            ProjectPhasesCreated: phases,
            MaterialStructuresCreated: ms,
            MaterialStructureLinesCreated: msLines,
            RoutingsCreated: routings,
            RoutingOperationsCreated: routingOps,
            ProductionOrdersCreated: orders,
            ProductionOperationsCreated: operations,
            OperationsStatusStamped: statusStamped,
            BackwardScheduledOperations: scheduled,
            Warnings: warnings);

    private static CooMotionDemoSeedResult EmptyResult(
        int companyId, string companyCode, string companyName,
        IReadOnlyList<string> warnings) =>
        new(companyId, companyCode, companyName,
            AlreadySeeded: false,
            LocationsCreated: 0, CustomerProjectsCreated: 0, ProjectPhasesCreated: 0,
            MaterialStructuresCreated: 0, MaterialStructureLinesCreated: 0,
            RoutingsCreated: 0, RoutingOperationsCreated: 0,
            ProductionOrdersCreated: 0, ProductionOperationsCreated: 0,
            OperationsStatusStamped: 0, BackwardScheduledOperations: 0,
            Warnings: warnings);

    /// <summary>
    /// Per-bucket SaveChanges + inner-exception walking (carry-forward from
    /// PR #352). Failure in one bucket logs the deepest inner exception and
    /// detaches added entities so the next bucket runs against a clean
    /// change-tracker.
    /// </summary>
    private async Task TrySaveBucketAsync(
        string bucketName, Func<Task> seedWork,
        List<string> warnings, CancellationToken ct)
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
                "CooMotionDemoSeeder bucket {Bucket} failed: {Message}",
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
