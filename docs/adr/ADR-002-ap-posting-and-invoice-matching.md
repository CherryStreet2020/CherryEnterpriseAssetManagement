# ADR-002: AP posting + invoice matching

**Status:** proposed (2026-05-08).
**Closes audit findings:** S1-5 and S2-10 from [`docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md`](../audit-2026-05-08-followup/STRUCTURAL_AUDIT.md).
**Depends on:** ADR-001 (GR-Accrued is the clearing account), ADR-003 (central GL account resolver).

---

## Context

Today, [Pages/AccountsPayable/Details.cshtml.cs:160-217](../../Pages/AccountsPayable/Details.cshtml.cs:160) flips invoice statuses (Approve / RecordPayment / Void) and stamps audit fields — but creates **no** `JournalEntry` and never invokes `PeriodGuard`. The `InvoiceMatchingService` exists but is not invoked from the approve flow ([`Services/InvoiceMatchingService.cs`](../../Services/InvoiceMatchingService.cs)).

The downstream consequence: there is no AP balance, no expense or asset-cost posting, and three-way match status stays at `NotMatched` regardless of reality. Voice-to-WO and partner integrations would see invoices "approved" with zero ledger impact.

This ADR defines the AP posting flow, the three-way match enforcement, and the payment posting flow.

## Decisions

### D-002-1. Two journal entries per invoice lifecycle

| Event | Journal entry |
|---|---|
| **Approve** (after match) | Dr `GR-Accrued` (matched lines) + Dr expense / asset / WIP (unmatched lines) + Dr/Cr `PurchasePriceVariance` (if invoice unit cost ≠ PO unit cost on matched lines) — Cr `Accounts Payable` |
| **Record payment** | Dr `Accounts Payable` (payment amount) — Cr `Cash` |
| **Void** (if approved before void) | Reverses the approval JE with a new contra JE; the original is preserved (append-only ledger principle) |

**Why two entries**: standard accrual accounting. The approval-time entry captures the obligation; the payment-time entry captures the cash outflow. They MUST be separate so AP aging works (Outstanding AP = sum of approved-but-unpaid).

### D-002-2. Three-way match is a hard gate on approval

Before an invoice can be approved, every invoice line linked to a `PurchaseOrderLineId` MUST also have a `GoodsReceiptLineId` AND match within tolerance:

- **Quantity match:** `InvoiceLine.Quantity == PoLine.QuantityOrdered` (or within received qty for partial bills) AND `GoodsReceiptLine.QuantityReceived >= InvoiceLine.Quantity`.
- **Price match:** `|InvoiceLine.UnitPrice − PoLine.UnitPrice| / PoLine.UnitPrice <= MatchTolerancePercent` (default 1%, per-Company override on `Companies.MatchTolerancePercent`).

If any line fails, `MatchStatus = Exception` and approval is blocked unless an admin user clicks "Override Match" (separate handler with its own audit log). Unmatched lines (no `PurchaseOrderLineId`) are allowed to approve directly — they're "manual" invoices.

### D-002-3. PPV (Purchase Price Variance) handling

When the invoice unit cost differs from the PO unit cost on a matched line, the variance posts to `PurchasePriceVariance`:

```
Matched line, qty 10, PO unit $5, invoice unit $5.50:
  Dr  GR-Accrued       50.00    (qty × PO unit)
  Dr  PPV               5.00    (qty × delta)
   Cr Accounts Payable 55.00    (qty × invoice unit)
```

A favorable variance (invoice cheaper than PO) credits PPV instead.

### D-002-4. Service shape

```csharp
public interface IApPostingService
{
    /// <summary>Approves the invoice: runs the 3-way match check,
    /// posts the approval JE (Dr GR-Accrued/expense/asset, Cr AP),
    /// flips status to Approved. Period-guarded.</summary>
    Task<ApPostingResult> PostApprovalAsync(int invoiceId, bool overrideMatch = false, string approverUsername = "");

    /// <summary>Posts a payment against an approved invoice.
    /// Period-guarded. Updates AmountPaid; flips status to Paid
    /// when AmountPaid >= TotalAmount.</summary>
    Task<ApPostingResult> PostPaymentAsync(int invoiceId, decimal amount, DateTime paymentDate, string paymentReference);

    /// <summary>Reverses a prior approval JE with a contra JE.
    /// Flips status to Void. Period-guarded against the void date.</summary>
    Task<ApPostingResult> PostVoidAsync(int invoiceId, string reason);
}

public sealed record ApPostingResult(
    int InvoiceId,
    int? JournalEntryId,
    InvoiceMatchStatus MatchStatus,
    decimal AmountPosted);
```

