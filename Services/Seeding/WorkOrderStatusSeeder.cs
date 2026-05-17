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
    // ADR-012 v0.2 / PR #119.3 — Seeds the per-classification status
    // profiles, labels, and transitions for all five unified-WorkOrder
    // classifications.
    //
    // Maintenance reuses the existing MaintenanceStatus enum values
    // (Scheduled=0, InProgress=1, Completed=2, Cancelled=3, Overdue=4,
    // OnHold=5) so existing Razor pages that cast Status->enum keep
    // working unchanged.
    //
    // Non-Maintenance classifications get their own lifecycle:
    //   Quality:     Reported → Contained → Investigating → Disposition
    //                → ActionImplemented → EffectivenessVerified → Closed
    //   Engineering: Draft → Submitted → TechnicalReview → CcbApproved
    //                → InImplementation → PssrRequired → Effective
    //                → Closed
    //   HSE:         Reported → Acknowledged → Investigating
    //                → RootCauseIdentified → ActionsAssigned
    //                → RecordabilityDetermined → Logged → Closed
    //   CIP:         Initiation → Feasibility → AfeApproved
    //                → Construction → Commissioning
    //                → SubstantialComplete → Closed
    //
    // Idempotent. Bails if any WorkOrderStatusProfile row exists.
    public interface IWorkOrderStatusSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class WorkOrderStatusSeeder : IWorkOrderStatusSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WorkOrderStatusSeeder> _logger;

        public WorkOrderStatusSeeder(AppDbContext db, ILogger<WorkOrderStatusSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            var existing = await _db.Set<WorkOrderStatusProfile>().CountAsync();
            if (existing > 0 && !forceReseed)
            {
                _logger.LogInformation(
                    "WorkOrderStatusSeeder: skipping, {Count} profiles already present.",
                    existing);
                return 0;
            }

            if (forceReseed && existing > 0)
            {
                _logger.LogWarning("WorkOrderStatusSeeder: forceReseed=true; wiping status config.");
                _db.Set<WorkOrderStatusTransition>().RemoveRange(_db.Set<WorkOrderStatusTransition>());
                _db.Set<WorkOrderStatusLabel>().RemoveRange(_db.Set<WorkOrderStatusLabel>());
                _db.Set<WorkOrderStatusProfile>().RemoveRange(_db.Set<WorkOrderStatusProfile>());
                await _db.SaveChangesAsync();
            }

            var now = DateTime.UtcNow;
            var profiles = new List<WorkOrderStatusProfile>();
            var labels = new List<WorkOrderStatusLabel>();
            var transitions = new List<WorkOrderStatusTransition>();

            SeedMaintenance(profiles, labels, transitions, now);
            SeedQuality(profiles, labels, transitions, now);
            SeedEngineering(profiles, labels, transitions, now);
            SeedHse(profiles, labels, transitions, now);
            SeedCip(profiles, labels, transitions, now);

            await _db.Set<WorkOrderStatusProfile>().AddRangeAsync(profiles);
            await _db.Set<WorkOrderStatusLabel>().AddRangeAsync(labels);
            await _db.Set<WorkOrderStatusTransition>().AddRangeAsync(transitions);
            var saved = await _db.SaveChangesAsync();
            _logger.LogInformation(
                "WorkOrderStatusSeeder: seeded {Profiles} profiles, {Labels} labels, {Transitions} transitions.",
                profiles.Count, labels.Count, transitions.Count);
            return saved;
        }

        // ------------------------- helpers -------------------------

        private static void AddLabel(List<WorkOrderStatusLabel> list,
            WorkOrderClassification cls, short code, string key, string label,
            string color, bool terminal, bool holding, int order, DateTime now)
        {
            list.Add(new WorkOrderStatusLabel
            {
                Classification = cls,
                StatusCode = code,
                StatusKey = key,
                DisplayLabel = label,
                DisplayColor = color,
                IsTerminal = terminal,
                IsHolding = holding,
                DisplayOrder = order,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        private static void AddTransition(List<WorkOrderStatusTransition> list,
            WorkOrderClassification cls, short from, short to,
            DateTime now,
            string? actionLabel = null, bool back = false, int order = 100,
            string? requiredApprovalStage = null, string? guardServiceName = null)
        {
            list.Add(new WorkOrderStatusTransition
            {
                Classification = cls,
                FromStatusCode = from,
                ToStatusCode = to,
                ActionLabel = actionLabel,
                IsBackTransition = back,
                DisplayOrder = order,
                RequiredApprovalStage = requiredApprovalStage,
                GuardServiceName = guardServiceName,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        // ==================== MAINTENANCE (Classification=0) ====================
        // Reuses existing MaintenanceStatus enum values.
        private static void SeedMaintenance(
            List<WorkOrderStatusProfile> profiles,
            List<WorkOrderStatusLabel> labels,
            List<WorkOrderStatusTransition> transitions,
            DateTime now)
        {
            const WorkOrderClassification CLS = WorkOrderClassification.Maintenance;

            profiles.Add(new WorkOrderStatusProfile
            {
                Classification = CLS,
                Name = "Maintenance work order",
                StartStatusCode = 0,
                CanReopenFromTerminal = true,
                Description = "Standard maintenance lifecycle (PM, Corrective, Predictive, Emergency, Inspection, Calibration).",
                CreatedAt = now, UpdatedAt = now,
            });

            AddLabel(labels, CLS, 0, "Scheduled",  "Scheduled",   "blue",  false, false, 10, now);
            AddLabel(labels, CLS, 1, "InProgress", "In Progress", "amber", false, false, 20, now);
            AddLabel(labels, CLS, 2, "Completed",  "Completed",   "green", true,  false, 90, now);
            AddLabel(labels, CLS, 3, "Cancelled",  "Cancelled",   "gray",  true,  false, 95, now);
            AddLabel(labels, CLS, 4, "Overdue",    "Overdue",     "red",   false, false, 15, now);
            AddLabel(labels, CLS, 5, "OnHold",     "On Hold",     "amber", false, true,  30, now);

            AddTransition(transitions, CLS, 0, 1, now, actionLabel: "Start Work", order: 10);
            AddTransition(transitions, CLS, 0, 3, now, actionLabel: "Cancel",     order: 90);
            AddTransition(transitions, CLS, 1, 2, now, actionLabel: "Complete",   order: 10);
            AddTransition(transitions, CLS, 1, 5, now, actionLabel: "Place On Hold", order: 20);
            AddTransition(transitions, CLS, 1, 3, now, actionLabel: "Cancel",     order: 90);
            AddTransition(transitions, CLS, 5, 1, now, actionLabel: "Resume",     order: 10);
            AddTransition(transitions, CLS, 5, 3, now, actionLabel: "Cancel",     order: 90);
            AddTransition(transitions, CLS, 4, 1, now, actionLabel: "Start Work", order: 10);
            AddTransition(transitions, CLS, 4, 3, now, actionLabel: "Cancel",     order: 90);
            AddTransition(transitions, CLS, 1, 0, now, actionLabel: "Revert to Scheduled", back: true, order: 200);
            AddTransition(transitions, CLS, 2, 1, now, actionLabel: "Reopen",     back: true, order: 200);
        }

        // ==================== QUALITY (Classification=2) ====================
        // NCR / CAPA lifecycle per ISO 9001 Cl. 10.2 + Ford G8D.
        private static void SeedQuality(
            List<WorkOrderStatusProfile> profiles,
            List<WorkOrderStatusLabel> labels,
            List<WorkOrderStatusTransition> transitions,
            DateTime now)
        {
            const WorkOrderClassification CLS = WorkOrderClassification.Quality;

            profiles.Add(new WorkOrderStatusProfile
            {
                Classification = CLS,
                Name = "Quality NCR / CAPA",
                StartStatusCode = 0,
                CanReopenFromTerminal = true,
                Description = "NCR/CAPA lifecycle: Reported → Contained → Investigating → DispositionApproved → ActionImplemented → EffectivenessVerified → Closed (ISO 9001 Cl. 10.2; Ford G8D).",
                CreatedAt = now, UpdatedAt = now,
            });

            AddLabel(labels, CLS, 0, "Reported",              "Reported",                "red",   false, false, 10, now);
            AddLabel(labels, CLS, 1, "Contained",             "Contained",               "amber", false, false, 20, now);
            AddLabel(labels, CLS, 2, "Investigating",         "Investigating",           "amber", false, false, 30, now);
            AddLabel(labels, CLS, 3, "DispositionApproved",   "Disposition Approved",    "amber", false, false, 40, now);
            AddLabel(labels, CLS, 4, "ActionImplemented",     "Corrective Action Done",  "amber", false, false, 50, now);
            AddLabel(labels, CLS, 5, "EffectivenessVerified", "Effectiveness Verified",  "green", false, false, 60, now);
            AddLabel(labels, CLS, 6, "Closed",                "Closed",                  "green", true,  false, 90, now);
            AddLabel(labels, CLS, 7, "Cancelled",             "Cancelled",               "gray",  true,  false, 95, now);

            AddTransition(transitions, CLS, 0, 1, now, actionLabel: "Contain Nonconforming Material", order: 10);
            AddTransition(transitions, CLS, 0, 7, now, actionLabel: "Cancel NCR", order: 90);
            AddTransition(transitions, CLS, 1, 2, now, actionLabel: "Begin Investigation", order: 10);
            AddTransition(transitions, CLS, 2, 3, now, actionLabel: "Approve Disposition",
                requiredApprovalStage: "QA-Disposition", order: 10);
            AddTransition(transitions, CLS, 3, 4, now, actionLabel: "Mark Action Implemented", order: 10);
            AddTransition(transitions, CLS, 4, 5, now, actionLabel: "Verify Effectiveness",
                guardServiceName: "QaEffectivenessGuard", order: 10);
            AddTransition(transitions, CLS, 5, 6, now, actionLabel: "Close NCR", order: 10);
            AddTransition(transitions, CLS, 6, 4, now, actionLabel: "Reopen — Verification Failed", back: true, order: 200);
        }

        // ==================== ENGINEERING (Classification=3) ====================
        // ECO / MOC lifecycle per OSHA 29 CFR 1910.119(l) + PSSR (i).
        private static void SeedEngineering(
            List<WorkOrderStatusProfile> profiles,
            List<WorkOrderStatusLabel> labels,
            List<WorkOrderStatusTransition> transitions,
            DateTime now)
        {
            const WorkOrderClassification CLS = WorkOrderClassification.Engineering;

            profiles.Add(new WorkOrderStatusProfile
            {
                Classification = CLS,
                Name = "Engineering Change (ECO/MOC)",
                StartStatusCode = 0,
                CanReopenFromTerminal = false,
                Description = "ECO/MOC lifecycle with PSSR gate. Draft → Submitted → TechnicalReview → CcbApproved → InImplementation → PssrRequired → Effective → Closed.",
                CreatedAt = now, UpdatedAt = now,
            });

            AddLabel(labels, CLS, 0, "Draft",            "Draft",                       "gray",  false, false, 10, now);
            AddLabel(labels, CLS, 1, "Submitted",        "Submitted",                   "blue",  false, false, 20, now);
            AddLabel(labels, CLS, 2, "TechnicalReview", "Technical Review",            "blue",  false, false, 30, now);
            AddLabel(labels, CLS, 3, "CcbApproved",     "CCB Approved",                "amber", false, false, 40, now);
            AddLabel(labels, CLS, 4, "InImplementation","Implementation In Progress",  "amber", false, false, 50, now);
            AddLabel(labels, CLS, 5, "PssrRequired",    "PSSR Required",               "red",   false, true,  60, now);
            AddLabel(labels, CLS, 6, "Effective",       "Effective",                   "green", false, false, 70, now);
            AddLabel(labels, CLS, 7, "Closed",          "Closed",                      "green", true,  false, 90, now);
            AddLabel(labels, CLS, 8, "Cancelled",       "Cancelled",                   "gray",  true,  false, 95, now);

            AddTransition(transitions, CLS, 0, 1, now, actionLabel: "Submit for Review", order: 10);
            AddTransition(transitions, CLS, 0, 8, now, actionLabel: "Cancel", order: 90);
            AddTransition(transitions, CLS, 1, 2, now, actionLabel: "Begin Technical Review", order: 10);
            AddTransition(transitions, CLS, 2, 3, now, actionLabel: "Approve at CCB",
                requiredApprovalStage: "CCB", order: 10);
            AddTransition(transitions, CLS, 3, 4, now, actionLabel: "Begin Implementation", order: 10);
            AddTransition(transitions, CLS, 4, 5, now, actionLabel: "Open PSSR Gate", order: 10);
            AddTransition(transitions, CLS, 5, 6, now, actionLabel: "Mark Effective",
                requiredApprovalStage: "PSSR",
                guardServiceName: "PssrCompletionGuard", order: 10);
            AddTransition(transitions, CLS, 6, 7, now, actionLabel: "Close Out", order: 10);
        }

        // ==================== HSE (Classification=4) ====================
        // Incident / JSA lifecycle per ISO 45001 + OSHA 29 CFR 1904.
        private static void SeedHse(
            List<WorkOrderStatusProfile> profiles,
            List<WorkOrderStatusLabel> labels,
            List<WorkOrderStatusTransition> transitions,
            DateTime now)
        {
            const WorkOrderClassification CLS = WorkOrderClassification.HSE;

            profiles.Add(new WorkOrderStatusProfile
            {
                Classification = CLS,
                Name = "HSE Incident / JSA",
                StartStatusCode = 0,
                CanReopenFromTerminal = false,
                Description = "Incident lifecycle per ISO 45001 + OSHA 1904. Recordability determined before close; OSHA 300 entry is immutable.",
                CreatedAt = now, UpdatedAt = now,
            });

            AddLabel(labels, CLS, 0, "Reported",                "Reported",                  "red",   false, false, 10, now);
            AddLabel(labels, CLS, 1, "Acknowledged",            "Acknowledged",              "amber", false, false, 20, now);
            AddLabel(labels, CLS, 2, "Investigating",           "Investigating",             "amber", false, false, 30, now);
            AddLabel(labels, CLS, 3, "RootCauseIdentified",     "Root Cause Identified",     "amber", false, false, 40, now);
            AddLabel(labels, CLS, 4, "ActionsAssigned",         "Corrective Actions Assigned","amber",false, false, 50, now);
            AddLabel(labels, CLS, 5, "RecordabilityDetermined", "Recordability Determined",  "blue",  false, false, 60, now);
            AddLabel(labels, CLS, 6, "Logged",                  "Logged to OSHA 300",        "blue",  false, false, 70, now);
            AddLabel(labels, CLS, 7, "Closed",                  "Closed",                    "green", true,  false, 90, now);
            AddLabel(labels, CLS, 8, "Cancelled",               "Cancelled",                 "gray",  true,  false, 95, now);

            AddTransition(transitions, CLS, 0, 1, now, actionLabel: "Acknowledge", order: 10);
            AddTransition(transitions, CLS, 1, 2, now, actionLabel: "Open Investigation", order: 10);
            AddTransition(transitions, CLS, 2, 3, now, actionLabel: "Lock Root Cause", order: 10);
            AddTransition(transitions, CLS, 3, 4, now, actionLabel: "Assign Corrective Actions", order: 10);
            AddTransition(transitions, CLS, 4, 5, now, actionLabel: "Determine Recordability",
                guardServiceName: "OshaRecordabilityGuard", order: 10);
            AddTransition(transitions, CLS, 5, 6, now, actionLabel: "Log to OSHA 300",
                guardServiceName: "Osha300LogGuard", order: 10);
            AddTransition(transitions, CLS, 6, 7, now, actionLabel: "Close",
                requiredApprovalStage: "EHS-Director", order: 10);
            // Skip OSHA log if not recordable.
            AddTransition(transitions, CLS, 5, 7, now, actionLabel: "Close — Not Recordable", order: 20);
        }

        // ==================== CIP (Classification=5) ====================
        // Capital project lifecycle. SubstantialComplete triggers
        // accounting reclassification + depreciation start (ASC 360-10
        // + ASC 835-20).
        private static void SeedCip(
            List<WorkOrderStatusProfile> profiles,
            List<WorkOrderStatusLabel> labels,
            List<WorkOrderStatusTransition> transitions,
            DateTime now)
        {
            const WorkOrderClassification CLS = WorkOrderClassification.CIP;

            profiles.Add(new WorkOrderStatusProfile
            {
                Classification = CLS,
                Name = "Capital project (CIP)",
                StartStatusCode = 0,
                CanReopenFromTerminal = false,
                Description = "CIP lifecycle. SubstantialComplete fires the fixed-asset reclassification + depreciation start (ASC 360-10 / 835-20). Closed is irreversible.",
                CreatedAt = now, UpdatedAt = now,
            });

            AddLabel(labels, CLS, 0,  "Initiation",          "Initiation",               "gray",  false, false, 10,  now);
            AddLabel(labels, CLS, 1,  "Feasibility",         "Feasibility / FEED",       "blue",  false, false, 20,  now);
            AddLabel(labels, CLS, 2,  "AfeApproved",         "AFE Approved",             "amber", false, false, 30,  now);
            AddLabel(labels, CLS, 3,  "DetailedDesign",      "Detailed Design",          "amber", false, false, 40,  now);
            AddLabel(labels, CLS, 4,  "Procurement",         "Procurement",              "amber", false, false, 50,  now);
            AddLabel(labels, CLS, 5,  "Construction",        "Construction",             "amber", false, false, 60,  now);
            AddLabel(labels, CLS, 6,  "MechanicalCompletion","Mechanical Completion",    "amber", false, false, 70,  now);
            AddLabel(labels, CLS, 7,  "Commissioning",       "Commissioning",            "amber", false, false, 80,  now);
            AddLabel(labels, CLS, 8,  "SubstantialComplete", "Substantial Completion",   "green", false, false, 90,  now);
            AddLabel(labels, CLS, 9,  "Closed",              "Closed",                   "green", true,  false, 100, now);
            AddLabel(labels, CLS, 10, "Cancelled",           "Cancelled",                "gray",  true,  false, 110, now);
            AddLabel(labels, CLS, 11, "OnHold",              "On Hold",                  "amber", false, true,  35,  now);

            AddTransition(transitions, CLS, 0, 1, now, actionLabel: "Open Feasibility / FEED", order: 10);
            AddTransition(transitions, CLS, 0, 10, now, actionLabel: "Cancel", order: 90);
            AddTransition(transitions, CLS, 1, 2, now, actionLabel: "Approve AFE",
                requiredApprovalStage: "AFE-Tier1", order: 10);
            AddTransition(transitions, CLS, 2, 3, now, actionLabel: "Begin Detailed Design", order: 10);
            AddTransition(transitions, CLS, 3, 4, now, actionLabel: "Begin Procurement", order: 10);
            AddTransition(transitions, CLS, 4, 5, now, actionLabel: "Begin Construction", order: 10);
            AddTransition(transitions, CLS, 5, 6, now, actionLabel: "Mark Mechanical Completion", order: 10);
            AddTransition(transitions, CLS, 6, 7, now, actionLabel: "Begin Commissioning", order: 10);
            AddTransition(transitions, CLS, 7, 8, now, actionLabel: "Substantial Completion",
                guardServiceName: "CipCapitalizationGuard", order: 10);
            AddTransition(transitions, CLS, 8, 9, now, actionLabel: "Close Out", order: 10);

            // On-hold paths from active stages.
            AddTransition(transitions, CLS, 5, 11, now, actionLabel: "Place On Hold", order: 20);
            AddTransition(transitions, CLS, 7, 11, now, actionLabel: "Place On Hold", order: 20);
            AddTransition(transitions, CLS, 11, 5, now, actionLabel: "Resume", order: 10);
        }
    }
}
