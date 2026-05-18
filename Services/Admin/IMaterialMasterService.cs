using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Admin;

// Sprint 4 Phase F Wave 1 — MaterialMaster admin service.
// Same shape as IRegulatoryProfileService (PR #216). Result<T> +
// idempotent + voice-ready.
public interface IMaterialMasterService
{
    Task<Result<IReadOnlyList<MaterialMaster>>> ListAsync(CancellationToken ct);

    Task<Result<MaterialMaster>> GetAsync(int id, CancellationToken ct);

    Task<Result<MaterialMaster>> CreateAsync(
        CreateMaterialMasterRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);

    Task<Result<MaterialMaster>> UpdateAsync(
        int id,
        UpdateMaterialMasterRequest request,
        int actorUserId,
        Guid? idempotencyKey,
        CancellationToken ct);
}

public sealed record CreateMaterialMasterRequest(
    string ShopCode,
    string? AstmDesignation,
    string? Description,
    MaterialForm Form,
    decimal? DensityKgPerM3,
    bool IsAnisotropic);

public sealed record UpdateMaterialMasterRequest(
    string ShopCode,
    string? AstmDesignation,
    string? Description,
    MaterialForm Form,
    decimal? DensityKgPerM3,
    bool IsAnisotropic);
