// =============================================================================
// CherryAI EAM — Receiving Control Center result types (Sprint 11 PR #3)
// Returned wrapped in Result<T> (ADR-014 D2) from IReceivingControlCenterService.
// =============================================================================

using System;
using System.Collections.Generic;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Receiving;

// ----- Query results -------------------------------------------------

public sealed class ExceptionLanePage
{
    public List<ExceptionLaneItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExceptionLaneItem
{
    public int ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public string? VendorName { get; set; }
    public string Kind { get; set; } = "info";        // quantity | doc | supplier | qc-hold | damage | partial | orphan
    public string Severity { get; set; } = "info";    // critical | warning | info | success | neutral
    public string Headline { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public int AiPriority { get; set; }
    public bool HasAiSuggestion { get; set; }
    public TimeSpan? SlaRemaining { get; set; }
    public string SlaTone { get; set; } = "neutral"; // neutral | warning | danger
}

public sealed class KpiStripSnapshot
{
    public KpiTileSnapshot DockToStock { get; set; } = new();
    public KpiTileSnapshot Accuracy { get; set; } = new();
    public KpiTileSnapshot OpenExceptions { get; set; } = new();
    public KpiTileSnapshot DocCompleteness { get; set; } = new();
    public KpiTileSnapshot SupplierOnTime { get; set; } = new();
    public KpiTileSnapshot QuarantineCycle { get; set; } = new();
    public KpiTileSnapshot AsnPenetration { get; set; } = new();
    public KpiTileSnapshot VoiceAdoption { get; set; } = new();
    public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class KpiTileSnapshot
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "0";
    public string? Unit { get; set; }
    public double? Target { get; set; }
    public string DeltaDirection { get; set; } = "flat";    // up | down | flat
    public string? DeltaText { get; set; }
    public double[] SparkPoints { get; set; } = Array.Empty<double>();
    public string SparkTone { get; set; } = "muted";
}

public sealed class ActivityFeedDelta
{
    public List<ActivityFeedEntry> Entries { get; set; } = new();
    public int HighestSequence { get; set; }
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ActivityFeedEntry
{
    public int Sequence { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string ActorKind { get; set; } = "human";   // human | ai | system
    public string ActorName { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
    public string? TargetRef { get; set; }
    public string? Snippet { get; set; }
}

// ----- Command results -----------------------------------------------

public sealed class ReceiveResult
{
    public int ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public StockReceiptStatus Status { get; set; }
    public decimal QuantityReceived { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public bool RequiresQuarantine { get; set; }
    public string? QuarantineReason { get; set; }
}

public sealed class QuarantineResult
{
    public int ReceiptId { get; set; }
    public StockReceiptStatus FromStatus { get; set; }
    public StockReceiptStatus ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime QuarantinedAtUtc { get; set; }
}

public sealed class MatchResult
{
    public int ReceiptId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string? PoLineId { get; set; }
    public bool WasOrphan { get; set; }
}
