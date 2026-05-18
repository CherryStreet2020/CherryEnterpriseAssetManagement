// =============================================================================
// CherryAI EAM — Receiving Control Center command + query DTOs (Sprint 11 PR #3)
// ADR-016 §D7. All mutation commands flow through IdempotencyMediator
// (ADR-014 D4); all queries return strongly-typed view snapshots.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Receiving;

// ----- Queries -------------------------------------------------------

public sealed class ExceptionLaneFilter
{
    public string? SiteCode { get; set; }
    public string[]? Kinds { get; set; }   // "quantity" | "doc" | "supplier" | "qc-hold" | "damage" | "partial" | "orphan"
    public bool? AiPrioritized { get; set; } = true;
    public int Take { get; set; } = 50;
    public int Skip { get; set; } = 0;
}

public sealed class KpiStripFilter
{
    public string? SiteCode { get; set; }
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-14);
    public DateTime To { get; set; } = DateTime.UtcNow;
}

public sealed class ActivityFeedFilter
{
    public string? SiteCode { get; set; }
    public int SinceSequence { get; set; }
    public int Take { get; set; } = 50;
}

// ----- Commands (mutation; all flow through IdempotencyMediator) -----

public sealed class ReceiveByPoCommand
{
    public string PoNumber { get; set; } = string.Empty;
    public string PoLineId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int? MaterialMasterId { get; set; }
    public int? ProfileId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? Uom { get; set; }
    public int? LocationId { get; set; }
    public string? Attributes { get; set; }     // jsonb payload validated against profile
    public string? Notes { get; set; }
}

public sealed class ReceiveByAsnCommand
{
    public string AsnId { get; set; } = string.Empty;
    public string? LineId { get; set; }
    public decimal? OverrideQuantity { get; set; }     // null = accept the ASN-declared quantity
    public string? Notes { get; set; }
}

public sealed class BlindReceiveCommand
{
    public int? VendorId { get; set; }
    public int? ItemId { get; set; }
    public int? ProfileId { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? Uom { get; set; }
    public int? LocationId { get; set; }
    public string? Attributes { get; set; }
    public string? Notes { get; set; }
}

public sealed class QuarantineCommand
{
    public int ReceiptId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class MatchOrphanReceiptCommand
{
    public int ReceiptId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string? PoLineId { get; set; }
}
