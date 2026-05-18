using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Voice;

// ADR-015 D10 — Stub implementation of IReceiptVoiceTools.
//
// All four methods return Result.Failure with a clear "not yet implemented"
// message. Real bodies land in Sprint 5 voice-AI runtime work. Shipping
// the stub now means:
//   - The DI container resolves the interface today.
//   - The voice-AI integration tests can target stable interfaces.
//   - The tool catalog shape is locked at PR time and won't drift.
//
// DO NOT call these from production code paths in Sprint 4. The receipt
// UI continues to use IStockReceiptService for everything. These tools
// are for the voice-AI runtime only.
public sealed class ReceiptVoiceToolsStub : IReceiptVoiceTools
{
    private const string NotImplementedMessage =
        "IReceiptVoiceTools is a Sprint 5 surface. Migration PR #1 ships " +
        "the contract only; the real implementation lands with the voice-AI " +
        "runtime. Use IStockReceiptService for receipt operations in Sprint 4.";

    public Task<Result<ChainOfCustodyGraph>> TraceChainOfCustodyAsync(
        string naturalKey, string direction, CancellationToken ct)
        => Task.FromResult(Result.Failure<ChainOfCustodyGraph>(NotImplementedMessage));

    public Task<Result<IReadOnlyList<ExpectedReceiptItem>>> ListExpectedReceiptsAsync(
        DateTime fromUtc, DateTime toUtc, int? forUserId, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<ExpectedReceiptItem>>(NotImplementedMessage));

    public Task<Result<int>> QuarantineByFilterAsync(
        string profileCode,
        IReadOnlyDictionary<string, object?> attributeFilter,
        string reason,
        int actorUserId,
        Guid idempotencyKey,
        CancellationToken ct)
        => Task.FromResult(Result.Failure<int>(NotImplementedMessage));

    public Task<Result<IReadOnlyList<StockReceipt>>> LookupReceiptAsync(
        string naturalKey, string? profileHint, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<StockReceipt>>(NotImplementedMessage));
}
