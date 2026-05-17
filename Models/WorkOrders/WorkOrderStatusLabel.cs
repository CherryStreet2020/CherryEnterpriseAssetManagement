using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Status-code → label map per classification.
    //
    // One row per (Classification, StatusCode). The runtime engine reads
    // this table to render the status badge ("Scheduled", "Substantial
    // Completion", "PSSR Required") and decide whether a status is
    // terminal or holding the workflow.
    //
    // Same StatusCode can mean different things across classifications:
    //   Classification=0, StatusCode=0 → "Scheduled" (Maintenance)
    //   Classification=2, StatusCode=0 → "Reported" (Quality NCR)
    //   Classification=5, StatusCode=0 → "Initiation" (CIP)
    //
    // The UI badge color is stored here too, so admins can recolor
    // statuses without code changes.
    [Table("WorkOrderStatusLabel")]
    public class WorkOrderStatusLabel
    {
        public int Id { get; set; }

        public WorkOrderClassification Classification { get; set; }

        public short StatusCode { get; set; }

        // Machine-readable key. Stable identifier the status engine
        // and guards reference (e.g. "PssrRequired" — a guard checks
        // this exact string before allowing transition out of it).
        [Required, StringLength(40)]
        public string StatusKey { get; set; } = string.Empty;

        // Display label rendered in the badge + dropdowns. Translatable
        // in a future i18n PR; current scope is English-only.
        [Required, StringLength(80)]
        public string DisplayLabel { get; set; } = string.Empty;

        // Tailwind color name driving the badge: blue / amber / green /
        // red / gray. Picked at seed time per regulator convention
        // (red for "needs attention", green for "good", amber for
        // "in progress", gray for "neutral/terminal").
        [Required, StringLength(20)]
        public string DisplayColor { get; set; } = "gray";

        // Terminal: the workflow is complete. WO can't transition out
        // unless the profile has CanReopenFromTerminal=true.
        public bool IsTerminal { get; set; } = false;

        // Holding: workflow is paused waiting on an external event
        // (PSSR signoff, customer approval, parts ETA). Different from
        // Cancelled because the workflow can resume. Drives the "needs
        // attention" filter on the Plant Floor.
        public bool IsHolding { get; set; } = false;

        // Ordering within the classification's lifecycle (low = early).
        // Used to sort the dropdown in chronological order rather than
        // by StatusCode.
        public int DisplayOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
