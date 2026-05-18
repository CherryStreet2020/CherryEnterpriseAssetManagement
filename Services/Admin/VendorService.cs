using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #4 — Vendor edit service implementation.
//
// Same Result<T> + audit + idempotency pattern as RegulatoryProfile
// (PR #216) and MaterialMaster (PR #217). Tenant-scoped via
// ITenantContext so VendorService.UpdateInfoAsync() respects the
// same VisibleCompanyIds boundary the legacy page enforced.
public sealed class VendorService : IVendorService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VendorService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator _idempotency;

    public VendorService(
        AppDbContext db,
        ILogger<VendorService> logger,
        ITenantContext tenantContext,
        Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator idempotency)
    {
        _db = db;
        _logger = logger;
        _tenantContext = tenantContext;
        _idempotency = idempotency;
    }

    public async Task<Result<Vendor>> GetAsync(int id, CancellationToken ct)
    {
        var vendor = await LoadAsync(id, ct);
        return vendor is null
            ? Result.Failure<Vendor>($"Vendor {id} not found")
            : Result.Success(vendor);
    }

    public async Task<Result<Vendor>> UpdateInfoAsync(
        int id,
        UpdateVendorInfoRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result.Failure<Vendor>("Vendor Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<Vendor>("Vendor Name is required");

        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var vendor = await LoadAsync(id, innerCt);
                if (vendor is null)
                    return Result.Failure<Vendor>($"Vendor {id} not found");

                // Code uniqueness if changed
                if (!string.Equals(vendor.Code, request.Code, StringComparison.OrdinalIgnoreCase))
                {
                    var dup = await _db.Vendors.AnyAsync(
                        v => v.Id != id && v.Code == request.Code, innerCt);
                    if (dup) return Result.Failure<Vendor>($"Vendor Code '{request.Code}' already exists");
                }

                var before = SnapshotInfo(vendor);

                vendor.Code = request.Code.Trim();
                vendor.Name = request.Name.Trim();
                vendor.LegalName = request.LegalName?.Trim();
                vendor.VendorType = request.VendorType;
                vendor.PaymentTerms = request.PaymentTerms;
                vendor.TaxId = request.TaxId?.Trim();
                vendor.CreditLimit = request.CreditLimit;
                vendor.Is1099Vendor = request.Is1099Vendor;
                vendor.IsPreferred = request.IsPreferred;
                vendor.AccountNumber = request.AccountNumber?.Trim();
                vendor.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim();
                vendor.Notes = request.Notes?.Trim();
                vendor.IsActive = request.IsActive;
                vendor.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(vendor.Id, "UpdateInfo",
                    JsonSerializer.Serialize(before),
                    JsonSerializer.Serialize(SnapshotInfo(vendor)),
                    actorUserId,
                    $"Updated Vendor '{vendor.Code}' info",
                    innerCt);

                return Result.Success(vendor);
            },
            ct);
    }

    public async Task<Result<Vendor>> UpdateContactAsync(
        int id,
        UpdateVendorContactRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            request,
            async innerCt =>
            {
                var vendor = await LoadAsync(id, innerCt);
                if (vendor is null)
                    return Result.Failure<Vendor>($"Vendor {id} not found");

                var before = SnapshotContact(vendor);

                vendor.ContactName = request.ContactName?.Trim();
                vendor.Phone = request.Phone?.Trim();
                vendor.Fax = request.Fax?.Trim();
                vendor.Email = request.Email?.Trim();
                vendor.Website = request.Website?.Trim();
                vendor.Address = request.Address?.Trim();
                vendor.City = request.City?.Trim();
                vendor.State = request.State?.Trim();
                vendor.PostalCode = request.PostalCode?.Trim();
                vendor.Country = request.Country?.Trim();
                vendor.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(vendor.Id, "UpdateContact",
                    JsonSerializer.Serialize(before),
                    JsonSerializer.Serialize(SnapshotContact(vendor)),
                    actorUserId,
                    $"Updated Vendor '{vendor.Code}' contact details",
                    innerCt);

                return Result.Success(vendor);
            },
            ct);
    }

    public async Task<Result<Vendor>> DuplicateAsync(
        int sourceId,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            new { sourceId },
            async innerCt =>
            {
                var source = await LoadAsync(sourceId, innerCt);
                if (source is null)
                    return Result.Failure<Vendor>($"Vendor {sourceId} not found");

                var newCode = $"{source.Code}-COPY";
                var counter = 1;
                while (await _db.Vendors.AnyAsync(v => v.Code == newCode, innerCt))
                {
                    newCode = $"{source.Code}-COPY{counter++}";
                }

                var maxSortOrder = await _db.Vendors.MaxAsync(v => (int?)v.SortOrder, innerCt) ?? 0;

                var newVendor = new Vendor
                {
                    Code = newCode,
                    Name = $"{source.Name} (Copy)",
                    LegalName = source.LegalName,
                    VendorType = source.VendorType,
                    PaymentTerms = source.PaymentTerms,
                    TaxId = source.TaxId,
                    CreditLimit = source.CreditLimit,
                    Is1099Vendor = source.Is1099Vendor,
                    IsPreferred = false, // duplicated vendor must explicitly become preferred
                    ContactName = source.ContactName,
                    Phone = source.Phone,
                    Fax = source.Fax,
                    Email = source.Email,
                    Website = source.Website,
                    Address = source.Address,
                    City = source.City,
                    State = source.State,
                    PostalCode = source.PostalCode,
                    Country = source.Country,
                    Notes = $"Duplicated from {source.Code}",
                    AccountNumber = source.AccountNumber,
                    Currency = source.Currency,
                    IsActive = true,
                    SortOrder = maxSortOrder + 10,
                    CompanyId = _tenantContext.CompanyId,
                };

                _db.Vendors.Add(newVendor);
                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(newVendor.Id, "Duplicate",
                    beforeJson: null,
                    afterJson: JsonSerializer.Serialize(new { newVendor.Code, newVendor.Name, SourceVendorId = source.Id, SourceCode = source.Code }),
                    actorUserId,
                    $"Duplicated Vendor '{source.Code}' → '{newCode}'",
                    innerCt);

                return Result.Success(newVendor);
            },
            ct);
    }

    public async Task<Result<bool>> ToggleActiveAsync(
        int id,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        return await _idempotency.ExecuteAsync(
            actorUserId,
            idempotencyKey ?? Guid.Empty,
            new { id, action = "toggle" },
            async innerCt =>
            {
                var vendor = await LoadAsync(id, innerCt);
                if (vendor is null)
                    return Result.Failure<bool>($"Vendor {id} not found");

                var previous = vendor.IsActive;
                vendor.IsActive = !vendor.IsActive;
                vendor.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(innerCt);

                await WriteAuditAsync(vendor.Id, vendor.IsActive ? "Activate" : "Deactivate",
                    JsonSerializer.Serialize(new { IsActive = previous }),
                    JsonSerializer.Serialize(new { vendor.IsActive }),
                    actorUserId,
                    $"{(vendor.IsActive ? "Activated" : "Deactivated")} Vendor '{vendor.Code}'",
                    innerCt);

                return Result.Success(true);
            },
            ct);
    }

    // ----- helpers -----

    private async Task<Vendor?> LoadAsync(int id, CancellationToken ct)
    {
        return await _db.Vendors
            .Where(v => v.Id == id &&
                (v.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0)))
            .FirstOrDefaultAsync(ct);
    }

    private static object SnapshotInfo(Vendor v) => new
    {
        v.Code, v.Name, v.LegalName,
        VendorType = v.VendorType.ToString(),
        PaymentTerms = v.PaymentTerms.ToString(),
        v.TaxId, v.CreditLimit, v.Is1099Vendor, v.IsPreferred,
        v.AccountNumber, v.Currency, v.Notes, v.IsActive,
    };

    private static object SnapshotContact(Vendor v) => new
    {
        v.ContactName, v.Phone, v.Fax, v.Email, v.Website,
        v.Address, v.City, v.State, v.PostalCode, v.Country,
    };

    private async Task WriteAuditAsync(
        int entityId, string action, string? beforeJson, string? afterJson,
        int actorUserId, string description, CancellationToken ct)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = nameof(Vendor),
                EntityId = entityId,
                Action = action,
                BeforeJson = beforeJson,
                AfterJson = afterJson,
                Username = actorUserId.ToString(),
                Timestamp = DateTime.UtcNow,
                Description = description,
                ActorKind = ActorKind.User,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log write failed for Vendor {Id} {Action}", entityId, action);
        }
    }
}
