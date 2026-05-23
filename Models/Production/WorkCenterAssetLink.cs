using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5c — WorkCenterAssetLink
//
// N:N join between WorkCenter (the dispatch unit) and Asset (the specific
// machine). One WC can own several Assets (FMS cell of 4 mills); one Asset
// can belong to multiple WCs over time (Asset moved between cells gets two
// links with non-overlapping effective ranges).
//
// IsPrimary: when a WC owns N assets, ONE is marked primary (the "headline"
// machine that shows on the WC card by default). Others surface in the
// drilldown.
//
// Why a join table (vs Asset.WorkCenterId FK): an Asset can be a member of
// multiple WCs simultaneously (a multi-tasking machine that participates in
// both the "CNC Cell" WC and the "Inspect" WC) — and we want history. PR #5e
// adds DowntimeEvent + ScrapEvent which link to ProductionOperation +
// (transitively) WorkCenter via this join.
// =============================================================================
[Table("WorkCenterAssetLinks")]
public class WorkCenterAssetLink
{
    public int Id { get; set; }

    public int WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }

    public int AssetId { get; set; }  // FK → Asset (existing, carries CurrentOEE etc.)

    public bool IsPrimary { get; set; } = false;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }    // null = currently active

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
}
