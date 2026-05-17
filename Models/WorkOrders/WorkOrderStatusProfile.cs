using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Per-classification state-machine HEADER.
    //
    // One row per WorkOrderClassification. Says "for this classification,
    // new WOs start at StatusCode=X, terminal states are {a, b, c}, and
    // the StatusLabels + StatusTransitions tables fill in the meaning."
    //
    // This is the Maximo WOSTATUS synonym-domain pattern: a single
    // smallint Status column on the header carries different meaning
    // depending on Classification. The runtime engine (IWorkOrderStatusEngine)
    // never decodes the int via a hardcoded enum — it always asks this
    // table what the int means.
    //
    // Existing MaintenanceStatus enum values stay valid for Classification=0
    // by convention: Scheduled=0, InProgress=1, Completed=2, Cancelled=3,
    // Overdue=4, OnHold=5. Existing Razor pages that cast Status to
    // MaintenanceStatus keep working unchanged. New code that handles
    // non-Maintenance classifications goes through the status engine.
    [Table("WorkOrderStatusProfile")]
    public class WorkOrderStatusProfile
    {
        // Composite PK isn't a thing in EF Core scalar form here — we use
        // Classification as the single-column PK because there is exactly
        // one profile per classification.
        [Key]
        public WorkOrderClassification Classification { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;

        // Status code a brand-new WO of this classification starts at.
        // For Maintenance this is 0 (Scheduled). For Quality this is 0
        // (Reported). For CIP this is 0 (Initiation).
        public short StartStatusCode { get; set; } = 0;

        // Whether a closed WO of this classification can be reopened.
        // CIP NO (capitalization is irreversible from an accounting POV);
        // Maintenance YES (you can re-open a corrective WO if the issue
        // recurs); Quality YES (rare but allowed for failed effectiveness
        // verification); HSE NO (OSHA 300 entries are immutable).
        public bool CanReopenFromTerminal { get; set; } = false;

        // Optional comment describing the high-level lifecycle. Renders
        // as a tooltip in the admin UI in Sprint 4.
        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
