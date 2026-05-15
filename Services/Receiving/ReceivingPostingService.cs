using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
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
    }

    public class ReceivingPostingService : IReceivingPostingService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IGlAccountResolver _glResolver;
        private readonly IOutboxWriter _outbox;
        private readonly ILogger<ReceivingPostingService> _logger;

        public ReceivingPostingService(
            AppDbContext db,
            ITenantContext tenantContext,
            IGlAccountResolver glResolver,
            IOutboxWriter outbox,
            ILogger<ReceivingPostingService> logger)
        {
            _db = db;
            _tenantContext = tenantContext;
            _glResolver = glResolver;
            _outbox = outbox;
            _logger = logger;
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

            var jeReference = $"GR-{receipt.ReceiptNumber}";

            // Idempotency guard: existing JE means we've posted this receipt before.
            var existingJe = await _db.JournalEntries
                .Where(j => j.Reference == jeReference && j.Source == "GR")
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
                var drAccount = await _glResolver.ResolveAsync(receiptCompanyId, drKind, ctx);
                debitTotals[drAccount] = debitTotals.GetValueOrDefault(drAccount, 0m) + lineAmount;
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

            var grAccruedAccount = await _glResolver.ResolveAsync(receiptCompanyId, GlAccountKind.GrAccrued, new GlResolveContext());

            var je = new JournalEntry
            {
                BookId = null, // GR/IR accruals are not book-scoped; the Book FK is nullable per the model. Per-company default-book resolution can replace this if/when product semantics require it.
                Batch = $"GR-{receipt.ReceiptNumber}",
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
                    Description = $"GR {receipt.ReceiptNumber}",
                    Debit = amount,
                    Credit = 0m
                });
            }
            je.Lines.Add(new JournalLine
            {
                LineNo = lineNo,
                Account = grAccruedAccount,
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
    }
}
