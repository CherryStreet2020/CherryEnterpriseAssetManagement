using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.11 — HseWorkOrderDetails satellite.
    //
    // Holds Health/Safety/Environment-only fields for Classification=HSE
    // work orders. Covers near-miss reports, OSHA recordable incidents,
    // hazard reports, JSAs (Job Safety Analyses), and BBS (Behavior-Based
    // Safety) observations.
    //
    // Risk-matrix scoring follows ANSI Z10: RiskScore = HazardSeverity ×
    // Likelihood, range 1-25 (5x5 matrix). HazardSeverity and Likelihood
    // are 1-based enums so the math comes out matching the standard
    // matrix exactly.
    //
    // Relationship: 1:0..1 with WorkOrder.
    //   - UNIQUE on WorkOrderId, ON DELETE CASCADE
    //
    // Field sourcing:
    //   - HseIssueType: routing input — incidents vs proactive JSAs
    //     follow different workflows.
    //   - OshaCaseNumber: required for OSHA 300 recordkeeping.
    //   - RecordabilityClass: per OSHA 29 CFR 1904 — which OSHA 300
    //     column the incident hits (or NotRecordable). Drives ITA
    //     submission requirement.
    //   - HazardSeverity x Likelihood: ANSI Z10 risk-matrix inputs.
    //     RiskScore = product, 1-25. Drives intervention priority.
    //   - EmployeesAffected: head count for the incident scope.
    //   - BodyPartAffected + InjuryType: OSHA injury taxonomy (Form 300
    //     column entries).
    //   - DaysAway + DaysRestricted: counts for OSHA DART rate.
    //   - LostTimeIncident: shorthand bool — DaysAway > 0.
    //   - OshaItaSubmissionRequired: triggers annual OSHA Injury Tracking
    //     Application submission (March 2 deadline).
    //   - RegulatoryNotifications: jsonb array of `{agency, formNumber,
    //     submittedAt}` documenting external reports filed (OSHA, EPA,
    //     state regulators).
    //   - JsaSteps: jsonb array of `{stepOrder, step, hazard, control,
    //     hierarchyOfControls}`. Hierarchy values: Elimination /
    //     Substitution / Engineering / Administrative / PPE (per OSHA
    //     3071).
    //   - WitnessStatementsUrl + PhotosUrl: external storage pointers
    //     to evidence (Box / SharePoint / S3). Not embedded so we don't
    //     bloat the table.
    //
    // Source standards:
    //   - ISO 45001:2018 (OH&S management systems)
    //   - OSHA 29 CFR 1904 (Recording and Reporting Occupational
    //     Injuries and Illnesses)
    //   - OSHA Publication 3071 (Job Hazard Analysis)
    //   - OSHA ITA (Injury Tracking Application — 29 CFR 1904.41)
    //   - ANSI Z10-2019 (Occupational Health and Safety Management
    //     Systems — risk-matrix methodology)
    [Table("HseWorkOrderDetails")]
    public class HseWorkOrderDetails
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }

        public HseIssueType HseIssueType { get; set; } = HseIssueType.HazardReport;

        [StringLength(32)]
        public string? OshaCaseNumber { get; set; }

        public OshaRecordabilityClass RecordabilityClass { get; set; } =
            OshaRecordabilityClass.NotRecordable;

        // ANSI Z10 risk-matrix inputs. 1-based so RiskScore = product is
        // in the 1-25 range that the standard 5x5 matrix uses.
        public HazardSeverity HazardSeverity { get; set; } = HazardSeverity.Minor;

        public HazardLikelihood Likelihood { get; set; } = HazardLikelihood.Unlikely;

        // Computed: (int)HazardSeverity * (int)Likelihood. 1-25.
        // Stored rather than derived so it can be indexed for "highest-
        // risk first" queue ordering.
        public int RiskScore { get; set; }

        public int? EmployeesAffected { get; set; }

        [StringLength(64)]
        public string? BodyPartAffected { get; set; }

        // OSHA Form 300 injury classification (Sprain/Burn/Cut/etc.).
        // Free-text 80 chars to accommodate the full OSHA taxonomy
        // without enforcing a closed enum that misses regional variants.
        [StringLength(80)]
        public string? InjuryType { get; set; }

        // OSHA DART-rate inputs.
        public int? DaysAway { get; set; }
        public int? DaysRestricted { get; set; }

        public bool LostTimeIncident { get; set; }

        public bool OshaItaSubmissionRequired { get; set; }

        // jsonb array of `{agency, formNumber, submittedAt}` entries.
        [Column(TypeName = "jsonb")]
        public string? RegulatoryNotifications { get; set; }

        // jsonb array of `{stepOrder, step, hazard, control,
        // hierarchyOfControls}` entries per OSHA 3071 JSA template.
        [Column(TypeName = "jsonb")]
        public string? JsaSteps { get; set; }

        [StringLength(500)]
        public string? WitnessStatementsUrl { get; set; }

        [StringLength(500)]
        public string? PhotosUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum HseIssueType
    {
        SafetyInspection = 0,
        HazardReport = 1,
        NearMiss = 2,
        Incident = 3,
        Jsa = 4,                  // Job Safety Analysis
        BehaviorBasedSafety = 5,  // BBS observation
        Audit = 6,
    }

    // Per OSHA 29 CFR 1904 — Form 300 recordkeeping categories.
    public enum OshaRecordabilityClass
    {
        NotRecordable = 0,
        OtherRecordable = 1,      // Column N (job transfer or restriction)
        RestrictedDuty = 2,
        DaysAway = 3,             // Column H
        Hospitalization = 4,
        Fatality = 5,
    }

    // ANSI Z10 5x5 risk matrix. 1-based to align with the matrix scoring.
    public enum HazardSeverity
    {
        Negligible = 1,
        Minor = 2,
        Moderate = 3,
        Serious = 4,
        Catastrophic = 5,
    }

    // ANSI Z10 5x5 risk matrix. 1-based to align with the matrix scoring.
    public enum HazardLikelihood
    {
        Rare = 1,
        Unlikely = 2,
        Possible = 3,
        Likely = 4,
        AlmostCertain = 5,
    }
}
