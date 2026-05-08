using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Vendor invoice voided payload, V1. Emitted by
/// <c>Services/AccountsPayable/ApPostingService.PostVoidAsync</c>
/// after the (optional) contra-JE has been written. <see cref="ContraJournalEntryId"/>
/// is non-null only when the invoice was previously approved — voiding
/// a Draft invoice produces no GL impact.
/// </summary>
[DomainEvent("invoice.voided", version: 1)]
public sealed record InvoiceVoidedV1(
    int InvoiceId,
    string InvoiceNumber,
    int VendorId,
    int CompanyId,
    string Currency,
    decimal Total,
    string Reason,
    DateTime VoidedAt,
    int? ContraJournalEntryId,
    string PreviousStatus
) : IDomainEvent
{
    public string EventType => "invoice.voided";
    public int Version => 1;
    public string EntityType => "VendorInvoice";
    public string EntityId => InvoiceId.ToString();
}
