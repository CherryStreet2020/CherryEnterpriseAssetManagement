// =============================================================================
// B8 PR-PRO-11 (2026-05-28) — Production Transaction Validation Pipeline
//
// 14 validators as guards on PRO-3/4/5 transaction services. Each validator
// implements IProductionTransactionValidator, is registered via DI, and
// discovered by the pipeline via IEnumerable<IProductionTransactionValidator>.
//
// Architecture: pipeline runs ALL applicable validators per action (not
// short-circuit). Blockers prevent the transaction. Warnings are logged
// but allow proceed. Overridable blockers can be bypassed with a
// supervisor override field.
//
// Design ref: IWorkOrderTransitionGuard keyed pattern adapted to
// chain-of-responsibility for multi-validator-per-action.
// =============================================================================

namespace Abs.FixedAssets.Services.Production.Validators;

/// <summary>Severity of a validation finding.</summary>
public enum ValidationSeverity
{
    /// <summary>Transaction is blocked. Cannot proceed without override.</summary>
    Block = 0,
    /// <summary>Warning only. Transaction proceeds but finding is logged.</summary>
    Warning = 1,
}

/// <summary>A single validation finding from a validator.</summary>
public sealed record TransactionValidationError(
    string ValidatorName,
    ValidationSeverity Severity,
    string Message,
    string? OverrideField = null);

/// <summary>Aggregated result from one validator.</summary>
public sealed record TransactionValidationResult(
    bool IsValid,
    IReadOnlyList<TransactionValidationError> Errors)
{
    public static readonly TransactionValidationResult Valid = new(true, Array.Empty<TransactionValidationError>());

    public static TransactionValidationResult Block(string validatorName, string message, string? overrideField = null)
        => new(false, new[] { new TransactionValidationError(validatorName, ValidationSeverity.Block, message, overrideField) });

    public static TransactionValidationResult Warn(string validatorName, string message)
        => new(true, new[] { new TransactionValidationError(validatorName, ValidationSeverity.Warning, message) });
}

/// <summary>Context passed to each validator — describes the transaction being attempted.</summary>
public sealed class TransactionValidationContext
{
    /// <summary>The action type (e.g., "Issue", "IssueAll", "Start", "Complete", "MoveToNext").</summary>
    public required string ActionType { get; init; }

    /// <summary>Production Order ID.</summary>
    public int ProductionOrderId { get; init; }

    /// <summary>BOM line ID (material transactions).</summary>
    public int? BomLineId { get; init; }

    /// <summary>Operation ID (operation transactions).</summary>
    public int? OperationId { get; init; }

    /// <summary>Item/part being transacted.</summary>
    public int? ItemId { get; init; }

    /// <summary>Quantity being transacted.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Lot number (if applicable).</summary>
    public string? LotNumber { get; init; }

    /// <summary>Serial number (if applicable).</summary>
    public string? SerialNumber { get; init; }

    /// <summary>User performing the transaction.</summary>
    public required string PerformedBy { get; init; }

    /// <summary>Company/tenant ID.</summary>
    public int CompanyId { get; init; }

    /// <summary>Reason code (for over-issue, scrap, etc.).</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Supervisor override flag (when a blocker is overridable).</summary>
    public bool SupervisorOverride { get; init; }

    /// <summary>Target job ID (for transfers).</summary>
    public int? TargetProductionOrderId { get; init; }

    /// <summary>Resource/machine ID.</summary>
    public int? ResourceId { get; init; }

    /// <summary>Employee user ID (for labor cert check).</summary>
    public int? EmployeeUserId { get; init; }
}

/// <summary>
/// Contract for production transaction validators. Each validator declares
/// which action types it applies to and performs async validation.
/// </summary>
public interface IProductionTransactionValidator
{
    /// <summary>Human-readable name for error reporting.</summary>
    string Name { get; }

    /// <summary>Action types this validator guards (e.g., "Issue", "Start", "Complete").</summary>
    IReadOnlySet<string> ApplicableActionTypes { get; }

    /// <summary>Run validation. Return Valid or a result with errors.</summary>
    Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext context,
        CancellationToken ct = default);
}

/// <summary>Well-known action type constants for ApplicableActionTypes.</summary>
public static class TransactionActions
{
    // Material
    public const string Issue = "Issue";
    public const string IssueAll = "IssueAll";
    public const string IssueKit = "IssueKit";
    public const string PartialIssue = "PartialIssue";
    public const string OverIssue = "OverIssue";
    public const string Return = "Return";
    public const string ReverseIssue = "ReverseIssue";
    public const string TransferToJob = "TransferToJob";
    public const string TransferFromJob = "TransferFromJob";
    public const string Substitute = "Substitute";
    public const string Split = "Split";
    public const string ScrapComponent = "ScrapComponent";

    // Operation
    public const string Start = "Start";
    public const string StartSetup = "StartSetup";
    public const string StartRun = "StartRun";
    public const string Pause = "Pause";
    public const string Resume = "Resume";
    public const string Complete = "Complete";
    public const string PartialComplete = "PartialComplete";
    public const string FinalComplete = "FinalComplete";
    public const string Skip = "Skip";
    public const string AddOperation = "AddOperation";
    public const string InsertRework = "InsertRework";
    public const string ChangeResource = "ChangeResource";
    public const string AddEmployee = "AddEmployee";
    public const string LogTime = "LogTime";
    public const string Stop = "Stop";
    public const string ReverseCompletion = "ReverseCompletion";
    public const string EditTime = "EditTime";

    // WIP Move
    public const string MoveToNext = "MoveToNext";
    public const string SendBack = "SendBack";
    public const string MoveToSpecific = "MoveToSpecific";
    public const string HoldAtOp = "HoldAtOp";
    public const string ReleaseHold = "ReleaseHold";
    public const string ReverseMove = "ReverseMove";

    // Completion
    public const string RecordCompletion = "RecordCompletion";
    public const string RecordScrap = "RecordScrap";
    public const string RecordRework = "RecordRework";
    public const string ApproveScrap = "ApproveScrap";

    // Material issue actions (convenience set)
    public static readonly HashSet<string> MaterialIssueActions = new()
    {
        Issue, IssueAll, IssueKit, PartialIssue, OverIssue
    };

    public static readonly HashSet<string> OperationStartActions = new()
    {
        Start, StartSetup, StartRun, AddEmployee
    };

    public static readonly HashSet<string> OperationCompleteActions = new()
    {
        Complete, PartialComplete, FinalComplete
    };

    public static readonly HashSet<string> AllMoveActions = new()
    {
        MoveToNext, SendBack, MoveToSpecific, HoldAtOp, ReleaseHold, ReverseMove
    };
}
