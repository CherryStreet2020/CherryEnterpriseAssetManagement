using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    // ADR-012 v0.2 / PR #119.2 — Seeds WorkOrderFieldVisibility with
    // global defaults for the five unified-WorkOrder classifications.
    //
    // ~75 rows covering the high-value header fields × 5 classifications.
    // Per-classification labels match the regulator-blessed terminology
    // operators expect (FailureCode under Maintenance vs HazardCode under
    // HSE, etc.).
    //
    // Idempotent: bails if the table already has rows for the global scope
    // (TenantId IS NULL). forceReseed=true wipes and re-runs.
    //
    // Tenant overrides land in additional rows (TenantId != NULL) via the
    // admin endpoint (Sprint 4); this seeder NEVER touches tenant rows.
    public interface IWorkOrderFieldVisibilitySeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class WorkOrderFieldVisibilitySeeder : IWorkOrderFieldVisibilitySeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WorkOrderFieldVisibilitySeeder> _logger;

        public WorkOrderFieldVisibilitySeeder(
            AppDbContext db,
            ILogger<WorkOrderFieldVisibilitySeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            var existingGlobal = await _db.WorkOrderFieldVisibility
                .Where(v => v.TenantId == null)
                .CountAsync();

            if (existingGlobal > 0 && !forceReseed)
            {
                _logger.LogInformation(
                    "WorkOrderFieldVisibilitySeeder: skipping, {Count} global rows already present.",
                    existingGlobal);
                return 0;
            }

            if (forceReseed && existingGlobal > 0)
            {
                _logger.LogWarning(
                    "WorkOrderFieldVisibilitySeeder: forceReseed=true; wiping global rows.");
                var globals = _db.WorkOrderFieldVisibility.Where(v => v.TenantId == null);
                _db.WorkOrderFieldVisibility.RemoveRange(globals);
                await _db.SaveChangesAsync();
            }

            var now = DateTime.UtcNow;
            var rows = BuildDefaultRows(now);
            await _db.WorkOrderFieldVisibility.AddRangeAsync(rows);
            var saved = await _db.SaveChangesAsync();
            _logger.LogInformation(
                "WorkOrderFieldVisibilitySeeder: seeded {Count} global default rows.",
                saved);
            return saved;
        }

        // ---- Default layout rules ----
        //
        // The renderer expects sections in this order (controlled by
        // MIN DisplayOrder per section in the service):
        //   "Identification"     — 1xx range
        //   "Asset & Location"   — 2xx range
        //   "Scheduling"         — 3xx range
        //   "People"             — 4xx range
        //   "Resolution"         — 5xx range
        //   "Cost"               — 6xx range
        //   "Vendor"             — 7xx range
        //   "Approval"           — 8xx range
        //   "External"           — 9xx range
        //   Per-classification — "Capital Asset" / "Quality NCR" /
        //   "Engineering Change" / "Incident" / "JSA" → bxx range
        //   (b = classification-specific; ships from satellite tables in
        //   Phase D and gets its own seeder rows there).
        //
        // Below: 75 rows of high-value defaults per the field-research
        // gap analysis in SPRINT3_PLAN_v0.2.md.
        private static List<WorkOrderFieldVisibility> BuildDefaultRows(DateTime now)
        {
            var rows = new List<WorkOrderFieldVisibility>();
            void R(WorkOrderClassification cls, string field, FieldVisibility vis,
                   int order, string section, string? label = null, string? help = null)
            {
                rows.Add(new WorkOrderFieldVisibility
                {
                    Classification = cls,
                    FieldName = field,
                    Visibility = vis,
                    DisplayOrder = order,
                    SectionName = section,
                    DisplayLabel = label,
                    HelpText = help,
                    TenantId = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            // ==== MAINTENANCE — the existing form, codified as defaults ====
            R(WorkOrderClassification.Maintenance, "WorkOrderNumber",  FieldVisibility.ReadOnly, 100, "Identification");
            R(WorkOrderClassification.Maintenance, "Description",      FieldVisibility.Required, 110, "Identification");
            R(WorkOrderClassification.Maintenance, "Type",             FieldVisibility.Required, 120, "Identification", "Maintenance Type");
            R(WorkOrderClassification.Maintenance, "Priority",         FieldVisibility.Required, 130, "Identification");
            R(WorkOrderClassification.Maintenance, "Status",           FieldVisibility.ReadOnly, 140, "Identification");
            R(WorkOrderClassification.Maintenance, "ScheduledDate",    FieldVisibility.Required, 310, "Scheduling");
            R(WorkOrderClassification.Maintenance, "NextScheduledDate",FieldVisibility.Optional, 320, "Scheduling",
                help: "Auto-populated for PM occurrences from the schedule.");
            R(WorkOrderClassification.Maintenance, "RecurrenceIntervalDays", FieldVisibility.Optional, 330, "Scheduling");
            R(WorkOrderClassification.Maintenance, "TechnicianId",     FieldVisibility.Required, 410, "People");
            R(WorkOrderClassification.Maintenance, "Vendor",           FieldVisibility.Optional, 710, "Vendor");
            R(WorkOrderClassification.Maintenance, "PurchaseOrderNumber", FieldVisibility.Optional, 720, "Vendor");
            R(WorkOrderClassification.Maintenance, "FailureCodeId",    FieldVisibility.Optional, 510, "Resolution", "Failure Code");
            R(WorkOrderClassification.Maintenance, "RootCause",        FieldVisibility.Optional, 520, "Resolution");
            R(WorkOrderClassification.Maintenance, "CorrectiveAction", FieldVisibility.Optional, 530, "Resolution");
            R(WorkOrderClassification.Maintenance, "Resolution",       FieldVisibility.Optional, 540, "Resolution");
            R(WorkOrderClassification.Maintenance, "ResolutionSummary",FieldVisibility.Optional, 550, "Resolution");
            R(WorkOrderClassification.Maintenance, "LessonsLearned",   FieldVisibility.Optional, 560, "Resolution");
            R(WorkOrderClassification.Maintenance, "DowntimeHours",    FieldVisibility.Optional, 610, "Cost");
            R(WorkOrderClassification.Maintenance, "LaborHours",       FieldVisibility.Optional, 620, "Cost");
            R(WorkOrderClassification.Maintenance, "OvertimeHours",    FieldVisibility.Optional, 630, "Cost");
            R(WorkOrderClassification.Maintenance, "EstimatedCost",    FieldVisibility.Optional, 640, "Cost");
            R(WorkOrderClassification.Maintenance, "ActualCost",       FieldVisibility.Optional, 650, "Cost");
            R(WorkOrderClassification.Maintenance, "ApprovalStatus",   FieldVisibility.Optional, 810, "Approval");
            R(WorkOrderClassification.Maintenance, "ExternalWorkOrderId", FieldVisibility.Optional, 910, "External");
            R(WorkOrderClassification.Maintenance, "ExternalSource",   FieldVisibility.Optional, 920, "External");

            // ==== QUALITY — NCR/CAPA/8D shape ====
            R(WorkOrderClassification.Quality, "WorkOrderNumber",  FieldVisibility.ReadOnly, 100, "Identification", "NCR Number");
            R(WorkOrderClassification.Quality, "Description",      FieldVisibility.Required, 110, "Identification", "Nonconformance Description");
            R(WorkOrderClassification.Quality, "Priority",         FieldVisibility.Required, 130, "Identification");
            R(WorkOrderClassification.Quality, "Status",           FieldVisibility.ReadOnly, 140, "Identification");
            R(WorkOrderClassification.Quality, "ScheduledDate",    FieldVisibility.Optional, 310, "Scheduling", "Containment Target Date");
            R(WorkOrderClassification.Quality, "RootCause",        FieldVisibility.Required, 510, "Resolution", "Root Cause");
            R(WorkOrderClassification.Quality, "CorrectiveAction", FieldVisibility.Required, 520, "Resolution",
                help: "Permanent corrective action (per 8D D5).");
            R(WorkOrderClassification.Quality, "LessonsLearned",   FieldVisibility.Optional, 530, "Resolution",
                help: "Recognition + systemic prevention (per 8D D7/D8).");
            R(WorkOrderClassification.Quality, "TechnicianId",     FieldVisibility.Optional, 410, "People", "Quality Engineer");
            R(WorkOrderClassification.Quality, "ApprovalStatus",   FieldVisibility.Required, 810, "Approval", "QA Disposition Approval");
            R(WorkOrderClassification.Quality, "ExternalWorkOrderId", FieldVisibility.Optional, 910, "External", "Customer Complaint #");

            // ==== ENGINEERING — ECO / MOC shape ====
            R(WorkOrderClassification.Engineering, "WorkOrderNumber", FieldVisibility.ReadOnly, 100, "Identification", "ECO / MOC Number");
            R(WorkOrderClassification.Engineering, "Description",     FieldVisibility.Required, 110, "Identification", "Change Description");
            R(WorkOrderClassification.Engineering, "Revision",        FieldVisibility.Required, 115, "Identification",
                help: "Increments on each re-issue. See ASME Y14.35.");
            R(WorkOrderClassification.Engineering, "Priority",        FieldVisibility.Required, 130, "Identification");
            R(WorkOrderClassification.Engineering, "Status",          FieldVisibility.ReadOnly, 140, "Identification");
            R(WorkOrderClassification.Engineering, "ScheduledDate",   FieldVisibility.Required, 310, "Scheduling", "Effective Date");
            R(WorkOrderClassification.Engineering, "TechnicianId",    FieldVisibility.Optional, 410, "People", "Lead Engineer");
            R(WorkOrderClassification.Engineering, "Notes",           FieldVisibility.Optional, 510, "Resolution", "Impact Assessment");
            R(WorkOrderClassification.Engineering, "ApprovalStatus",  FieldVisibility.Required, 810, "Approval", "CCB Approval",
                help: "Change Control Board approval. MOC under PSM also requires PSSR completion.");
            R(WorkOrderClassification.Engineering, "ExternalWorkOrderId", FieldVisibility.Optional, 910, "External");

            // ==== HSE — Incident / JSA shape ====
            R(WorkOrderClassification.HSE, "WorkOrderNumber",     FieldVisibility.ReadOnly, 100, "Identification", "Incident / JSA Number");
            R(WorkOrderClassification.HSE, "Description",         FieldVisibility.Required, 110, "Identification", "Event Description");
            R(WorkOrderClassification.HSE, "Priority",            FieldVisibility.Required, 130, "Identification", "Risk Score");
            R(WorkOrderClassification.HSE, "Status",              FieldVisibility.ReadOnly, 140, "Identification");
            R(WorkOrderClassification.HSE, "ScheduledDate",       FieldVisibility.Required, 310, "Scheduling", "Event Date");
            R(WorkOrderClassification.HSE, "TechnicianId",        FieldVisibility.Required, 410, "People", "EHS Investigator");
            R(WorkOrderClassification.HSE, "FailureCodeId",       FieldVisibility.Optional, 510, "Resolution", "Hazard Category");
            R(WorkOrderClassification.HSE, "RootCause",           FieldVisibility.Required, 520, "Resolution");
            R(WorkOrderClassification.HSE, "CorrectiveAction",    FieldVisibility.Required, 530, "Resolution",
                help: "Apply the hierarchy of controls (Eliminate → Substitute → Engineering → Admin → PPE).");
            R(WorkOrderClassification.HSE, "Resolution",          FieldVisibility.Optional, 540, "Resolution");
            R(WorkOrderClassification.HSE, "ResolutionSummary",   FieldVisibility.Required, 550, "Resolution");
            R(WorkOrderClassification.HSE, "LessonsLearned",      FieldVisibility.Required, 560, "Resolution",
                help: "Required for OSHA 300 closure.");
            R(WorkOrderClassification.HSE, "ApprovalStatus",      FieldVisibility.Required, 810, "Approval", "EHS Director Approval");

            // ==== CIP — Capital project shape ====
            R(WorkOrderClassification.CIP, "WorkOrderNumber",  FieldVisibility.ReadOnly, 100, "Identification", "CIP / AFE Number");
            R(WorkOrderClassification.CIP, "Description",      FieldVisibility.Required, 110, "Identification", "Project Scope");
            R(WorkOrderClassification.CIP, "Revision",         FieldVisibility.Required, 115, "Identification",
                help: "Increments on AFE revisions and change orders.");
            R(WorkOrderClassification.CIP, "Priority",         FieldVisibility.Required, 130, "Identification");
            R(WorkOrderClassification.CIP, "Status",           FieldVisibility.ReadOnly, 140, "Identification");
            R(WorkOrderClassification.CIP, "ScheduledDate",    FieldVisibility.Required, 310, "Scheduling", "Planned Start");
            R(WorkOrderClassification.CIP, "CompletedDate",    FieldVisibility.Optional, 320, "Scheduling", "Substantial Completion Date",
                help: "When this date is set and the WO moves to SubstantialComplete, the costs reclassify to the target Fixed Asset and depreciation begins.");
            R(WorkOrderClassification.CIP, "TechnicianId",     FieldVisibility.Required, 410, "People", "Project Manager");
            R(WorkOrderClassification.CIP, "Vendor",           FieldVisibility.Optional, 710, "Vendor", "Primary Contractor");
            R(WorkOrderClassification.CIP, "PurchaseOrderNumber", FieldVisibility.Optional, 720, "Vendor", "Contract / PO Number");
            R(WorkOrderClassification.CIP, "EstimatedCost",    FieldVisibility.Required, 610, "Cost", "Approved Budget (AFE)");
            R(WorkOrderClassification.CIP, "ActualCost",       FieldVisibility.ReadOnly, 620, "Cost", "Costs Incurred");
            R(WorkOrderClassification.CIP, "LaborCost",        FieldVisibility.Optional, 630, "Cost");
            R(WorkOrderClassification.CIP, "MaterialsCost",    FieldVisibility.Optional, 640, "Cost");
            R(WorkOrderClassification.CIP, "OutsideVendorCost",FieldVisibility.Optional, 650, "Cost", "Contracted Services");
            R(WorkOrderClassification.CIP, "ApprovalStatus",   FieldVisibility.Required, 810, "Approval", "AFE Approval Chain",
                help: "Tier-based approval per company threshold. JV projects also require partner sign-off.");
            R(WorkOrderClassification.CIP, "ExternalWorkOrderId", FieldVisibility.Optional, 910, "External");
            R(WorkOrderClassification.CIP, "ExternalSource",   FieldVisibility.Optional, 920, "External", "Source System",
                help: "e.g. SAP-PS, Oracle-PPM, ManualImport");

            return rows;
        }
    }
}
