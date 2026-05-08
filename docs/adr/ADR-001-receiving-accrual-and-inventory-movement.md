# ADR-001: Receiving accrual + inventory movement

**Status:** proposed (2026-05-08).
**Closes audit finding:** S1-1 from [`docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md`](../audit-2026-05-08-followup/STRUCTURAL_AUDIT.md).
**Depends on:** ADR-003 (central GL account resolver).
**Sibling:** ADR-002 (AP posting + invoice matching).

---

## Context

Today, [Pages/Receiving/Receive.cshtml.cs:206-261](../../Pages/Receiving/Receive.cshtml.cs:206) creates `GoodsReceipt` + `GoodsReceiptLine` rows and increments `PurchaseOrderLine.QuantityReceived`, with a PeriodGuard call gating the receipt-row write — but does **NOT** touch `ItemInventory.QuantityOnHand`, does NOT create an `ItemTransaction`, and does NOT post a goods-receipt-accrual journal entry.

The downstream consequences: stock counts never change from receipts, three-way match has no inventory side to compare against, and the AP posting flow (ADR-002) has nothing to clear when an invoice arrives. Mobile / voice receiving features built on this layer would capture intent that never lands in the ledger.

This ADR defines the GR/IR (Goods Receipt / Invoice Receipt) accounting model and the service that implements it.

## Decisions

### D-001-1. Two-step accounting model: GR/IR

Adopt the standard ERP GR/IR (Goods Receipt / Invoice Receipt) accrual pattern. On receipt, post:

```
Dr  Inventory (or expense / WIP / CIP)         qty × PO unit cost
   Cr  GR-Accrued (a/k/a "Goods Received Not Invoiced")     same
```

Later, when the invoice posts (ADR-002), it clears `GR-Accrued` against `AP`:

```
Dr  GR-Accrued                                  invoice qty × PO unit cost
    plus any PPV adjustments
   Cr  Accounts Payable                         invoice total
```

**Why:** This separates the operational fact (inventory exists) from the financial fact (we owe money). Standard in SAP, Oracle, MS Dynamics, NetSuite. Lets receiving and AP run on independent timelines without inventory waiting on invoice arrival.

### D-001-2. Inventory movement: stock vs non-stock vs CIP-tagged

Routing per `GoodsReceiptLine`:

