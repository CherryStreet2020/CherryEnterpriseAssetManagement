using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 PR #4 — Vendor edit service.
//
// Result<T> + IdempotencyMediator + AuditLog, same pattern as
// IRegulatoryProfileService (PR #216) and IMaterialMasterService
// (PR #217). The Razor page handler and the future voice-AI MCP
// tool layer call the same methods.
public interface IVendorService
{
    Task<Result<Vendor>> GetAsync(int id, CancellationToken ct);

    Task<Result<Vendor>> UpdateInfoAsync(
        int id,
        UpdateVendorInfoRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<Vendor>> UpdateContactAsync(
        int id,
        UpdateVendorContactRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<Vendor>> DuplicateAsync(
        int sourceId,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<bool>> ToggleActiveAsync(
        int id,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);
}

public sealed record UpdateVendorInfoRequest(
    string Code,
    string Name,
    string? LegalName,
    VendorType VendorType,
    PaymentTerms PaymentTerms,
    string? TaxId,
    decimal? CreditLimit,
    bool Is1099Vendor,
    bool IsPreferred,
    string? AccountNumber,
    string? Currency,
    string? Notes,
    bool IsActive);

public sealed record UpdateVendorContactRequest(
    string? ContactName,
    string? Phone,
    string? Fax,
    string? Email,
    string? Website,
    string? Address,
    string? City,
    string? State,
    string? PostalCode,
    string? Country);
