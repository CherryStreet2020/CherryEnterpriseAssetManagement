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

namespace Abs.FixedAssets.Services.AccountsPayable
{
    public sealed record ApPostingResult(
        int InvoiceId,
        int? JournalEntryId,
        InvoiceMatchStatus MatchStatus,
        decimal AmountPosted);

    /// <summary>
    /// Implements ADR-002. Two journal entries per invoice lifecycle —
    /// one on approval (Dr GR-Accrued / Dr expense / Cr AP), one on
    /// payment (Dr AP / Cr Cash). Three-way match is a hard gate on
    /// approval; admins can override with an audit trail.
    /// </summary>
    public interface IApPostingService
    {
        Task<ApPostingResult> PostApprovalAsync(int invoiceId, bool overrideMatch = false, string approverUsername = "");
        Task<ApPostingResult> PostPaymentAsync(int invoiceId, decimal amount, DateTime paymentDate, string? paymentReference = null);
        Task<ApPostingResult> PostVoidAsync(int invoiceId, string reason);
    }

    public class ApPostingService : IApPostingService, IPostingService<ApInvoiceApprovalRequest>
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IGlAccountResolver _glResolver;
        private readonly IPeriodGuard _periodGuard;
        private readonly InvoiceMatchingService _matching;
        private readonly IOutboxWriter _outbox;
        private readonly IIdempotencyMediator _idempotency;
        private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
        private readonly ILogger<ApPostingService> _logger;

        public ApPostingService(
            AppDbContext db,
            ITenantContext tenantContext,
            IGlAccountResolver glResolver,
            IPeriodGuard periodGuard,
            InvoiceMatchingService matching,
            IOutboxWriter outbox,
            IIdempotencyMediator idempotency,
            Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
            ILogger<ApPostingService> logger)
        {
            _db = db;
            _tenantContext = tenantContext;
            _glResolver = glResolver;
            _periodGuard = periodGuard;
            _matching = matching;
            _outbox = outbox;
            _idempotency = idempotency;
            _chainOfCustody = chainOfCustody;
            _logger = logger;
        }

