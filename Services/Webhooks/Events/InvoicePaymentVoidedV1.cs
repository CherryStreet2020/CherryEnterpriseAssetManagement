using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Vendor invoice payment voided payload, V1. Emitted by
/// <c>Services/AccountsPayable/ApPostingService.VoidPaymentAsync</c> after
/// the contra-payment JE (Dr Cash / Cr AP — reversing the original
/// PostPayment JE) has been written and the InvoicePayment row marked
/// IsVoided. Distinct from <see cref="InvoiceVoidedV1"/> which covers
/// voiding the entire invoice; this event scopes to a single payment.
/// </summary>
[DomainEvent("invoice.payment_voided", version: 1)]
public sealed record InvoicePaymentVoidedV1(
    int InvoiceId,
    string InvoiceNumber,
    int PaymentId,
    int VendorId,
    int CompanyId,
    string Currency,
    decimal PaymentAmount,
    decimal RemainingAmountPaid,
    decimal InvoiceTotal,
    DateTime VoidedAt,
    string Reason,
    int? ContraJournalEntryId
) : IDomainEvent
{
    public string EventType => "invoice.payment_voided";
    public int Version => 1;
    public string EntityType => "InvoicePayment";
    public string EntityId => PaymentId.ToString();
}
