using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Centralized state-machine engine for
    // unified WorkOrders.
    //
    // Every WO status transition in non-Maintenance code paths goes
    // through this engine. Existing Maintenance code paths that cast
    // Status to MaintenanceStatus enum continue to work unchanged
    // (Maintenance classification's status profile matches the existing
    // enum values).
    //
    // The engine is the single place that:
    //   1. Looks up the legal next states for a WO's current state
    //   2. Checks required-approval gates (CCB, PSSR, AFE-Tier1, QA-Release)
    //   3. Resolves and runs the optional GuardServiceName guard
    //   4. Writes the new status + audit trail
    public interface IWorkOrderStatusEngine
    {
        // Returns the legal transitions out of the WO's current status.
        // Render these as buttons / menu options in the UI.
        Task<IReadOnlyList<TransitionOption>> GetAvailableTransitionsAsync(
            WorkOrder workOrder,
            CancellationToken ct = default);

        // Attempts the transition. Returns the result so the UI can
        // render the right toast / blocker message.
        //
        // Side effects (writes to the WorkOrder + audit log + any guard
        // side effects) happen inside this call. The caller is
        // responsible for the surrounding DbContext.SaveChangesAsync().
        Task<TransitionResult> TryTransitionAsync(
            WorkOrder workOrder,
            short toStatusCode,
            int userId,
            string? comment,
            CancellationToken ct = default);

        // Returns the badge label + color for the given (classification,
        // statusCode). Falls back to "[Unknown]" if no label is
        // configured. Cached.
        Task<WorkOrderStatusLabel?> GetLabelAsync(
            WorkOrderClassification classification,
            short statusCode,
            CancellationToken ct = default);

        // Returns the full list of labels for a classification (used by
        // dropdowns + filter chips).
        Task<IReadOnlyList<WorkOrderStatusLabel>> GetAllLabelsAsync(
            WorkOrderClassification classification,
            CancellationToken ct = default);

        // Invalidates the cache. Called by the admin-edit endpoint when
        // a customer changes their status config.
        void Invalidate();
    }

    // What the UI renders for a single transition button.
    public sealed record TransitionOption(
        short ToStatusCode,
        string ToStatusKey,
        string ActionLabel,
        string DisplayColor,
        bool IsBackTransition,
        bool RequiresApproval,
        string? RequiredApprovalStage,
        bool IsGuarded,
        string? GuardServiceName,
        int DisplayOrder);

    // What the engine returns from TryTransitionAsync.
    public sealed record TransitionResult(
        TransitionOutcome Outcome,
        short? NewStatusCode,
        string? NewStatusKey,
        string Message,
        // Set when Outcome=Blocked or AllowedWithWarning so the UI can
        // surface what the user needs to do (e.g. "Capture PSSR signoffs
        // first").
        string? BlockedReason = null);

    public enum TransitionOutcome
    {
        // Transition succeeded; WO status was updated.
        Success = 0,
        // No matching transition row in WorkOrderStatusTransition for
        // (Classification, From, To). The UI should never offer an
        // option that returns this; it's a defense-in-depth check.
        NotAllowed = 1,
        // Required approval is missing — UI should surface the approval
        // chain so the user can request it.
        ApprovalMissing = 2,
        // Guard service blocked the transition. Reason is in
        // BlockedReason.
        GuardBlocked = 3,
        // Allowed but with a warning from the guard. WO status updated;
        // UI should display the warning.
        AllowedWithWarning = 4,
    }

    // Contract for the optional guard hooks. Each guard is registered
    // in DI by name (matching WorkOrderStatusTransition.GuardServiceName).
    public interface IWorkOrderTransitionGuard
    {
        Task<GuardResult> RunAsync(
            WorkOrder workOrder,
            short fromStatusCode,
            short toStatusCode,
            int userId,
            string? comment,
            CancellationToken ct = default);
    }

    public sealed record GuardResult(
        GuardDecision Decision,
        string? Reason = null);

    public enum GuardDecision
    {
        Allow = 0,
        Block = 1,
        AllowWithWarning = 2,
    }
}