### D-002-5. PeriodGuard placement

- `PostApprovalAsync` checks against `InvoiceDate` (or `ApprovedAt` if invoice is back-dated to a closed period — admin override needed).
- `PostPaymentAsync` checks against `PaymentDate`.
- `PostVoidAsync` checks against `DateTime.UtcNow` for the contra entry's posting date.

### D-002-6. Outbox events

- On approve: emit `invoice.approved.v1` AND `invoice.posted.v1` (the latter carries the JE id; the former carries match status).
- On pay: emit `invoice.paid.v1`.
- On void: emit `invoice.voided.v1`.

### D-002-7. Idempotency

Approve, pay, void are each guarded by status check at the top — idempotent on retry, but a duplicate Approve attempt on an already-Approved invoice is a no-op return (`MatchStatus` re-evaluated, no second JE).

### D-002-8. Match-override audit trail

When an admin overrides a failed match, the override creates an `AuditLog` row with `Action = "INVOICE.MATCH_OVERRIDE"`, the invoice id, and a `BeforeJson` snapshot of the failing match details. Override is permission-gated (admin role).

## Implementation phases

| Phase | Scope | Sizing |
|---|---|---|
| 1 | `IApPostingService` + implementation; integrate `InvoiceMatchingService` as the gate; refactor `Details.cshtml.cs` handlers (Approve/RecordPayment/Void) to delegate to the service | ~600 LOC |
| 2 | Tests: 3-way match happy + each failure shape, PPV, partial bill, void, payment in installments | ~400 LOC |
| 3 | UI: render `MatchStatus` + per-line match status on the invoice detail page | ~150 LOC, separate PR |

## Migration

No new tables. Additions:
- `Companies.MatchTolerancePercent` (decimal, nullable, default 0.01) — per-tenant tolerance override.
- `Companies.PpvGlAccount` (string, nullable) — per-tenant PPV account, falls back through ADR-003.
- `VendorInvoice.JournalEntryId` (int, nullable, FK) — links invoice to its approval JE.
- `VendorInvoice.PaymentJournalEntryIds` — out of scope; payments link to JE via `Reference = "AP-{invoiceNumber}-PMT"`.

## Open questions

1. **Multi-line invoices with mixed match status.** Allow approval if SOME lines pass and others are manual? **Recommend: yes** — common case (invoice has both PO lines and freight charges). Per-line treatment.
2. **Foreign exchange.** Skipped per ADR-001 D-3.
3. **Payment terms / discounts.** Net-30 / 2/10 net-30 / etc. **Recommend: defer to a separate ADR** — not blocking Sprint 0.5.
4. **Recurring invoices.** Out of scope.

## Tests

- `ApPostingServiceTests`:
  - `PostApprovalAsync_FullyMatchedInvoice_PostsCorrectJE`
  - `PostApprovalAsync_QuantityMismatch_BlocksWithMatchException`
  - `PostApprovalAsync_PriceWithinTolerance_PostsWithoutPpv`
  - `PostApprovalAsync_PriceOutsideTolerance_PostsWithPpvDr_BlocksApproval`
  - `PostApprovalAsync_OverrideMatch_PostsWithAuditTrail`
  - `PostApprovalAsync_ManualInvoiceNoPoLines_PostsToExpense`
  - `PostApprovalAsync_PeriodClosed_Throws`
  - `PostPaymentAsync_FullPayment_PostsDrApCrCash_FlipsStatusToPaid`
  - `PostPaymentAsync_PartialPayment_PostsAndKeepsApproved`
  - `PostVoidAsync_PostsContraJE_PreservesOriginal`
  - `Outbox_EmitsApprovedAndPostedOnApprove`
