// Theme B9 Wave 5 PR-14 (2026-05-30) — IProjectBillingService. CLOSES Wave 5.
//
// The bill-side of the project: billing schedule → invoices → revenue
// recognition, tied to the PR-8 billing milestones. Hosts the two §20 gates:
//   - a Milestone-type billing line cannot be invoiced until its milestone is
//     Achieved;
//   - a line flagged RequiresAcceptance cannot be invoiced until acceptance is
//     confirmed (cannot final-bill without required acceptance).
//
// ADR-025: read THROUGH this service. Tenant scope flows through the parent
// CustomerProject; every incoming FK on a write is scoped to the project.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectBillingService
{
    /// <summary>Billing schedule (+ milestone-achieved), invoices, recognition, and totals (read).</summary>
    Task<Result<ProjectBillingView>> GetBillingAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreateBillingScheduleAsync(CreateBillingScheduleRequest req, CancellationToken ct = default);

    /// <summary>Confirm acceptance on a billing line (set-once; unlocks final billing).</summary>
    Task<Result<ProjectBillingSchedule>> ConfirmAcceptanceAsync(int billingScheduleId, string? confirmedBy = null, CancellationToken ct = default);

    /// <summary>
    /// Record an invoice against a billing line. Blocked when: a Milestone-type
    /// line's milestone is not Achieved; or the line RequiresAcceptance and
    /// acceptance has not been confirmed. Advances the line to Invoiced.
    /// </summary>
    Task<Result<int>> RecordInvoiceAsync(RecordInvoiceRequest req, CancellationToken ct = default);

    Task<Result<int>> RecognizeRevenueAsync(RecognizeRevenueRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectBillingView(
    int ProjectId,
    string Currency,
    decimal ContractValue,
    decimal ScheduledTotal,
    decimal InvoicedTotal,
    decimal RecognizedTotal,
    decimal RemainingToBill,
    decimal? PercentBilledOfContract,
    IReadOnlyList<BillingScheduleRow> Schedule,
    IReadOnlyList<InvoiceRow> Invoices,
    IReadOnlyList<RevenueRow> Recognition);

public sealed record BillingScheduleRow(
    int Id, string Code, string Name, ProjectBillingType BillingType, ProjectBillingStatus Status,
    decimal ScheduledAmount, System.DateTime? ScheduledDate, int? ProjectMilestoneId,
    bool MilestoneAchieved, bool RequiresAcceptance, bool AcceptanceConfirmed,
    decimal InvoicedAgainst, bool ReadyToInvoice);

public sealed record InvoiceRow(
    int Id, string InvoiceNumber, System.DateTime InvoiceDate, decimal InvoicedAmount,
    ProjectInvoiceStatus Status, int? ProjectBillingScheduleId);

public sealed record RevenueRow(
    int Id, RevenueRecognitionMethod Method, decimal RecognizedAmount,
    System.DateTime RecognitionDate, int? ProjectBillingScheduleId);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateBillingScheduleRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    decimal ScheduledAmount,
    ProjectBillingType BillingType = ProjectBillingType.Milestone,
    string? Description = null,
    string? Currency = null,
    System.DateTime? ScheduledDate = null,
    decimal? PercentOfContract = null,
    int? ProjectMilestoneId = null,
    bool RequiresAcceptance = false,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record RecordInvoiceRequest(
    int ProjectBillingScheduleId,
    string InvoiceNumber,
    decimal InvoicedAmount,
    System.DateTime InvoiceDate,
    int? ExternalInvoiceId = null,
    string? Notes = null,
    string? Currency = null,
    string? CreatedBy = null);

public sealed record RecognizeRevenueRequest(
    int CustomerProjectId,
    decimal RecognizedAmount,
    System.DateTime RecognitionDate,
    RevenueRecognitionMethod Method = RevenueRecognitionMethod.PointInTime,
    int? ProjectBillingScheduleId = null,
    string? PeriodLabel = null,
    decimal? PercentComplete = null,
    string? Notes = null,
    string? Currency = null,
    string? CreatedBy = null);
