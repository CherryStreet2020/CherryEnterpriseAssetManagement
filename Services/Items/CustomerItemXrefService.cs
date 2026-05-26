// B6 Foundation Sprint PR-FS-6 (2026-05-26) — CustomerItemXrefService impl.
//
// Bidirectional customer-PN ↔ Item resolution. Concurrency-safe (RowVersion +
// retry). Service-side NULL-safe uniqueness check on add.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class CustomerItemXrefService : ICustomerItemXrefService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CustomerItemXrefService> _logger;

    public CustomerItemXrefService(AppDbContext db, ILogger<CustomerItemXrefService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerItemXref?> ResolveByCustomerPnAsync(
        int customerId,
        string customerPartNumber,
        string? customerRevision,
        DateTime? asOfUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerPartNumber))
            throw new ArgumentException("CustomerPartNumber is required.", nameof(customerPartNumber));

        var asOf = asOfUtc ?? DateTime.UtcNow;
        var q = _db.CustomerItemXrefs.AsNoTracking()
            .Where(x => x.CustomerId == customerId
                     && x.CustomerPartNumber == customerPartNumber
                     && x.Status == CustomerXrefStatus.Active
                     && x.IsActive
                     && x.EffectiveFromUtc <= asOf
                     && (x.EffectiveToUtc == null || x.EffectiveToUtc > asOf));
        if (customerRevision != null)
        {
            q = q.Where(x => x.CustomerRevision == customerRevision);
        }
        return await q.OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerItemXref?> ResolveByItemAsync(
        int customerId,
        int itemId,
        DateTime? asOfUtc,
        CancellationToken ct)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        return await _db.CustomerItemXrefs.AsNoTracking()
            .Where(x => x.CustomerId == customerId
                     && x.ItemId == itemId
                     && x.Status == CustomerXrefStatus.Active
                     && x.IsActive
                     && x.EffectiveFromUtc <= asOf
                     && (x.EffectiveToUtc == null || x.EffectiveToUtc > asOf))
            .OrderByDescending(x => x.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerItemXref>> GetAllForItemAsync(
        int itemId,
        bool includeObsolete,
        CancellationToken ct)
    {
        var q = _db.CustomerItemXrefs.AsNoTracking().Where(x => x.ItemId == itemId);
        if (!includeObsolete)
        {
            q = q.Where(x => x.Status != CustomerXrefStatus.Obsolete);
        }
        return await q
            .OrderBy(x => x.CustomerId)
            .ThenBy(x => x.Status)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerItemXref>> GetAllForCustomerAsync(
        int customerId,
        bool includeObsolete,
        CancellationToken ct)
    {
        var q = _db.CustomerItemXrefs.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (!includeObsolete)
        {
            q = q.Where(x => x.Status != CustomerXrefStatus.Obsolete);
        }
        return await q
            .OrderBy(x => x.CustomerPartNumber)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .ToListAsync(ct);
    }

    public async Task<CustomerItemXref> AddXrefAsync(
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
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerPartNumber))
            throw new ArgumentException("CustomerPartNumber is required.", nameof(customerPartNumber));

        // Service-side NULL-safe uniqueness check (PR-FS-5 lesson applied).
        // Postgres partial UNIQUE can't catch CustomerRevision=NULL duplicates;
        // EF's `==` on nullables translates to IS NULL / = correctly.
        var existing = await _db.CustomerItemXrefs
            .FirstOrDefaultAsync(x => x.CustomerId == customerId
                                   && x.CustomerPartNumber == customerPartNumber
                                   && x.CustomerRevision == customerRevision
                                   && x.IsActive
                                   && x.Status == CustomerXrefStatus.Active, ct);
        if (existing != null)
        {
            if (existing.ItemId == itemId)
            {
                _logger.LogInformation(
                    "CustomerItemXrefService: idempotent no-op — xref {XrefId} already exists for Customer {CustomerId} PN {PartNumber} Rev {Rev} → Item {ItemId}.",
                    existing.Id, customerId, customerPartNumber, customerRevision ?? "<null>", itemId);
                return existing;
            }
            throw new InvalidOperationException(
                $"Active xref already exists for Customer {customerId} PN '{customerPartNumber}' Rev '{customerRevision ?? "<null>"}' " +
                $"pointing to Item {existing.ItemId}, but caller supplied Item {itemId}. " +
                $"Use SupersedeAsync to replace the existing xref + create a new revision.");
        }

        var now = DateTime.UtcNow;
        var xref = new CustomerItemXref
        {
            ItemId = itemId,
            CustomerId = customerId,
            CustomerPartNumber = customerPartNumber,
            CustomerRevision = customerRevision,
            CustomerPartDescription = customerPartDescription,
            CustomerDrawingNumber = customerDrawingNumber,
            CustomerDrawingRevision = customerDrawingRevision,
            CustomerSpecificationNumber = customerSpecificationNumber,
            CustomerEcoNumber = customerEcoNumber,
            Status = CustomerXrefStatus.Active,
            IsActive = true,
            EffectiveFromUtc = now,
            EffectiveToUtc = null,
            Notes = notes,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.CustomerItemXrefs.Add(xref);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CustomerItemXrefService: added xref {XrefId} Customer {CustomerId} PN '{PartNumber}' Rev '{Rev}' → Item {ItemId}.",
            xref.Id, customerId, customerPartNumber, customerRevision ?? "<null>", itemId);

        return xref;
    }

    public async Task<CustomerItemXref> SupersedeAsync(
        int existingXrefId,
        string? newCustomerRevision,
        string? newCustomerEcoNumber,
        string? newCustomerDrawingRevision,
        string? notes,
        string? supersededBy,
        CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var existing = await _db.CustomerItemXrefs.FirstOrDefaultAsync(x => x.Id == existingXrefId, ct)
                    ?? throw new InvalidOperationException($"CustomerItemXref {existingXrefId} not found.");
                if (existing.Status != CustomerXrefStatus.Active)
                {
                    throw new InvalidOperationException(
                        $"CustomerItemXref {existingXrefId} is not Active (status={existing.Status}). " +
                        $"Only Active xrefs can be superseded.");
                }

                // PR-FS-6 P1 fix (Codex on PR #362): atomic supersede — both
                // writes (new insert + old row flip) commit in a single
                // SaveChangesAsync so a concurrency conflict on the flip
                // doesn't leave the new insert orphaned. EF resolves the
                // new row's auto-gen Id and stamps it on existing's
                // SupersededByXref FK as part of the same transaction.
                var now = DateTime.UtcNow;
                var newXref = new CustomerItemXref
                {
                    ItemId = existing.ItemId,
                    CustomerId = existing.CustomerId,
                    CustomerPartNumber = existing.CustomerPartNumber,
                    CustomerRevision = newCustomerRevision,
                    CustomerPartDescription = existing.CustomerPartDescription,
                    CustomerDrawingNumber = existing.CustomerDrawingNumber,
                    CustomerDrawingRevision = newCustomerDrawingRevision ?? existing.CustomerDrawingRevision,
                    CustomerSpecificationNumber = existing.CustomerSpecificationNumber,
                    CustomerEcoNumber = newCustomerEcoNumber,
                    Status = CustomerXrefStatus.Active,
                    IsActive = true,
                    EffectiveFromUtc = now,
                    EffectiveToUtc = null,
                    Notes = notes,
                    CreatedAt = now,
                    CreatedBy = supersededBy,
                };
                _db.CustomerItemXrefs.Add(newXref);

                existing.Status = CustomerXrefStatus.Superseded;
                existing.SupersededByXref = newXref;  // nav, not raw FK — EF fixes up the Id at SaveChanges
                existing.EffectiveToUtc = now;
                existing.UpdatedAt = now;
                existing.UpdatedBy = supersededBy;

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "CustomerItemXrefService: superseded xref {OldId} (Customer {CustId} PN '{PN}') → new xref {NewId} Rev '{NewRev}'.",
                    existingXrefId, existing.CustomerId, existing.CustomerPartNumber, newXref.Id, newCustomerRevision ?? "<null>");
                return newXref;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "CustomerItemXrefService: concurrency conflict on Supersede xref {XrefId} attempt {Attempt}/{Max} — retrying.",
                    existingXrefId, attempt, maxRetries);
                foreach (var entry in _db.ChangeTracker.Entries<CustomerItemXref>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    public async Task<CustomerItemXref> ObsoleteAsync(int xrefId, string reason, string by, CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var xref = await _db.CustomerItemXrefs.FirstOrDefaultAsync(x => x.Id == xrefId, ct)
                    ?? throw new InvalidOperationException($"CustomerItemXref {xrefId} not found.");

                var now = DateTime.UtcNow;
                xref.Status = CustomerXrefStatus.Obsolete;
                xref.IsActive = false;
                xref.EffectiveToUtc = now;
                xref.Notes = string.IsNullOrEmpty(xref.Notes)
                    ? $"Obsoleted: {reason}"
                    : $"{xref.Notes}\n\nObsoleted: {reason}";
                xref.UpdatedAt = now;
                xref.UpdatedBy = by;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "CustomerItemXrefService: obsoleted xref {XrefId} reason='{Reason}' by {By}.",
                    xrefId, reason, by);
                return xref;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "CustomerItemXrefService: concurrency conflict on Obsolete xref {XrefId} attempt {Attempt}/{Max} — retrying.",
                    xrefId, attempt, maxRetries);
                foreach (var entry in _db.ChangeTracker.Entries<CustomerItemXref>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
