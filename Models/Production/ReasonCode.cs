using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5d / rolling PRA-3 — ReasonCode catalog
//
// Standardized reason codes for the operator's drop-downs when logging
// shop-floor events: scrap, rework, downtime, hold. Used by:
//   - Workbench scrap / rework qty entry (Sprint 13.5 PR #5d)
//   - Future DowntimeEvent + ScrapEvent + ReworkEvent tables (PR #5e)
//   - WorkOrder Hold reason (existing — string field, will migrate to FK later)
//
// CROSS-TENANT REFERENCE PATTERN (mirrors MaterialMaster from PR #5c.2):
//   - CompanyId NULL  = SYSTEM reference code (canonical codes every tenant sees)
//   - CompanyId set   = tenant-specific extension (a shop's custom code)
//
// Two partial UNIQUE indexes enforce category-scoped codes:
//   IX_ReasonCodes_System_Category_Code  WHERE CompanyId IS NULL
//   IX_ReasonCodes_Company_Category_Code WHERE CompanyId IS NOT NULL
//
// (NO COALESCE-in-index — Replit prod-validator gotcha lesson from PR #5c.1.1.)
//
// CATEGORY ENUM:
//   - Scrap     — material destroyed during a production op
//   - Rework    — material that needs to be re-processed
//   - Downtime  — machine/operator was unavailable (no production)
//   - Hold      — order paused for external reason (waiting on customer,
//                 material, NCR, engineering review, etc.)
//   - Other     — catch-all (sparingly used)
//
// SAMPLE SYSTEM CODES (seeded via seed/reference-data/reason-codes.json — PR #5c.4):
//   Scrap:    SC-MATL (material defect), SC-OP (operator error), SC-SETUP (setup error), SC-EQUIP (equipment failure)
//   Rework:   RW-DIM (dimensional out-of-spec), RW-FIN (surface finish), RW-COSM (cosmetic)
//   Downtime: DT-SETUP (planned setup), DT-PM (PM activity), DT-BRKDN (unplanned breakdown), DT-MATL (waiting on material)
//   Hold:     HD-CUST (waiting on customer), HD-MATL (waiting on material), HD-NCR (NCR open), HD-ENG (engineering review)
// =============================================================================
[Table("ReasonCodes")]
public class ReasonCode
{
    public int Id { get; set; }

    // NULL = system reference code (cross-tenant shared).
    // INT > 0 = tenant-specific extension.
    public int? CompanyId { get; set; }

    // Shop-defined short code. Required, UNIQUE per (CompanyId, Category).
    [Required]
    [StringLength(32)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    public ReasonCodeCategory Category { get; set; } = ReasonCodeCategory.Scrap;

    public int SortOrder { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [StringLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [StringLength(100)]
    public string? ModifiedBy { get; set; }
}

public enum ReasonCodeCategory
{
    Scrap = 0,
    Rework = 1,
    Downtime = 2,
    Hold = 3,
    Other = 99,
}
