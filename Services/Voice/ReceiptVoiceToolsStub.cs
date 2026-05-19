using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Receiving;

namespace Abs.FixedAssets.Services.Voice;

// ReceiptVoiceToolsStub — kept as a test fixture only.
//
// Sprint 11 PR #4 introduced ReceiptVoiceTools (the real production
// implementation backed by AppDbContext + IReceivingControlCenterService).
// Program.cs DI now resolves IReceiptVoiceTools to ReceiptVoiceTools.
//
// This stub stays in the codebase so unit tests and mocking scenarios have
// an interface-complete null object to wire when they don't need the full
// AppDbContext + downstream service graph. Every method returns
// Result.Failure with a stub message — useful as a "did you forget to wire
// the real impl?" detector.
public sealed class ReceiptVoiceToolsStub : IReceiptVoiceTools
{
    private const string StubMessage =
        "ReceiptVoiceToolsStub is a test fixture only. Inject ReceiptVoiceTools " +
        "(or a Moq) for production code paths.";

    public Task<Result<ChainOfCustodyGraph>> TraceChainOfCustodyAsync(
        string naturalKey, string direction, CancellationToken ct)
        => Task.FromResult(Result.Failure<ChainOfCustodyGraph>(StubMessage));

    public Task<Result<IReadOnlyList<ExpectedReceiptItem>>> ListExpectedReceiptsAsync(
        DateTime fromUtc, DateTime toUtc, int? forUserId, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<ExpectedReceiptItem>>(StubMessage));

    public Task<Result<int>> QuarantineByFilterAsync(
        string profileCode,
        IReadOnlyDictionary<string, object?> attributeFilter,
        string reason,
        int actorUserId,
        Guid idempotencyKey,
        CancellationToken ct)
        => Task.FromResult(Result.Failure<int>(StubMessage));

    public Task<Result<IReadOnlyList<StockReceipt>>> LookupReceiptAsync(
        string naturalKey, string? profileHint, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<StockReceipt>>(StubMessage));

    public Task<Result<IReadOnlyList<ExpectedArrival>>> ListExpectedArrivalsAsync(
        string? siteCode, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<ExpectedArrival>>(StubMessage));

    public Task<Result<IReadOnlyList<OrphanMatchCandidate>>> MatchOrphanReceiptAsync(
        int receiptId, int actorUserId, CancellationToken ct)
        => Task.FromResult(Result.Failure<IReadOnlyList<OrphanMatchCandidate>>(StubMessage));

    public Task<Result<ExceptionExplanation>> ExplainExceptionAsync(
        int receiptId, CancellationToken ct)
        => Task.FromResult(Result.Failure<ExceptionExplanation>(StubMessage));

    public Task<Result<ReceiveResult>> ReceiveByVoiceAsync(
        int actorUserId, IdempotencyKey idempotencyKey, ReceiveByPoCommand command,
        VoiceContext voiceContext, CancellationToken ct)
        => Task.FromResult(Result.Failure<ReceiveResult>(StubMessage));

    public Task<Result<QuarantineResult>> QuarantineByVoiceAsync(
        int actorUserId, IdempotencyKey idempotencyKey, QuarantineCommand command,
        VoiceContext voiceContext, CancellationToken ct)
        => Task.FromResult(Result.Failure<QuarantineResult>(StubMessage));

    public Task<Result<MillCertExtraction>> OcrParseMillCertAsync(
        byte[] pdfBytes, string profileCode, CancellationToken ct)
        => Task.FromResult(Result.Failure<MillCertExtraction>(StubMessage));
}
