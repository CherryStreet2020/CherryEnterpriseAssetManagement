// Sprint 13.5 PRA-4 — IUomService + UomService.
//
// The unified UOM conversion service. Every domain that converts between
// units (Inventory, Production, Sales, Purchasing, Telemetry rollups) calls
// this service — no more ad-hoc conversion code scattered across services.
//
// CONVERSION ALGORITHM:
//
//   STEP 1 — Try a per-item override:
//     SELECT FROM UomConversions
//     WHERE CompanyId = tenant
//       AND FromUomId = from AND ToUomId = to
//       AND ItemId = item
//
//   STEP 2 — Try a company-wide cross-category override:
//     SELECT FROM UomConversions
//     WHERE CompanyId = tenant
//       AND FromUomId = from AND ToUomId = to
//       AND ItemId IS NULL
//
//   STEP 3 — Try in-category affine conversion via base unit:
//     If From.UomCategoryId == To.UomCategoryId:
//       base_value = From.Factor * value + From.Offset
//       result     = (base_value - To.Offset) / To.Factor
//
//   STEP 4 — Failure (no conversion path).
//
// TENANT SCOPING: per-item + company-wide overrides are tenant-scoped via
// ITenantContext. System UOMs (CompanyId IS NULL) are visible to all tenants
// for in-category conversion (Step 3).
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §5
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Masters;

public interface IUomService
{
    /// <summary>Convert a value from one UOM to another. ItemId optional for per-item overrides.</summary>
    Task<Result<decimal>> ConvertAsync(
        decimal value,
        int fromUomId,
        int toUomId,
        int? itemId,
        CancellationToken ct);

    /// <summary>Pull a UOM row by Id (cached read).</summary>
    Task<UnitOfMeasureMaster?> GetUomAsync(int uomId, CancellationToken ct);

    /// <summary>Pull all UOMs in a category (for dropdowns).</summary>
    Task<IReadOnlyList<UnitOfMeasureMaster>> GetByCategoryAsync(int uomCategoryId, CancellationToken ct);
}

public sealed class UomService : IUomService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UomService> _logger;

    public UomService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<UomService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<decimal>> ConvertAsync(
        decimal value,
        int fromUomId,
        int toUomId,
        int? itemId,
        CancellationToken ct)
    {
        if (fromUomId == toUomId)
            return Result.Success(value);

        // Pull both UOM rows.
        var uoms = await _db.UnitsOfMeasure
            .Where(u => u.Id == fromUomId || u.Id == toUomId)
            .ToListAsync(ct);
        var from = uoms.FirstOrDefault(u => u.Id == fromUomId);
        var to = uoms.FirstOrDefault(u => u.Id == toUomId);
        if (from is null)
            return Result.Failure<decimal>($"UOM {fromUomId} not found.");
        if (to is null)
            return Result.Failure<decimal>($"UOM {toUomId} not found.");

        var visibleCos = _tenantContext.VisibleCompanyIds;
        if (visibleCos.Count == 0)
            return Result.Failure<decimal>("No tenant scope available for UOM conversion.");

        // STEP 1 — per-item override.
        if (itemId.HasValue)
        {
            var perItem = await _db.UomConversions
                .Where(c => c.IsActive
                         && visibleCos.Contains(c.CompanyId)
                         && c.FromUomId == fromUomId
                         && c.ToUomId == toUomId
                         && c.ItemId == itemId.Value)
                .Select(c => new { c.Multiplier, c.Offset })
                .FirstOrDefaultAsync(ct);
            if (perItem is not null)
                return Result.Success(perItem.Multiplier * value + perItem.Offset);
        }

        // STEP 2 — company-wide cross-category override.
        var companyWide = await _db.UomConversions
            .Where(c => c.IsActive
                     && visibleCos.Contains(c.CompanyId)
                     && c.FromUomId == fromUomId
                     && c.ToUomId == toUomId
                     && c.ItemId == null)
            .Select(c => new { c.Multiplier, c.Offset })
            .FirstOrDefaultAsync(ct);
        if (companyWide is not null)
            return Result.Success(companyWide.Multiplier * value + companyWide.Offset);

        // STEP 3 — in-category affine via base unit.
        if (from.UomCategoryId == to.UomCategoryId)
        {
            // base_value = From.Factor * value + From.Offset
            // result     = (base_value - To.Offset) / To.Factor
            if (to.ConversionFactorToBase == 0m)
                return Result.Failure<decimal>(
                    $"UOM {to.Code} has zero conversion factor; cannot convert into it.");
            var baseValue = from.ConversionFactorToBase * value + from.ConversionOffsetToBase;
            var result = (baseValue - to.ConversionOffsetToBase) / to.ConversionFactorToBase;
            return Result.Success(result);
        }

        // STEP 4 — failure.
        return Result.Failure<decimal>(
            $"No conversion defined between UOM {from.Code} ({from.UomCategoryId}) " +
            $"and UOM {to.Code} ({to.UomCategoryId}). " +
            "Add a UomConversion row (company-wide or per-item) to enable.");
    }

    public async Task<UnitOfMeasureMaster?> GetUomAsync(int uomId, CancellationToken ct)
    {
        return await _db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == uomId, ct);
    }

    public async Task<IReadOnlyList<UnitOfMeasureMaster>> GetByCategoryAsync(
        int uomCategoryId,
        CancellationToken ct)
    {
        return await _db.UnitsOfMeasure
            .Where(u => u.UomCategoryId == uomCategoryId && u.IsActive)
            .OrderBy(u => u.SortOrder)
            .ThenBy(u => u.Code)
            .ToListAsync(ct);
    }
}
