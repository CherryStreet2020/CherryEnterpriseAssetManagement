using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Monthly depreciation journal entry posted, V1. Emitted by the
/// /Journals/Generate and /Journals/Index page handlers after
/// <c>JournalGenerator.GenerateMonthlyAsync</c> commits the JE
/// (Dr Depreciation Expense / Cr Accumulated Depreciation).
///
/// The historical-backfill path (<c>HistoricJournalBackfillService</c>)
/// is intentionally exempt — it's bootstrap/migration tooling that can
/// run for many periods × books at once, and per-row emission would
/// flood subscribers during a deployment-time backfill.
/// </summary>
[DomainEvent("depreciation.posted", version: 1)]
public sealed record DepreciationPostedV1(
    int JournalEntryId,
    int BookId,
    string BookCode,
    int? CompanyId,
    int Period,
    DateTime PostingDate,
    decimal TotalDepreciation,
    string Batch,
    int LineCount,
    string CreatedBy
) : IDomainEvent
{
    public string EventType => "depreciation.posted";
    public int Version => 1;
    public string EntityType => "JournalEntry";
    public string EntityId => JournalEntryId.ToString();
}
