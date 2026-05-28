// Sprint 15.2 PR-9 (2026-05-28) — ISubcontractValidationService.
//
// THE 15 §24 NON-NEGOTIABLE VALIDATIONS as service guards.
//
// Per Dean's spec §24, these are the rules that MUST hold for every subcontract
// op before any write — release, ship, receive, complete, close, issue,
// consolidate. The Cockpit panel (this PR) calls these guards before exposing
// each action. The 8-step orchestrator (PR-7) should also adopt these guards
// in a follow-up tightening pass.
//
// Each validator returns Result<ValidationOutcome>. Outcome is Pass/Warn/Block
// — Block fails the action, Warn surfaces in the UI but allows override.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §24
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 2 PR-9

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Production;

public enum SubcontractValidationOutcome
{
    /// <summary>Validation passes — action is allowed.</summary>
    Pass = 0,
    /// <summary>Validation warns — action allowed with supervisor override or audit note.</summary>
    Warn = 1,
    /// <summary>Validation blocks — action must not proceed.</summary>
    Block = 2,
}

public sealed record SubcontractValidationResult(
    string RuleNumber,
    string RuleName,
    SubcontractValidationOutcome Outcome,
    string Description,
    string? OverrideHint);

public interface ISubcontractValidationService
{
    /// <summary>Rule 1: Cannot release subcontract operation without supplier/service item.</summary>
    Task<Result<SubcontractValidationResult>> CanReleaseAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 2: Cannot ship WIP to vendor without completed quantity from prior operation.</summary>
    Task<Result<SubcontractValidationResult>> CanShipAsync(
        int subcontractOperationId, decimal proposedQuantity, CancellationToken ct = default);

    /// <summary>Rule 3: Cannot receive more WIP back than shipped without over-receipt approval.</summary>
    Task<Result<SubcontractValidationResult>> CanReceiveAsync(
        int subcontractOperationId, decimal proposedQuantityReceived, CancellationToken ct = default);

    /// <summary>Rule 4: Cannot complete subcontract operation until receipt is accepted or override approved.</summary>
    Task<Result<SubcontractValidationResult>> CanCompleteAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 5: Cannot move WIP to next operation if inspection hold exists.</summary>
    Task<Result<SubcontractValidationResult>> CanMoveToNextOpAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 6: Cannot close subcontract PO with open vendor WIP.</summary>
    Task<Result<SubcontractValidationResult>> CanCloseSubcontractPoAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 7: Cannot close Production Order with unresolved vendor WIP.</summary>
    Task<Result<SubcontractValidationResult>> CanCloseProductionOrderAsync(
        int productionOrderId, CancellationToken ct = default);

    /// <summary>Rule 8: Cannot issue/consume material that is still physically at vendor.</summary>
    Task<Result<SubcontractValidationResult>> CanIssueMaterialAsync(
        int subcontractOperationId, int itemId, decimal proposedQuantity, CancellationToken ct = default);

    /// <summary>Rule 9: Cannot buy to job without linking PO line to demand.</summary>
    Task<Result<SubcontractValidationResult>> CanBuyToJobAsync(
        int purchaseOrderLineId, CancellationToken ct = default);

    /// <summary>Rule 10: Cannot consolidate PO demand without preserving job/operation allocations.</summary>
    Task<Result<SubcontractValidationResult>> CanConsolidatePoDemandAsync(
        int purchaseOrderLineId, CancellationToken ct = default);

    /// <summary>Rule 11: Cannot use unapproved supplier for controlled material/service.</summary>
    Task<Result<SubcontractValidationResult>> CanUseSupplierAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 12: Cannot bypass cert requirement if customer/spec requires it.</summary>
    Task<Result<SubcontractValidationResult>> CanBypassCertRequirementAsync(
        int subcontractOperationId, CancellationToken ct = default);

    /// <summary>Rule 13: Cannot send wrong revision to subcontract supplier.</summary>
    Task<Result<SubcontractValidationResult>> CanShipRevisionAsync(
        int subcontractOperationId, string? drawingRevision, CancellationToken ct = default);

    /// <summary>Rule 14: Cannot receive wrong revision without quality/engineering approval.</summary>
    Task<Result<SubcontractValidationResult>> CanReceiveRevisionAsync(
        int subcontractOperationId, string? returnedRevision, CancellationToken ct = default);

    /// <summary>Rule 15: Cannot final close project with open PO commitments unless waived.</summary>
    Task<Result<SubcontractValidationResult>> CanFinalCloseProjectAsync(
        int productionOrderId, CancellationToken ct = default);

    /// <summary>Aggregate: run all 15 rules for the subcontract op + report which pass/warn/block.</summary>
    Task<Result<System.Collections.Generic.IReadOnlyList<SubcontractValidationResult>>> RunAllAsync(
        int subcontractOperationId, CancellationToken ct = default);
}