        // ADR-025 D2 — IPostingService<ApInvoiceApprovalRequest> implementation.
        //
        // Sprint 12.9 PR #2 — wraps the legacy PostApprovalAsync(int, bool, string)
        // entry point in the canonical contract: idempotency-keyed, Result-enveloped,
        // CancellationToken-aware. Existing call sites continue to use the legacy
        // method; new code paths (Razor pages refactored under Sprint 12.9 PR #4,
        // Sprint 13 Purchasing Control Center, voice MCP tool layer) should call
        // PostAsync.
        //
        // The IIdempotencyMediator wrap means same (actorUserId, idempotencyKey)
        // returns the cached PostingReceipt from the prior call. Same key with a
        // different request payload (e.g. flipping OverrideMatch) returns a
        // 409-style failure inside the Result envelope.
        //
        // Expected failures (invoice not found, period closed, match exception
        // without override) get caught and converted to Result.Failure for the
        // service-method-first action surface (ADR-014 D2). Unexpected failures
        // continue to throw — those are platform errors the caller should treat
        // as unrecoverable.
        async Task<Result<PostingReceipt>> IPostingService<ApInvoiceApprovalRequest>.PostAsync(
            ApInvoiceApprovalRequest source,
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
                        var legacy = await PostApprovalAsync(
                            source.InvoiceId,
                            source.OverrideMatch,
                            source.ApproverUsername);

                        // Map ApPostingResult → PostingReceipt envelope. LinesPosted
                        // and TotalDebits/TotalCredits aren't tracked on the legacy
                        // result type; receipt callers that need full fidelity should
                        // load the JournalEntry by Id from PostingReceipt.JournalEntryId.
                        var receipt = new PostingReceipt(
                            JournalEntryId: legacy.JournalEntryId,
                            LinesPosted: 0,
                            TotalDebits: legacy.AmountPosted,
                            TotalCredits: legacy.AmountPosted,
                            WasReplay: false,
                            AuditEventId: null);

                        return Result.Success(receipt);
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Expected failure modes — convert to Result.Failure for the
                        // service-method-first surface.
                        return Result.Failure<PostingReceipt>(ex.Message);
                    }
                },
                ct);
        }

        public async Task<ApPostingResult> PostApprovalAsync(int invoiceId, bool overrideMatch = false, string approverUsername = "")
        {
            var invoice = await LoadInvoiceScopedAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found.");

            // Idempotency: existing approval JE means we've already posted.
            // Lookup by Reference (model has no FK on the invoice today).
            var approvalRef = $"AP-APR-{invoice.InvoiceNumber}";
            var existingJeId = await _db.JournalEntries
                .Where(j => j.Reference == approvalRef && j.Source == "AP")
                .Select(j => (int?)j.Id)
                .FirstOrDefaultAsync();
            if (invoice.Status == InvoiceStatus.Approved && existingJeId.HasValue)
            {
                return new ApPostingResult(invoiceId, existingJeId, invoice.MatchStatus, invoice.Total);
            }

            // Three-way match gate. EvaluateMatchAsync returns the status;
            // UpdateInvoiceMatchStatusAsync persists it (returns void).
            var match = await _matching.EvaluateMatchAsync(invoiceId);
            await _matching.UpdateInvoiceMatchStatusAsync(invoiceId);
            if (match == InvoiceMatchStatus.Exception && !overrideMatch)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoice.InvoiceNumber} has match exceptions. " +
                    $"Resolve the line discrepancies or post with overrideMatch=true and an admin audit trail.");
            }

            var invoiceCompanyId = invoice.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (invoiceCompanyId == 0)
                throw new InvalidOperationException($"Invoice {invoiceId} has no resolvable CompanyId.");

            var postingDate = invoice.ApprovedAt ?? DateTime.UtcNow;
            var periodCheck = await _periodGuard.CanPostAsync(invoiceCompanyId, postingDate);
            if (!periodCheck.IsAllowed)
                throw new InvalidOperationException(periodCheck.Reason ?? $"Posting period for {postingDate:yyyy-MM-dd} is closed.");

            // Build the JE: Dr GR-Accrued (matched-against-PO lines) + Dr
            // DirectExpense (manual lines) + Dr/Cr PPV / Cr AccountsPayable.
            var debitTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);
            // PRA-5c — DEF-008 dual-write: parallel dict mapping account-number
            // string → AccountingKeyId. Stamped on every JournalLine alongside
            // the legacy Account string. Try/catch in ResolveAccountAndKeyAsync
            // keeps the legacy flow working if AccountingKey resolution fails
            // (e.g. no GlAccount row for the resolved industry-default number).
            var accountToKeyId = new Dictionary<string, int?>(StringComparer.Ordinal);
            decimal ppvTotal = 0m; // positive = unfavorable (Dr PPV)
            decimal totalCredit = 0m;

            foreach (var line in invoice.Lines)
            {
                var lineAmount = line.LineTotal > 0 ? line.LineTotal : line.Quantity * line.UnitPrice;
                if (lineAmount <= 0) continue;

                totalCredit += lineAmount;

                GlAccountKind drKind;
                decimal drAmount = lineAmount;

                if (line.PurchaseOrderLineId.HasValue)
                {
                    drKind = GlAccountKind.GrAccrued;

                    // PPV calculation: difference between invoice unit cost and PO unit cost.
                    if (line.PurchaseOrderLine != null && line.Quantity > 0)
                    {
                        var poUnit = line.PurchaseOrderLine.UnitPrice;
                        var invUnit = line.UnitPrice;
                        var variance = (invUnit - poUnit) * line.Quantity;
                        if (variance != 0m)
                        {
                            // GR-Accrued was Dr'd at the PO unit cost during receipt.
                            // Drop the GR-Accrued debit by the PO-cost portion only;
                            // the variance lives in PPV.
                            drAmount = poUnit * line.Quantity;
                            ppvTotal += variance;
                        }
                    }
                }
                else
                {
                    // Manual line — direct expense (or asset cost; for now
                    // the resolver's DirectExpense default; per-line GlAccountId
                    // override could refine in a follow-up).
                    drKind = GlAccountKind.DirectExpense;
                }

                var ctx = new GlResolveContext(
                    PurchaseOrderLineId: line.PurchaseOrderLineId,
                    VendorInvoiceLineId: line.Id);
                var (drAccount, drKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, drKind, ctx, logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} line={line.Id}");
                debitTotals[drAccount] = debitTotals.GetValueOrDefault(drAccount, 0m) + drAmount;
                accountToKeyId[drAccount] = drKeyId;
            }

            // Apply PPV as either debit (unfavorable: invoice > PO) or credit (favorable).
            string? ppvAccount = null;
            int? ppvKeyId = null;
            if (ppvTotal != 0m)
            {
                (ppvAccount, ppvKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.PurchasePriceVariance, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} ppv");
                if (ppvTotal > 0m)
                {
                    debitTotals[ppvAccount] = debitTotals.GetValueOrDefault(ppvAccount, 0m) + ppvTotal;
                    accountToKeyId[ppvAccount] = ppvKeyId;
                }
                // else handled in the credit-line section below
            }

            // PR #102 (B-09): Tax + Freight from the invoice header. Pre-fix,
            // invoice.TaxAmount and invoice.ShippingAmount were stored on the
            // header but never debited — so totalCredit (sum of line totals)
            // was strictly less than invoice.Total (= subtotal + tax + freight).
            // The PR #84 balance guard refused to save such JEs entirely, which
            // meant every invoice with non-zero tax or freight failed at the
            // approval handler with a cryptic balance error. Adding explicit
            // DR lines for tax + freight and bumping totalCredit by the same
            // amount keeps the JE balanced AND posts the full liability to AP.
            // The tax account is conceptually "recoverable" for now; PR #130
            // (tax matrix) refines for jurisdictions and non-recoverable rules.
            if (invoice.TaxAmount > 0m)
            {
                var (taxAccount, taxKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.SalesTaxRecoverable, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} tax");
                debitTotals[taxAccount] = debitTotals.GetValueOrDefault(taxAccount, 0m) + invoice.TaxAmount;
                accountToKeyId[taxAccount] = taxKeyId;
                totalCredit += invoice.TaxAmount;
            }
            if (invoice.ShippingAmount > 0m)
            {
                var (freightAccount, freightKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.FreightExpense, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} freight");
                debitTotals[freightAccount] = debitTotals.GetValueOrDefault(freightAccount, 0m) + invoice.ShippingAmount;
                accountToKeyId[freightAccount] = freightKeyId;
                totalCredit += invoice.ShippingAmount;
            }

            var (apAccount, apKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.AccountsPayable, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} ap");

            var je = new JournalEntry
            {
                BookId = null, // AP approval JE is not book-scoped; the Book FK is nullable per the model.
                Batch = $"AP-APR-{invoice.InvoiceNumber}",
                Period = int.Parse(postingDate.ToString("yyyyMM")),
                PostingDate = postingDate.Date,
                Source = "AP",
                Reference = $"AP-APR-{invoice.InvoiceNumber}",
                Description = $"Vendor invoice approval — {invoice.InvoiceNumber}",
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
                    Description = $"AP {invoice.InvoiceNumber}",
                    Debit = amount,
                    Credit = 0m
                });
            }
            // Favorable PPV: credit balance.
            if (ppvAccount != null && ppvTotal < 0m)
            {
                je.Lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = ppvAccount,
                    AccountingKeyId = ppvKeyId,
                    Description = $"PPV (favorable) {invoice.InvoiceNumber}",
                    Debit = 0m,
                    Credit = -ppvTotal
                });
            }
            je.Lines.Add(new JournalLine
            {
                LineNo = lineNo,
                Account = apAccount,
                AccountingKeyId = apKeyId,
                Description = $"AP — Vendor {invoice.VendorId}",
                Debit = 0m,
                Credit = totalCredit
            });

            _db.JournalEntries.Add(je);
            invoice.Status = InvoiceStatus.Approved;
            invoice.ApprovedAt = postingDate;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "ApPostingService: approved invoice {InvNum} → JE {JeId}, total={Total}, PPV={PPV}",
                invoice.InvoiceNumber, je.Id, totalCredit, ppvTotal);

            await _outbox.EnqueueAsync(
                invoiceCompanyId,
                siteId: null,
                new InvoiceApprovedV1(
                    InvoiceId: invoice.Id,
                    InvoiceNumber: invoice.InvoiceNumber,
                    VendorId: invoice.VendorId,
                    CompanyId: invoiceCompanyId,
                    Currency: invoice.Currency,
                    Total: invoice.Total,
                    ApprovedAt: postingDate,
                    MatchStatus: match.ToString(),
                    JournalEntryId: je.Id,
                    ApproverUsername: string.IsNullOrWhiteSpace(approverUsername) ? null : approverUsername,
                    MatchOverride: overrideMatch),
                correlationId: $"ap-approve-{invoice.Id}"
            );

            // Sprint 12D PR #3.2 / ADR-022 §D5 — chain-of-custody graph emission.
            //
            // Closes the "Machine Event → General Ledger" chain at the financial
            // boundary. Voice query "what does this GL entry come from?" walks
            // BACKWARDS from the GlEntry node through POSTED_TO → Invoice → (Vendor,
            // PO via the receipt chain established in PR #3). Combined with the
            // PR #3 Receipt → PO chain and PR #3.1 WO → Improvement → Asset chain,
            // any GL line can now be narrated end-to-end.
            //
            // Edges:
            //   Invoice --POSTED_TO--> GlEntry        (financial closure)
            //   Invoice --SUPPLIED_BY--> Vendor       (who got paid)
            //
            // Failure isolation: try/catch + LogWarning; JE/outbox already
            // committed when this runs.
            try
            {
                await _chainOfCustody.RecordEdgeAsync(
                    new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                        FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Invoice,
                        FromEntityId: invoice.Id,
                        FromLabel:    invoice.InvoiceNumber ?? $"INV-{invoice.Id}",
                        ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.GlEntry,
                        ToEntityId:   je.Id,
                        ToLabel:      je.Reference ?? $"JE-{je.Id}",
                        EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.PostedTo));

                if (invoice.VendorId > 0)
                {
                    await _chainOfCustody.RecordEdgeAsync(
                        new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                            FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Invoice,
                            FromEntityId: invoice.Id,
                            FromLabel:    invoice.InvoiceNumber ?? $"INV-{invoice.Id}",
                            ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Vendor,
                            ToEntityId:   invoice.VendorId,
                            ToLabel:      $"Vendor-{invoice.VendorId}",
                            EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.SuppliedBy));
                }
            }
            catch (Exception chainEx)
            {
                _logger.LogWarning(chainEx,
                    "ApPostingService.PostApprovalAsync: chain-of-custody emit failed for invoice {InvoiceId} → JE {JeId}. JE + outbox already committed; chain can be rebuilt via backfill.",
                    invoice.Id, je.Id);
            }

            return new ApPostingResult(invoiceId, je.Id, match, totalCredit);
        }

        public async Task<ApPostingResult> PostPaymentAsync(int invoiceId, decimal amount, DateTime paymentDate, string? paymentReference = null)
        {
            var invoice = await LoadInvoiceScopedAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found.");

            if (invoice.Status != InvoiceStatus.Approved && invoice.Status != InvoiceStatus.Paid)
                throw new InvalidOperationException($"Invoice {invoice.InvoiceNumber} must be Approved before payment.");

            if (amount <= 0)
                throw new InvalidOperationException("Payment amount must be > 0.");

            var invoiceCompanyId = invoice.CompanyId ?? _tenantContext.CompanyId ?? 0;
            var periodCheck = await _periodGuard.CanPostAsync(invoiceCompanyId, paymentDate);
            if (!periodCheck.IsAllowed)
                throw new InvalidOperationException(periodCheck.Reason ?? $"Payment period for {paymentDate:yyyy-MM-dd} is closed.");

            var (apAccount, apKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.AccountsPayable, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} pmt-ap");
            var (cashAccount, cashKeyId) = await _glResolver.ResolveAccountAndKeyAsync(invoiceCompanyId, GlAccountKind.Cash, new GlResolveContext(), logger: _logger, logContext: $"invoice={invoice.InvoiceNumber} pmt-cash");

            var je = new JournalEntry
            {
                BookId = null, // AP payment JE is not book-scoped.
                Batch = $"AP-PMT-{invoice.InvoiceNumber}",
                Period = int.Parse(paymentDate.ToString("yyyyMM")),
                PostingDate = paymentDate.Date,
                Source = "AP",
                Reference = $"AP-PMT-{invoice.InvoiceNumber}-{paymentReference ?? paymentDate.Ticks.ToString()}",
                Description = $"Vendor invoice payment — {invoice.InvoiceNumber}",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>
                {
                    new JournalLine { LineNo = 1, Account = apAccount, AccountingKeyId = apKeyId, Description = $"AP — Vendor {invoice.VendorId}", Debit = amount, Credit = 0m },
                    new JournalLine { LineNo = 2, Account = cashAccount, AccountingKeyId = cashKeyId, Description = "Cash", Debit = 0m, Credit = amount }
                }
            };
            _db.JournalEntries.Add(je);

            invoice.AmountPaid += amount;
            var fullyPaid = invoice.AmountPaid >= invoice.Total;
            if (fullyPaid)
                invoice.Status = InvoiceStatus.Paid;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _outbox.EnqueueAsync(
                invoiceCompanyId,
                siteId: null,
                new InvoicePaidV1(
                    InvoiceId: invoice.Id,
                    InvoiceNumber: invoice.InvoiceNumber,
                    VendorId: invoice.VendorId,
                    CompanyId: invoiceCompanyId,
                    Currency: invoice.Currency,
                    AmountPaid: amount,
                    RunningTotalPaid: invoice.AmountPaid,
                    InvoiceTotal: invoice.Total,
                    PaymentDate: paymentDate.Date,
                    PaymentReference: paymentReference,
                    JournalEntryId: je.Id,
                    IsFullyPaid: fullyPaid),
                correlationId: $"ap-payment-{invoice.Id}-{je.Id}"
            );

            return new ApPostingResult(invoiceId, je.Id, invoice.MatchStatus, amount);
        }

        public async Task<ApPostingResult> PostVoidAsync(int invoiceId, string reason)
        {
            var invoice = await LoadInvoiceScopedAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found.");

            if (invoice.Status == InvoiceStatus.Voided)
                return new ApPostingResult(invoiceId, null, invoice.MatchStatus, 0m);

            var previousStatus = invoice.Status;
            var voidDate = DateTime.UtcNow;
            var invoiceCompanyId = invoice.CompanyId ?? _tenantContext.CompanyId ?? 0;
            var periodCheck = await _periodGuard.CanPostAsync(invoiceCompanyId, voidDate);
            if (!periodCheck.IsAllowed)
                throw new InvalidOperationException(periodCheck.Reason ?? "Cannot void: posting period closed.");

            // If the invoice was approved, post a contra JE that reverses the
            // approval JE. Otherwise just flip status (no GL impact yet).
            int? contraJeId = null;
            var approvalRef = $"AP-APR-{invoice.InvoiceNumber}";
            var original = invoice.Status == InvoiceStatus.Approved
                ? await _db.JournalEntries
                    .Include(j => j.Lines)
                    .FirstOrDefaultAsync(j => j.Reference == approvalRef && j.Source == "AP")
                : null;
            if (original != null)
            {
                var contra = new JournalEntry
                {
                    BookId = original.BookId,
                    Batch = $"AP-VOID-{invoice.InvoiceNumber}",
                    Period = int.Parse(voidDate.ToString("yyyyMM")),
                    PostingDate = voidDate.Date,
                    Source = "AP",
                    Reference = $"AP-VOID-{invoice.InvoiceNumber}",
                    Description = $"Void of {original.Reference}: {reason}",
                    CreatedUtc = DateTime.UtcNow,
                    Lines = original.Lines.Select((l, idx) => new JournalLine
                    {
                        LineNo = idx + 1,
                        Account = l.Account,
                        // PRA-5c — copy AccountingKeyId from the original line so
                        // the contra entry references the same AccountingKey row
                        // (segment-key continuity for reporting).
                        AccountingKeyId = l.AccountingKeyId,
                        Description = $"VOID: {l.Description}",
                        Debit = l.Credit,
                        Credit = l.Debit
                    }).ToList()
                };
                _db.JournalEntries.Add(contra);
                await _db.SaveChangesAsync();
                contraJeId = contra.Id;
            }

            invoice.Status = InvoiceStatus.Voided;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _outbox.EnqueueAsync(
                invoiceCompanyId,
                siteId: null,
                new InvoiceVoidedV1(
                    InvoiceId: invoice.Id,
                    InvoiceNumber: invoice.InvoiceNumber,
                    VendorId: invoice.VendorId,
                    CompanyId: invoiceCompanyId,
                    Currency: invoice.Currency,
                    Total: invoice.Total,
                    Reason: reason,
                    VoidedAt: voidDate,
                    ContraJournalEntryId: contraJeId,
                    PreviousStatus: previousStatus.ToString()),
                correlationId: $"ap-void-{invoice.Id}"
            );

            return new ApPostingResult(invoiceId, contraJeId, invoice.MatchStatus, 0m);
        }

        private async Task<VendorInvoice?> LoadInvoiceScopedAsync(int invoiceId)
        {
            return await _db.VendorInvoices
                .Include(i => i.Lines).ThenInclude(l => l.PurchaseOrderLine)
                .Include(i => i.Lines).ThenInclude(l => l.GoodsReceiptLine)
                .Where(i => i.Id == invoiceId
                    && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0))
                .FirstOrDefaultAsync();
        }

        // PRA-5c inline helper extracted to shared
        // Services/Posting/GlPostingHelpers.ResolveAccountAndKeyAsync in
        // PRA-5e.1. Call sites use the extension method directly:
        //   _glResolver.ResolveAccountAndKeyAsync(companyId, kind, ctx, _logger, "...")
    }
}
