// Sprint 15.1 PR-1 — no-op IReceiptToJobService for test contexts that
// don't exercise direct-to-job receipt flow but DO instantiate services
// that take IReceiptToJobService as a constructor dependency
// (ReceivingPostingService).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Receiving;

namespace Abs.FixedAssets.Tests.TestHelpers;

public sealed class NullReceiptToJobService : IReceiptToJobService
{
    public Task<Result<ReceiptToJobResult>> ReceiveToJobAsync(
        ReceiveToJobRequest request, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ReceiptToJobResult(
            request.GoodsReceiptLineId, null, null, request.BomLineId,
            request.ProductionOrderId, 0m, 0m, false, "No-op test stub")));

    public Task<Result<ReceiptToJobResult>> ReverseReceiptToJobAsync(
        int goodsReceiptLineId, string? reversedBy, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ReceiptToJobResult(
            goodsReceiptLineId, null, null, 0, 0, 0m, 0m, false, "No-op test stub")));

    public Task<Result<ReceiptToJobResult>> CompleteInspectionAndPostAsync(
        int goodsReceiptLineId, decimal quantityAccepted, decimal quantityRejected,
        string? completedBy, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(new ReceiptToJobResult(
            goodsReceiptLineId, null, null, 0, 0, 0m, 0m, false, "No-op test stub")));

    public Task<IReadOnlyList<ReceiptToJobResult>> ProcessDirectToJobLinesAsync(
        int goodsReceiptId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ReceiptToJobResult>>(new List<ReceiptToJobResult>());
}
