using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Vendor invoice payment posted payload, V1. Emitted by
/// <c>Services/AccountsPayable/ApPostingService.PostPaymentAsync</c>
/// after the payment JE (Dr AP / Cr Cash) has been written. Fires once
/// per payment; <see cref="IsFullyPaid"/> distinguishes the closing
/// payment from partial payments so consumers can trigger payable-
/// closing workflows on the right one.
/// </summary>
[DomainEvent("invoice.paid", version: 1)]
public sealed record InvoicePaidV1(
    int InvoiceId,
    string InvoiceNumber,
    int VendorId,
    int CompanyId,
    string Currency,
    decimal AmountPaid,
    decimal RunningTotalPaid,
    decimal InvoiceTotal,
    DateTime PaymentDate,
    string? PaymentReference,
    int? JournalEntryId,
    bool IsFullyPaid
) : IDomainEvent
{
    public string EventType => "invoice.paid";
    public int Version => 1;
    public string EntityType => "VendorInvoice";
    public string EntityId => InvoiceId.ToString();
}
