// B6 Foundation Sprint PR-FS-6 (2026-05-26) — ICustomerItemXrefService.
//
// Bidirectional customer-PN ↔ Item resolution. SAP CMIR equivalent.
//
// Per Lock 15 — IService surface only, never direct DbContext from callers.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Services.Items;

public interface ICustomerItemXrefService
{
    /// <summary>
    /// Resolve the customer's PN to our Item. Used at SO ingestion. Returns
    /// null if no matching xref exists. Filters to Active status only;
    /// Superseded / Obsolete are ignored.
    ///
    /// Match logic: exact (case-sensitive) match on (CustomerId,
    /// CustomerPartNumber). If <paramref name="customerRevision"/> is supplied,
    /// the match additionally requires the revision to match; if null, returns
    /// the current (no-revision-filter) Active xref.
    ///
    /// As-of date filters to rows whose
    /// (EffectiveFromUtc, EffectiveToUtc) bracket the supplied timestamp.
    /// Default = DateTime.UtcNow.
    /// </summary>
    Task<CustomerItemXref?> ResolveByCustomerPnAsync(
        int customerId,
        string customerPartNumber,
        string? customerRevision,
        DateTime? asOfUtc,
        CancellationToken ct);

    /// <summary>
    /// Resolve OUR Item to the customer's PN (for ship/invoice rendering).
    /// Returns the current Active xref for the (CustomerId, ItemId) pair.
    /// </summary>
    Task<CustomerItemXref?> ResolveByItemAsync(
        int customerId,
        int itemId,
        DateTime? asOfUtc,
        CancellationToken ct);

    /// <summary>
    /// Get all xrefs for an Item (across all Customers). Used by admin UI to
    /// answer "who else calls this part what?". Includes Active + Superseded
    /// by default; Obsolete excluded unless <paramref name="includeObsolete"/>.
    /// </summary>
    Task<IReadOnlyList<CustomerItemXref>> GetAllForItemAsync(
        int itemId,
        bool includeObsolete,
        CancellationToken ct);

    /// <summary>
    /// Get all xrefs for a Customer (across all Items). Used by admin UI to
    /// render the customer's "part catalog."
    /// </summary>
    Task<IReadOnlyList<CustomerItemXref>> GetAllForCustomerAsync(
        int customerId,
        bool includeObsolete,
        CancellationToken ct);

    /// <summary>
    /// Add a new xref. Validates non-empty CustomerPartNumber + service-side
    /// NULL-safe uniqueness on (TenantId, CustomerId, CustomerPartNumber,
    /// CustomerRevision) for active rows. Idempotent: if an active xref with
    /// the same (CustomerId, CustomerPartNumber, CustomerRevision) already
    /// exists pointing to the same ItemId, returns it without writing.
    /// Throws if it points to a different ItemId (caller must supersede + add).
    /// </summary>
    Task<CustomerItemXref> AddXrefAsync(
        int itemId,
        int customerId,
        string customerPartNumber,
        string? customerRevision,
        string? customerPartDescription,
        string? customerDrawingNumber,
        string? customerDrawingRevision,
        string? customerSpecificationNumber,
        string? customerEcoNumber,
        string? notes,
        string? createdBy,
        CancellationToken ct);

    /// <summary>
    /// Supersede an existing xref with a new revision. Flips the old row to
    /// Status=Superseded + stamps SupersededByXrefId + EffectiveToUtc.
    /// Inserts a new Active xref pointing to the same ItemId at the new
    /// revision. Concurrency-safe (RowVersion + retry).
    /// </summary>
    Task<CustomerItemXref> SupersedeAsync(
        int existingXrefId,
        string? newCustomerRevision,
        string? newCustomerEcoNumber,
        string? newCustomerDrawingRevision,
        string? notes,
        string? supersededBy,
        CancellationToken ct);

    /// <summary>
    /// Mark an xref Obsolete (customer EOL). Concurrency-safe.
    /// </summary>
    Task<CustomerItemXref> ObsoleteAsync(int xrefId, string reason, string by, CancellationToken ct);
}
