// =============================================================================
// TransactionValidationPipeline — runs all applicable validators for a
// transaction action and aggregates results. Injected into each transaction
// service to call before executing the business logic.
// =============================================================================

using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production.Validators;

/// <summary>
/// Orchestrates all registered IProductionTransactionValidator instances
/// for a given transaction action. Returns aggregated blockers + warnings.
/// </summary>
public interface ITransactionValidationPipeline
{
    /// <summary>
    /// Run all applicable validators for the given context.
    /// Returns null if all pass (proceed). Returns a failure message if blocked.
    /// </summary>
    Task<TransactionValidationPipelineResult> RunAsync(
        TransactionValidationContext context,
        CancellationToken ct = default);
}

/// <summary>Pipeline aggregated result.</summary>
public sealed record TransactionValidationPipelineResult(
    bool IsBlocked,
    IReadOnlyList<TransactionValidationError> Blockers,
    IReadOnlyList<TransactionValidationError> Warnings)
{
    /// <summary>Combined blocker message for Result.Failure.</summary>
    public string BlockMessage => string.Join(" | ", Blockers.Select(b => $"[{b.ValidatorName}] {b.Message}"));

    public static readonly TransactionValidationPipelineResult Passed = new(false,
        Array.Empty<TransactionValidationError>(), Array.Empty<TransactionValidationError>());
}

public sealed class TransactionValidationPipeline : ITransactionValidationPipeline
{
    private readonly IEnumerable<IProductionTransactionValidator> _validators;
    private readonly ILogger<TransactionValidationPipeline> _logger;

    public TransactionValidationPipeline(
        IEnumerable<IProductionTransactionValidator> validators,
        ILogger<TransactionValidationPipeline> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TransactionValidationPipelineResult> RunAsync(
        TransactionValidationContext context,
        CancellationToken ct = default)
    {
        var blockers = new List<TransactionValidationError>();
        var warnings = new List<TransactionValidationError>();
        var validatorCount = 0;

        foreach (var validator in _validators)
        {
            if (!validator.ApplicableActionTypes.Contains(context.ActionType))
                continue;

            validatorCount++;
            try
            {
                var result = await validator.ValidateAsync(context, ct);
                foreach (var error in result.Errors)
                {
                    if (error.Severity == ValidationSeverity.Block)
                    {
                        // Check for supervisor override on overridable blockers
                        if (error.OverrideField != null && context.SupervisorOverride)
                        {
                            _logger.LogWarning(
                                "Validator {Name} blocked {Action} on PRO {ProId} but supervisor override applied: {Msg}",
                                validator.Name, context.ActionType, context.ProductionOrderId, error.Message);
                            warnings.Add(error with { Severity = ValidationSeverity.Warning });
                        }
                        else
                        {
                            blockers.Add(error);
                        }
                    }
                    else
                    {
                        warnings.Add(error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validator {Name} threw on {Action} for PRO {ProId}",
                    validator.Name, context.ActionType, context.ProductionOrderId);
                // Validator exception = warning, not block (fail-open during build-up)
                warnings.Add(new TransactionValidationError(
                    validator.Name, ValidationSeverity.Warning,
                    $"Validator error: {ex.Message}"));
            }
        }

        if (validatorCount > 0)
        {
            _logger.LogInformation(
                "Validation pipeline ran {Count} validators for {Action} on PRO {ProId}: {Blockers} blockers, {Warnings} warnings",
                validatorCount, context.ActionType, context.ProductionOrderId,
                blockers.Count, warnings.Count);
        }

        return new TransactionValidationPipelineResult(
            blockers.Count > 0,
            blockers,
            warnings);
    }
}
