using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Infrastructure;
using Abs.FixedAssets.Services.Posting;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Receiving
{
    /// <summary>Outcome of a single receipt-posting run.</summary>
    public sealed record ReceivingPostingResult(
        int GoodsReceiptId,
        int? JournalEntryId,
        int InventoryRowsTouched,
        decimal TotalAccrued);

    /// <summary>
    /// Posts inventory movements + GR/IR accrual journal entry for a
    /// just-saved <see cref="GoodsReceipt"/>. Implements ADR-001:
    /// <list type="bullet">
    ///   <item><description>Stock items: increment <see cref="ItemInventory"/>
    ///   at the receiving location + create <see cref="ItemTransaction"/>
    ///   (Type=Receipt) audit row. Dr <see cref="GlAccountKind.Inventory"/>.</description></item>
    ///   <item><description>Non-stock items (<see cref="ItemType.Service"/>):
    ///   no inventory write. Dr <see cref="GlAccountKind.DirectExpense"/>.</description></item>
    ///   <item><description>CIP-tagged lines: posted by <c>CipAutoCostPostingService</c>
    ///   (PR #37). Dr <see cref="GlAccountKind.CipPending"/>.</description></item>
    ///   <item><description>All Drs aggregate against a single Cr to
    ///   <see cref="GlAccountKind.GrAccrued"/> (Goods Received Not Invoiced).</description></item>
    /// </list>
    ///
    /// Idempotent on retry: a duplicate post detects the existing
    /// <c>JournalEntry.Reference == "GR-{ReceiptNumber}"</c> and returns
    /// without re-writing inventory or the JE.
    /// </summary>
    public interface IReceivingPostingService
    {
        Task<ReceivingPostingResult> PostReceiptAsync(int goodsReceiptId);

        /// <summary>
        /// PR #105 / B-17: reverse the inventory move + post a reversing JE
        /// for any GR line where the inspector recorded a non-zero
        /// <c>QuantityRejected</c>. Called at the end of an Inspect-Complete
        /// workflow. Idempotent: re-running against the same receipt skips
        /// when a "GR-REV" reference already exists. Returns the reversing
        /// JE id (or null if there was nothing to reverse).
        /// </summary>
        Task<ReceivingPostingResult> PostRejectionReversalAsync(int goodsReceiptId);
    }

    public class ReceivingPostingService : IReceivingPostingService, IPostingService<ReceiveGoodsRequest>
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IGlAccountResolver _glResolver;
        private readonly IOutboxWriter _outbox;
        private readonly IIdempotencyMediator _idempotency;
        private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
        private readonly IReceiptToJobService _receiptToJob;
        private readonly ILogger<ReceivingPostingService> _logger;

        public ReceivingPostingService(
            AppDbContext db,
            ITenantContext tenantContext,
            IGlAccountResolver glResolver,
            IOutboxWriter outbox,
            IIdempotencyMediator idempotency,
            Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
            IReceiptToJobService receiptToJob,
            ILogger<ReceivingPostingService> logger)
        {
            _db = db;
            _tenantContext = tenantContext;
            _glResolver = glResolver;
            _outbox = outbox;
            _idempotency = idempotency;
            _chainOfCustody = chainOfCustody;
            _receiptToJob = receiptToJob;
            _logger = logger;
        }

        // ADR-025 D2 — IPostingService<ReceiveGoodsRequest> implementation.
        //
        // Sprint 12.9 PR #2 — wraps the legacy PostReceiptAsync(int) entry point
        // in the canonical contract: idempotency-keyed, Result-enveloped,
        // CancellationToken-aware. Existing call sites continue to use the
        // legacy method; new code paths (Receiving Control Center cockpit voice
        // tools, Sprint 13/14 Control Centers) should call PostAsync.
        //
        // PostRejectionReversalAsync stays on IReceivingPostingService — it's a
        // separate logical post operation. When that flow needs the IPostingService
        // contract too, add a sibling IPostingService<RejectGoodsReversalRequest>
        // implementation in a follow-up PR rather than overload PostAsync.
        async Task<Result<PostingReceipt>> IPostingService<ReceiveGoodsRequest>.PostAsync(
            ReceiveGoodsRequest source,
            int actorUserId,
            Guid idempotencyKey,
            CancellationToken ct)
        {
            return await _idempotency.ExecuteAsync(
                actorUserId,
                idempotencyKey,
                source,
                async innerCt =>
                {
                    try
                    {
                        var legacy = await PostReceiptAsync(source.GoodsReceiptId);

                        // Map ReceivingPostingResult → PostingReceipt envelope. The
                        // legacy result records InventoryRowsTouched + TotalAccrued
                        // (the credit side of the JE — Cr GR-Accrued). For posting
                        // contract purposes, debits balance to TotalAccrued by
                        // construction (every Dr line aggregates against the single
                        // Cr GR-Accrued credit).
                        var receipt = new PostingReceipt(
                            JournalEntryId: legacy.JournalEntryId,
                            LinesPosted: 0,
                            TotalDebits: legacy.TotalAccrued,
                            TotalCredits: legacy.TotalAccrued,
                            WasReplay: false,
                            AuditEventId: null);

                        return Result.Success(receipt);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Result.Failure<PostingReceipt>(ex.Message);
                    }
                },
                ct);
        }

        public async Task<ReceivingPostingResult> PostReceiptAsync(int goodsReceiptId)
        {
            var receipt = await _db.GoodsReceipts
                .Include(r => r.Lines)
                    .ThenInclude(l => l.PurchaseOrderLine)
                        .ThenInclude(pl => pl!.Item)
                .Include(r => r.Lines)
                    .ThenInclude(l => l.PurchaseOrderLine)
                        .ThenInclude(pl => pl!.PurchaseOrder)
                .Where(r => r.Id == goodsReceiptId &&
                    _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (receipt == null)
            {
                _logger.LogWarning("ReceivingPostingService: GR {Id} not found or out of tenant scope.", goodsReceiptId);
                return new ReceivingPostingResult(goodsReceiptId, null, 0, 0m);
            }

            // DEF-N12 (PR #88): receipt.ReceiptNumber is generated as
            // "GR-{yyyyMMdd}-{HHmmss}" in Receive.cshtml.cs::OnPostAsync, so
            // the old "$\"GR-{ReceiptNumber}\"" format produced doubled
            // "GR-GR-..." entry numbers in the journal listing. Use the
            // ReceiptNumber as-is when it already carries the GR- prefix;
            // fall back to prepending for any future receipt-number scheme
            // that doesn't. The idempotency lookup also accepts the legacy
            // "GR-GR-..." format so re-processing an old receipt still
            // short-circuits cleanly without producing a duplicate JE.
            var jeReference = receipt.ReceiptNumber.StartsWith("GR-", StringComparison.OrdinalIgnoreCase)
                ? receipt.ReceiptNumber
                : $"GR-{receipt.ReceiptNumber}";
            var legacyJeReference = $"GR-{receipt.ReceiptNumber}";

            // Idempotency guard: existing JE means we've posted this receipt before.
            // Check both the new and legacy reference formats for pre-#88 entries.
            var existingJe = await _db.JournalEntries
                .Where(j => (j.Reference == jeReference || j.Reference == legacyJeReference) && j.Source == "GR")
                .Select(j => (int?)j.Id)
                .FirstOrDefaultAsync();
            if (existingJe.HasValue)
            {
                _logger.LogInformation("ReceivingPostingService: GR {Id} already posted as JE {JeId}, skipping.",
                    goodsReceiptId, existingJe.Value);
                return new ReceivingPostingResult(goodsReceiptId, existingJe.Value, 0, 0m);
            }

            var receiptCompanyId = receipt.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (receiptCompanyId == 0)
            {
                _logger.LogWarning("ReceivingPostingService: GR {Id} has no resolvable CompanyId; skipping post.", goodsReceiptId);
                return new ReceivingPostingResult(goodsReceiptId, null, 0, 0m);
            }

            // Aggregate JE debits by GL account, plus a single GR-Accrued credit.
            var debitTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);
            // PRA-5d — DEF-008 dual-write: parallel dict mapping account-number
            // string → AccountingKeyId. Stamped on every JournalLine alongside
            // legacy Account string. Try/catch in ResolveAccountAndKeyAsync
            // keeps legacy flow working if AccountingKey resolution fails.
            var accountToKeyId = new Dictionary<string, int?>(StringComparer.Ordinal);
            decimal totalAccrued = 0m;
            int inventoryRowsTouched = 0;
            var inventoryReceipts = new List<(GoodsReceiptLine Line, PurchaseOrderLine PoLine, decimal Quantity, int LocationId, decimal NewQuantityOnHand)>();

            foreach (var line in receipt.Lines)
            {
                var poLine = line.PurchaseOrderLine;
                if (poLine == null) continue;

                var quantity = line.QuantityAccepted > 0 ? line.QuantityAccepted : line.QuantityReceived;
                if (quantity <= 0) continue;

                var lineAmount = quantity * poLine.UnitPrice;
                if (lineAmount <= 0) continue;

                var item = poLine.Item;
                var isStock = item != null && item.Type != ItemType.Service;
                var isCipTagged = (line.CipProjectId ?? poLine.CipProjectId).HasValue;

                // Determine the Dr account.
                GlAccountKind drKind;
                if (isCipTagged)
                {
                    // Skip the GR-Accrued posting entirely — CIP routing handled
                    // by CipAutoCostPostingService.PostFromReceiptLineAsync (PR #37).
                    // Per ADR-001 D-2: CIP-tagged stock receipts don't move
                    // inventory either; they go straight to CIP basis.
                    continue;
                }
                else if (line.IsDirectToJob)
                {
                    // Sprint 15.1 PR-1 — Direct-to-job receipt line. Bypass
                    // inventory entirely; IReceiptToJobService handles:
                    //   - CostTransaction(PurchasedToJobReceipt) → PRO
                    //   - JE: Dr WIP-Material, Cr GR-Accrued
                    //   - BOM line supply link update
                    // Delegation is fire-and-forget safe — errors are logged
                    // but don't block the remaining standard inventory lines.
                    if (line.DirectToJobProductionOrderId.HasValue && line.DirectToJobBomLineId.HasValue)
                    {
                        try
                        {
                            await _receiptToJob.ReceiveToJobAsync(
                                new ReceiveToJobRequest(
                                    line.Id,
                                    line.DirectToJobProductionOrderId.Value,
                                    line.DirectToJobBomLineId.Value));
                        }
                        catch (Exception dtjEx)
                        {
                            _logger.LogWarning(dtjEx,
                                "ReceivingPostingService: DTJ delegation failed for GR line {LineId}; " +
                                "standard inventory path not applied either. Manual intervention needed.",
                                line.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ReceivingPostingService: GR line {LineId} is IsDirectToJob but missing " +
                            "PRO/BOM linkage. Skipping — no inventory or DTJ posting.",
                            line.Id);
                    }
                    continue;
                }
                else if (isStock)
                {
                    drKind = GlAccountKind.Inventory;
                }
                else
                {
                    // Service / non-stock — direct expense.
                    drKind = GlAccountKind.DirectExpense;
                }

                var ctx = new GlResolveContext(PurchaseOrderLineId: poLine.Id);
                var (drAccount, drKeyId) = await _glResolver.ResolveAccountAndKeyAsync(receiptCompanyId, drKind, ctx, logger: _logger, logContext: $"receipt={receipt.ReceiptNumber} line={line.Id}");
                debitTotals[drAccount] = debitTotals.GetValueOrDefault(drAccount, 0m) + lineAmount;
                accountToKeyId[drAccount] = drKeyId;
                totalAccrued += lineAmount;

                // Stock items: move inventory + write transaction.
                if (isStock && line.ReceivingLocationId.HasValue)
                {
                    var newOnHand = await ApplyInventoryReceiptAsync(line, poLine, quantity, receiptCompanyId, receipt.ReceiptDate);
                    inventoryRowsTouched++;
                    inventoryReceipts.Add((line, poLine, quantity, line.ReceivingLocationId.Value, newOnHand));
                }
            }

            // No accruable lines? (everything was CIP-routed or zero-quantity.) Nothing to post.
            if (totalAccrued <= 0)
            {
                return new ReceivingPostingResult(goodsReceiptId, null, inventoryRowsTouched, 0m);
            }

            var (grAccruedAccount, grAccruedKeyId) = await _glResolver.ResolveAccountAndKeyAsync(receiptCompanyId, GlAccountKind.GrAccrued, new GlResolveContext(), logger: _logger, logContext: $"receipt={receipt.ReceiptNumber} gr-accrued");

            var je = new JournalEntry
            {
                BookId = null, // GR/IR accruals are not book-scoped; the Book FK is nullable per the model. Per-company default-book resolution can replace this if/when product semantics require it.
                Batch = jeReference,   // DEF-N12: was $"GR-{receipt.ReceiptNumber}" producing doubled GR-GR-...
                Period = int.Parse(receipt.ReceiptDate.ToString("yyyyMM")),
                PostingDate = receipt.ReceiptDate.Date,
                Source = "GR",
                Reference = jeReference,
                Description = $"Goods receipt accrual for {receipt.ReceiptNumber}",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>()
            };

            int lineNo = 1;
            foreach (var (account, amount) in debitTotals.OrderBy(kv => kv.Key))
            {
                je.Lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = account,
                    AccountingKeyId = accountToKeyId.TryGetValue(account, out var keyId) ? keyId : null,
                    Description = $"GR {receipt.ReceiptNumber}",
                    Debit = amount,
                    Credit = 0m
                });
            }
            je.Lines.Add(new JournalLine
            {
                LineNo = lineNo,
                Account = grAccruedAccount,
                AccountingKeyId = grAccruedKeyId,
                Description = $"GR-Accrued {receipt.ReceiptNumber}",
                Debit = 0m,
                Credit = totalAccrued
            });

            _db.JournalEntries.Add(je);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "ReceivingPostingService: posted GR {Id} → JE {JeId}, accrued {Total} across {DrCount} debit account(s)",
                goodsReceiptId, je.Id, totalAccrued, debitTotals.Count);

            // One item.received per stock line that moved inventory.
            // Emitted only after the JE save succeeds — partners get a
            // signal that's grounded in committed financial state.
            foreach (var (line, poLine, quantity, locationId, newOnHand) in inventoryReceipts)
            {
                await _outbox.EnqueueAsync(
                    receiptCompanyId,
                    siteId: null,
                    new ItemReceivedV1(
                        ItemId: poLine.ItemId ?? 0,
                        LocationId: locationId,
                        CompanyId: receiptCompanyId,
                        GoodsReceiptId: receipt.Id,
                        GoodsReceiptLineId: line.Id,
                        PurchaseOrderId: poLine.PurchaseOrder?.Id,
                        PurchaseOrderLineId: poLine.Id,
                        Quantity: quantity,
                        UnitCost: poLine.UnitPrice,
                        NewQuantityOnHand: newOnHand,
                        LotNumber: line.LotNumber,
                        SerialNumber: line.SerialNumber,
                        ReceiptDate: receipt.ReceiptDate),
                    correlationId: $"item-receipt-{line.Id}"
                );
            }

            // Sprint 12D PR #3 / ADR-022 §D5 — chain-of-custody graph emission.
            //
            // This is the EVS demo chain. Voice query "why is receipt X blocked?"
            // walks BACKWARDS from the Receipt node through these edges to find
            // its PO, its Vendor, and the Items it carries — and then narrates
            // the chain via LLM (Sprint 12D PR #5).
            //
            // Edges emitted per receipt:
            //   Receipt --RECEIVED_AT--> PurchaseOrder      (one edge per unique PO)
            //   PurchaseOrder --SUPPLIED_BY--> Vendor       (one edge per unique vendor)
            //   Receipt --CONTAINS_ITEM--> Item             (one edge per unique item)
            //
            // Failures of the graph emit DO NOT roll back the JE. The chain is
            // a read-only narration substrate — losing one edge is recoverable
            // (the chain can be rebuilt from the relational data via PR #6's
            // backfill tool). Wrapping in a per-edge try/catch matches the
            // pattern AuditService.LogAsync uses for its bidirectional-nav
            // safety net (memory feedback_audit_log_serialization).
            try
            {
                var emittedPos = new HashSet<int>();
                var emittedVendors = new HashSet<int>();
                var emittedItems = new HashSet<int>();
                foreach (var (line, poLine, _, _, _) in inventoryReceipts)
                {
                    // Receipt -> PurchaseOrder
                    var po = poLine.PurchaseOrder;
                    if (po != null && emittedPos.Add(po.Id))
                    {
                        await _chainOfCustody.RecordEdgeAsync(
                            new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                                FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Receipt,
                                FromEntityId: receipt.Id,
                                FromLabel:    receipt.ReceiptNumber,
                                ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.PurchaseOrder,
                                ToEntityId:   po.Id,
                                ToLabel:      po.PONumber ?? $"PO-{po.Id}",
                                EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.ReceivedAt));

                        // PurchaseOrder -> Vendor (one per unique vendor across this receipt)
                        if (po.VendorId > 0 && emittedVendors.Add(po.VendorId))
                        {
                            await _chainOfCustody.RecordEdgeAsync(
                                new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                                    FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.PurchaseOrder,
                                    FromEntityId: po.Id,
                                    FromLabel:    po.PONumber ?? $"PO-{po.Id}",
                                    ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Vendor,
                                    ToEntityId:   po.VendorId,
                                    ToLabel:      $"Vendor-{po.VendorId}",
                                    EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.SuppliedBy));
                        }
                    }

                    // Receipt -> Item
                    var itemId = poLine.ItemId ?? 0;
                    if (itemId > 0 && emittedItems.Add(itemId))
                    {
                        var itemLabel = poLine.Item?.PartNumber ?? $"Item-{itemId}";
                        await _chainOfCustody.RecordEdgeAsync(
                            new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                                FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Receipt,
                                FromEntityId: receipt.Id,
                                FromLabel:    receipt.ReceiptNumber,
                                ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Item,
                                ToEntityId:   itemId,
                                ToLabel:      itemLabel,
                                EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.ContainsItem));
                    }
                }
            }
            catch (Exception chainEx)
            {
                _logger.LogWarning(chainEx,
                    "ReceivingPostingService: chain-of-custody emit failed for GR {Id} — JE {JeId} still committed; chain can be rebuilt via backfill.",
                    goodsReceiptId, je.Id);
            }

            return new ReceivingPostingResult(goodsReceiptId, je.Id, inventoryRowsTouched, totalAccrued);
        }

        /// <summary>
        /// Increments <see cref="ItemInventory"/> at the receiving location
        /// and writes an <see cref="ItemTransaction"/> audit row. The natural
        /// key on inventory is (ItemId, LocationId, CompanyId); creates the
        /// row if missing. Returns the post-increment on-hand quantity so
        /// the caller can include it in the <c>item.received</c> payload.
        /// </summary>
        private async Task<decimal> ApplyInventoryReceiptAsync(
            GoodsReceiptLine line,
            PurchaseOrderLine poLine,
            decimal quantity,
            int companyId,
            DateTime receiptDate)
        {
            var locationId = line.ReceivingLocationId!.Value;

            var inv = await _db.Set<ItemInventory>()
                .FirstOrDefaultAsync(i =>
                    i.ItemId == poLine.ItemId &&
                    i.LocationId == locationId &&
                    i.CompanyId == companyId);

            if (inv == null)
            {
                inv = new ItemInventory
                {
                    ItemId = poLine.ItemId ?? 0,
                    LocationId = locationId,
                    CompanyId = companyId,
                    QuantityOnHand = quantity,
                    LastReceiptDate = receiptDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Set<ItemInventory>().Add(inv);
            }
            else
            {
                inv.QuantityOnHand += quantity;
                inv.LastReceiptDate = receiptDate;
                inv.UpdatedAt = DateTime.UtcNow;
            }

            _db.Set<ItemTransaction>().Add(new ItemTransaction
            {
                TransactionNumber = $"GR{line.GoodsReceiptId}-L{line.LineNumber}-{DateTime.UtcNow.Ticks}",
                ItemId = poLine.ItemId ?? 0,
                Type = TransactionType.Receipt,
                Quantity = quantity,
                UnitCost = poLine.UnitPrice,
                ToLocationId = locationId,
                LotNumber = line.LotNumber,
                SerialNumber = line.SerialNumber
            });

            return inv.QuantityOnHand;
        }

        /// <summary>
        /// PR #105 / B-17: Reverse the inventory move + post a balanced
        /// reversing JE for any GR line where <c>QuantityRejected &gt; 0</c>.
        /// Mirrors the shape of <see cref="PostReceiptAsync"/> but with
        /// debits and credits flipped, scoped only to the rejected portion
        /// of each line. Idempotent via Source="GR-REV" + Reference lookup.
        /// </summary>
        public async Task<ReceivingPostingResult> PostRejectionReversalAsync(int goodsReceiptId)
        {
            var receipt = await _db.GoodsReceipts
                .Include(r => r.Lines)
                    .ThenInclude(l => l.PurchaseOrderLine)
                        .ThenInclude(pl => pl!.Item)
                .Where(r => r.Id == goodsReceiptId &&
                    _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (receipt == null)
            {
                _logger.LogWarning("PostRejectionReversalAsync: GR {Id} not found or out of tenant scope.", goodsReceiptId);
                return new ReceivingPostingResult(goodsReceiptId, null, 0, 0m);
            }

            var revReference = $"REV-GR-{receipt.ReceiptNumber}";

            // Idempotency: if a reversal already exists, do nothing. A
            // second-pass inspection that changes the rejection number after
            // the first commit would need a different ref scheme; for the
            // single-shot OnPostCompleteAsync invocation this is correct.
            var existingRev = await _db.JournalEntries
                .Where(j => j.Reference == revReference && j.Source == "GR-REV")
                .Select(j => (int?)j.Id)
                .FirstOrDefaultAsync();
            if (existingRev.HasValue)
            {
                _logger.LogInformation("PostRejectionReversalAsync: GR {Id} already reversed as JE {JeId}, skipping.",
                    goodsReceiptId, existingRev.Value);
                return new ReceivingPostingResult(goodsReceiptId, existingRev.Value, 0, 0m);
            }

            var receiptCompanyId = receipt.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (receiptCompanyId == 0)
            {
                _logger.LogWarning("PostRejectionReversalAsync: GR {Id} has no resolvable CompanyId; skipping.", goodsReceiptId);
                return new ReceivingPostingResult(goodsReceiptId, null, 0, 0m);
            }

            var creditTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);
            // PRA-5d — DEF-008 dual-write parallel dict (reversal-side).
            var accountToKeyId = new Dictionary<string, int?>(StringComparer.Ordinal);
            decimal totalReversed = 0m;
            int inventoryRowsTouched = 0;

            foreach (var line in receipt.Lines)
            {
                if (line.QuantityRejected <= 0) continue;

                var poLine = line.PurchaseOrderLine;
                if (poLine == null) continue;

                var qty = line.QuantityRejected;
                var amount = qty * poLine.UnitPrice;
                if (amount <= 0) continue;

                var item = poLine.Item;
                var isStock = item != null && item.Type != ItemType.Service;
                var isCipTagged = (line.CipProjectId ?? poLine.CipProjectId).HasValue;

                if (isCipTagged)
                {
                    // CIP receipts never moved inventory and never debited
                    // Inventory/Expense — they routed straight to CIP-Pending
                    // via a separate service. Rejection reversal on CIP lines
                    // is out of scope for this PR (admins handle CIP manually).
                    continue;
                }

                GlAccountKind crKind = isStock ? GlAccountKind.Inventory : GlAccountKind.DirectExpense;
                var ctx = new GlResolveContext(PurchaseOrderLineId: poLine.Id);
                var (crAccount, crKeyId) = await _glResolver.ResolveAccountAndKeyAsync(receiptCompanyId, crKind, ctx, logger: _logger, logContext: $"gr-rev receipt={receipt.ReceiptNumber} line={line.Id}");
                creditTotals[crAccount] = creditTotals.GetValueOrDefault(crAccount, 0m) + amount;
                accountToKeyId[crAccount] = crKeyId;
                totalReversed += amount;

                // Stock items: decrement on-hand + write a negative-qty Adjust
                // transaction. Don't go below zero — if inventory has already
                // been issued, the supplier-return path should handle that
                // separately. This service handles the clean "rejected on
                // dock, never entered usable inventory" case.
                if (isStock && line.ReceivingLocationId.HasValue)
                {
                    var locationId = line.ReceivingLocationId.Value;
                    var inv = await _db.Set<ItemInventory>()
                        .FirstOrDefaultAsync(i =>
                            i.ItemId == poLine.ItemId &&
                            i.LocationId == locationId &&
                            i.CompanyId == receiptCompanyId);
                    if (inv != null)
                    {
                        inv.QuantityOnHand -= qty;
                        if (inv.QuantityOnHand < 0m) inv.QuantityOnHand = 0m;
                        inv.UpdatedAt = DateTime.UtcNow;
                    }

                    _db.Set<ItemTransaction>().Add(new ItemTransaction
                    {
                        TransactionNumber = $"GR-REV{line.GoodsReceiptId}-L{line.LineNumber}-{DateTime.UtcNow.Ticks}",
                        ItemId = poLine.ItemId ?? 0,
                        Type = TransactionType.Adjust,
                        Quantity = -qty,
                        UnitCost = poLine.UnitPrice,
                        ToLocationId = locationId,
                        LotNumber = line.LotNumber,
                        SerialNumber = line.SerialNumber
                    });
                    inventoryRowsTouched++;
                }
            }

            if (totalReversed <= 0)
            {
                return new ReceivingPostingResult(goodsReceiptId, null, 0, 0m);
            }

            // Reversing JE: DR GR-Accrued / CR Inventory (or DirectExpense per
            // line) for the rejected portion. Trial balance net of the
            // Receipt JE + this reversal equals just the accepted portion,
            // which is what AP will eventually invoice and pay against.
            var (grAccruedAccount, grAccruedKeyId) = await _glResolver.ResolveAccountAndKeyAsync(receiptCompanyId, GlAccountKind.GrAccrued, new GlResolveContext(), logger: _logger, logContext: $"gr-rev={receipt.ReceiptNumber} accrued");

            var je = new JournalEntry
            {
                BookId = null,
                Batch = revReference,
                Period = int.Parse(receipt.ReceiptDate.ToString("yyyyMM")),
                PostingDate = DateTime.UtcNow.Date,
                Source = "GR-REV",
                Reference = revReference,
                Description = $"Rejection reversal for {receipt.ReceiptNumber}",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>()
            };

            int lineNo = 1;
            je.Lines.Add(new JournalLine
            {
                LineNo = lineNo++,
                Account = grAccruedAccount,
                AccountingKeyId = grAccruedKeyId,
                Description = $"GR-REV {receipt.ReceiptNumber}",
                Debit = totalReversed,
                Credit = 0m
            });
            foreach (var (account, amount) in creditTotals.OrderBy(kv => kv.Key))
            {
                je.Lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = account,
                    AccountingKeyId = accountToKeyId.TryGetValue(account, out var keyId) ? keyId : null,
                    Description = $"GR-REV {receipt.ReceiptNumber}",
                    Debit = 0m,
                    Credit = amount
                });
            }

            _db.JournalEntries.Add(je);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "PostRejectionReversalAsync: posted GR-REV for {Id} → JE {JeId}, reversed {Total} across {CrCount} credit account(s), {InvCount} inventory rows adjusted",
                goodsReceiptId, je.Id, totalReversed, creditTotals.Count, inventoryRowsTouched);

            return new ReceivingPostingResult(goodsReceiptId, je.Id, inventoryRowsTouched, totalReversed);
        }

        // PRA-5d inline helper extracted to shared
        // Services/Posting/GlPostingHelpers.ResolveAccountAndKeyAsync in
        // PRA-5e.1. Call sites use the extension method directly.
    }
}
