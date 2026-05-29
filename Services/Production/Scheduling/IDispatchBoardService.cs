// Theme B11 Wave R4-12 (2026-05-29) — Dispatch board (the operator/supervisor surface).
//
// The last piece of Wave R4. R4-10 said HOW LOADED each resource is; R4-11 said WHEN
// each operation runs and WHERE. R4-12 turns that into the screen a shop-floor
// supervisor actually works from: for every Work Center, the queue of operations in
// the order they should be run — sequenced by THAT work center's own dispatch rule
// (FIFO / earliest-due / shortest / critical-ratio / minimum-slack / highest-priority).
//
// It reads the R4-11 PlannedStart/End plus each operation's order context (due date,
// priority, remaining work) and ranks. The supervisor can "dispatch next" — release
// the top-ranked queued op into setup — which is the one write the board performs.
//
// Tenant scope: the operator's company (ops carry CompanyIdSnapshot).

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production.Scheduling
{
    /// <summary>One queued operation on a work center, with the context the rule ranked on.</summary>
    public sealed record DispatchEntry(
        int Rank,
        int ProductionOperationId,
        string OrderNumber,
        int SequenceNumber,
        string Description,
        ProductionOperationStatus Status,
        DateTime? DueUtc,
        int Priority,
        decimal RemainingWorkMins,
        DateTime? PlannedStartUtc,
        DateTime? PlannedEndUtc);

    /// <summary>A work center's dispatch queue, ordered by its own dispatch rule.</summary>
    public sealed record DispatchColumn(
        int WorkCenterId,
        string WorkCenterCode,
        string WorkCenterName,
        WorkCenterDispatchRule DispatchRule,
        IReadOnlyList<DispatchEntry> Entries);

    /// <summary>The whole board for a company: one column per work center with queued work.</summary>
    public sealed record DispatchBoard(
        int CompanyId,
        DateTime GeneratedAtUtc,
        IReadOnlyList<DispatchColumn> Columns);

    public interface IDispatchBoardService
    {
        /// <summary>
        /// Build the dispatch board for a company: every Work Center that has queued or
        /// in-progress operations, each column ordered by that WC's <see cref="WorkCenterDispatchRule"/>.
        /// </summary>
        Task<Result<DispatchBoard>> GetBoardAsync(int companyId, CancellationToken ct = default);

        /// <summary>
        /// Supervisor "dispatch next": move the top-ranked QUEUED (Scheduled/Released) operation
        /// on <paramref name="workCenterId"/> into InSetup. Returns the dispatched entry, or a
        /// failure if the work center has nothing queued.
        /// </summary>
        Task<Result<DispatchEntry>> DispatchNextAsync(int workCenterId, CancellationToken ct = default);
    }
}