| PO line type | `Item.Type` | `PurchaseOrderLine.CipProjectId` | Dr account |
|---|---|---|---|
| Stock | `ItemType.Stock` | null | Inventory (per `IGlAccountResolver(Inventory)`) |
| Stock | `ItemType.Stock` | not null | CIP-Pending (and `CipAutoCostPostingService.PostFromReceiptLineAsync` runs in same TX) |
| Direct charge | `ItemType.Service` / non-stock | null | Direct expense (per resolver) or WIP if `PurchaseOrderLine.WorkOrderId` is set |
| Asset purchase | any | null + `PurchaseOrderLine.AssetId` set | Asset Cost (per resolver, falls back to `Book.GlAccountAsset` of the asset's primary book) |

Stock-item receipts also:
1. Increment `ItemInventory.QuantityOnHand` at `receivingLocationId`. Create the row if missing (`ItemInventory(ItemId, LocationId)` is the natural key).
2. Create an `ItemTransaction(Type = Receipt, Quantity = +qty, ItemId, LocationId, Reference = "GR-{receiptNumber}-{lineNo}", CompanyId, CreatedAt)`.

Non-stock receipts skip the inventory writes — they don't have stockable existence.

### D-001-3. Service shape

```csharp
public interface IReceivingPostingService
{
    /// <summary>Posts inventory movements + GR/IR accrual JE for a
    /// just-saved GoodsReceipt. Called from the receiving page handler
    /// after PeriodGuard + GR row save. MUST run in the same EF
    /// transaction as the GR write.</summary>
    Task<ReceivingPostingResult> PostReceiptAsync(int goodsReceiptId);
}

public sealed record ReceivingPostingResult(
    int GoodsReceiptId,
    int? JournalEntryId,
    int InventoryRowsTouched,
    int CipCostsRouted,
    decimal TotalAccrued);
```

Internal flow:

1. Load GR + lines + PO + PO lines + items + receiving location, all tenant-scoped.
2. PeriodGuard (already invoked at the page level — defensive re-check here).
3. For each GR line, route per the table above:
   - Stock: increment `ItemInventory`, create `ItemTransaction`.
   - CIP-tagged: invoke `CipAutoCostPostingService.PostFromReceiptLineAsync` (S1-3).
   - Asset/Direct: no inventory writes; just GL.
4. Aggregate JE lines by GL account; build a single `JournalEntry`.
5. Save in same transaction.
6. Emit outbox `inventory.received.v1` (when stock items moved) + `gl.posted.v1` (always).

### D-001-4. Reversal pattern

Returns and rejections are handled by `GoodsReceiptLine.QuantityRejected` (existing field). On the receipt itself we post the **net** received quantity (received − rejected). A separate `OnPostReverseReceiptAsync` page handler creates a reversing GR line + reversing JE — out of scope for the initial S1-1 PR (track as a Sprint 0.5 follow-up).

### D-001-5. Multiple receipts per PO line

Cumulative: each receipt increments `PurchaseOrderLine.QuantityReceived` by its received quantity. The "fully received" check at `Receive.cshtml.cs:242` already supports this.

### D-001-6. Idempotency

`ItemTransaction.Reference = "GR-{receiptNumber}-{lineNo}"` is a natural key; combined with a unique index, a duplicate post fails fast. The `JournalEntry.Reference = "GR {receiptNumber}"` is also unique per book (existing JE invariant). Re-running `PostReceiptAsync` for the same GR returns the same `JournalEntryId`.

### D-001-7. Failure handling

If GL resolution or PeriodGuard fails, the GR write itself has already happened — but the page handler wraps both the GR save AND the posting service in a single transaction (using `db.Database.BeginTransactionAsync`), so a failure rolls back BOTH. The page surfaces the error via TempData (PR [#25](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/25) pattern).

## Implementation phases

| Phase | Scope | Sizing |
|---|---|---|
| 1 | New `IReceivingPostingService` + implementation; new `ItemMovementService` for the inventory + transaction writes; integration into `Pages/Receiving/Receive.cshtml.cs` | ~700 LOC |
| 2 | Tests: stock receipt happy path, non-stock direct-charge, CIP-routed, period-closed rejection, idempotency replay | ~250 LOC |
| 3 | Reversal handler (`OnPostReverseReceiptAsync`) | ~200 LOC, separate PR |

## Migration

No new tables. Schema additions:
- `ItemTransaction` already exists ([Models/Item.cs:955+](../../Models/Item.cs)) — confirm shape and add unique index on `Reference` if not present.
- `ItemInventory` already exists ([Models/Item.cs:883+](../../Models/Item.cs)) — confirm the natural key.
- `Companies.GrAccruedGlAccount` (string, nullable) — the per-tenant override of the GR-Accrued account. Resolver falls back through ADR-003's cascade.

## Open questions

1. **Per-PO-line GL hint.** Should `PurchaseOrderLine` carry an explicit `GlAccountId` override that the resolver respects? **Recommend: yes**, as a Phase 2 add — matches enterprise norms. Out of scope for the initial PR.
2. **PPV (purchase price variance).** When invoice ≠ PO unit cost, the variance must hit a PPV account. **Recommend: defer to ADR-002** — receiving uses PO unit cost as the authoritative basis; PPV is purely an AP-side concern.
3. **Multi-currency.** PO `Currency` is captured. Dollar-based posting until real multi-currency lands. **Recommend: defer** — not blocking Sprint 0.5.

## Tests

- `ReceivingPostingServiceTests`:
  - `PostReceiptAsync_StockItem_IncrementsInventoryAndCreatesItemTransaction`
  - `PostReceiptAsync_StockItem_PostsDrInventoryCrGrAccruedJE`
  - `PostReceiptAsync_NonStockItem_PostsDrExpenseCrGrAccrued_NoInventoryWrite`
  - `PostReceiptAsync_AssetItem_PostsDrAssetCostCrGrAccrued`
  - `PostReceiptAsync_CipTaggedLine_RoutesToCipAutoCostPostingService_NoInventoryWrite`
  - `PostReceiptAsync_PeriodClosed_ThrowsAndRollsBack`
  - `PostReceiptAsync_DuplicateReplay_ReturnsSameJEId_NoDoubleInventory`
  - `PostReceiptAsync_RejectedQuantity_PostsNetOnly`
  - `PostReceiptAsync_OutboxEvents_EmitInventoryReceivedAndGlPosted`
