using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Vendor invoice approved payload, V1. Emitted by
/// <c>Services/AccountsPayable/ApPostingService.PostApprovalAsync</c>
/// after the AP approval JE (Dr GR-Accrued/DirectExpense + PPV /
/// Cr AccountsPayable) has been written and the invoice flips to
/// <c>Approved</c>. Per ADR-002 this is the canonical "AP recognized"
/// signal — partners should use it to drive payable workflows.
/// </summary>
[DomainEvent("invoice.approved", version: 1)]
public sealed record InvoiceApprovedV1(
    int InvoiceId,
    string InvoiceNumber,
    int VendorId,
    int CompanyId,
    string Currency,
    decimal Total,
    DateTime ApprovedAt,
    string MatchStatus,
    int? JournalEntryId,
    string? ApproverUsername,
    bool MatchOverride
) : IDomainEvent
{
    public string EventType => "invoice.approved";
    public int Version => 1;
    public string EntityType => "VendorInvoice";
    public string EntityId => InvoiceId.ToString();
}
