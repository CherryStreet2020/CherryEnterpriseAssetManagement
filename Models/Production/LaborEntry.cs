using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5d — LaborEntry (operator clock-in/out event)
//
// EXECUTION-TIME LABOR EVENT that the Operator Workbench writes when an
// operator clocks onto a ProductionOperation. Distinct from LaborConfig.cs:
//   - LaborType / LaborRate / Craft / Skill in LaborConfig.cs = lookup catalog
//     (rate tables, skill rosters) used at SETUP time
//   - LaborEntry (this file) = the actual clock-in/clock-out event log
//     created at SHOP-FLOOR time by an operator hitting "Clock In" on the
//     Workbench page. References LaborType for rate-category attribution.
//
// REGULATORY DRIVERS:
//   - AS9100 8.5.2 — operator traceability per operation
//   - 21 CFR 211 — operator identity on every production step (pharma)
//   - Sarbanes-Oxley ICFR — labor cost attribution to specific ops
//
// ONE-OPEN-CLOCK-IN RULE: Per BIC checklist + shop-floor reality, an
// operator can have at most ONE LaborEntry with ClockOutAt = NULL at any
// time (you can't be clocked into two operations simultaneously). Enforced
// via partial UNIQUE on (CompanyId, OperatorUserId) WHERE ClockOutAt IS NULL.
//
// MULTI-OPERATOR OPS: When two operators run one ProductionOperation
// together (a 2-person setup, a team build), each operator creates their
// OWN LaborEntry — both rows reference the same ProductionOperationId.
// Aggregated labor cost is SUM(DurationMins * rate) across all entries.
//
// TENANT SCOPE: CompanyId + LocationId NOT NULL per BIC checklist. Indexed
// by (CompanyId, LocationId, ProductionOperationId, ClockInAt) for the
// "all labor on this op" lookup and (CompanyId, OperatorUserId, ClockInAt
// DESC) for the "show me my last 10 entries" view.
// =============================================================================
[Table("LaborEntries")]
public class LaborEntry
{
    public int Id { get; set; }

    // BIC tenant trio — required.
    public int CompanyId { get; set; }
    public int LocationId { get; set; }

    // What operation is being worked.
    public int ProductionOperationId { get; set; }

    // Who is clocking in (FK to Users).
    public int OperatorUserId { get; set; }

    // Optional rate-category attribution (Regular / OT / DoubleTime / Holiday
    // / OnCall / Training / Travel / Administrative — from LaborCategory enum).
    // Defaults to Regular when null. Indexed if the costing rollup query
    // needs to filter by category.
    public int? LaborTypeId { get; set; }

    // Time bracket.
    [Required]
    public DateTime ClockInAt { get; set; }

    // Null until operator clocks out. Partial UNIQUE on
    // (CompanyId, OperatorUserId) WHERE ClockOutAt IS NULL enforces
    // one-open-clock-in.
    public DateTime? ClockOutAt { get; set; }

    // Computed at clock-out time: (ClockOutAt - ClockInAt) in minutes,
    // stored for fast aggregate queries. Decimal because partial-minute
    // tracking matters for short setups.
    [Column(TypeName = "decimal(10,2)")]
    public decimal? DurationMins { get; set; }

    // Operator notes — what they did during the clock-in window.
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
}
