// Sprint 13.5 PR #5c — IWorkCenterService + WorkCenterService impl.
//
// First mutation surface for WorkCenter (PR #5c entity). v1 minimum to make
// the admin Index/Create/Edit pages work. Asset linking is in-scope (one of
// the BIC differentiators is live machine state rolling up to the WC card).
//
// Methods (4):
//   1. CreateAsync       — new WC in Active status with calendar/location/etc.
//   2. UpdateHeaderAsync — editable fields (name/description/cost rates/calendar)
//   3. UpdateStatusAsync — Active <-> Inactive <-> Maintenance <-> Retired
//   4. LinkAssetAsync    — add an Asset to a WC (primary or secondary)
//
// PR #5c.1 will add: UnlinkAssetAsync, SetPrimaryAssetAsync, calendar override.
//
// Tenancy: WorkCenter has a direct CompanyId (no Location/Customer fallback
// needed). Service rejects CompanyId values not in ITenantContext.VisibleCompanyIds.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.ChainOfCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public interface IWorkCenterService
{
    Task<Result<WorkCenter>> CreateAsync(CreateWorkCenterRequest request, CancellationToken ct);
    Task<Result<WorkCenter>> UpdateHeaderAsync(UpdateWorkCenterHeaderRequest request, CancellationToken ct);
    Task<Result<WorkCenter>> UpdateStatusAsync(UpdateWorkCenterStatusRequest request, CancellationToken ct);
    Task<Result<WorkCenterAssetLink>> LinkAssetAsync(LinkAssetRequest request, CancellationToken ct);
}

public sealed record CreateWorkCenterRequest(
    int CompanyId,
    int LocationId,           // PR #5c.1: REQUIRED — every WC physically lives at a site
    string Code,
    string Name,
    string? Description,
    WorkCenterType Type,
    WorkCenterCapacityModel CapacityModel,
    int? CalendarId,
    int? OwningDepartmentId,
    decimal StandardCostRatePerHour,
    decimal OverheadRatePerHour,
    string CurrencyCode,
    string? CreatedBy);

public sealed record UpdateWorkCenterHeaderRequest(
    int WorkCenterId,
    int LocationId,            // PR #5c.1: REQUIRED on Update too
    string Name,
    string? Description,
    WorkCenterType Type,
    WorkCenterCapacityModel CapacityModel,
    int? CalendarId,
    int? OwningDepartmentId,
    decimal EfficiencyPct,
    decimal UtilizationPct,
    decimal StandardCostRatePerHour,
    decimal OverheadRatePerHour,
    string CurrencyCode,
    string? ModifiedBy);

public sealed record UpdateWorkCenterStatusRequest(
    int WorkCenterId,
    WorkCenterStatus NewStatus,
    string? ModifiedBy);

public sealed record LinkAssetRequest(
    int WorkCenterId,
    int AssetId,
    bool IsPrimary,
    string? CreatedBy);

public sealed class WorkCenterService : IWorkCenterService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IChainOfCustodyService _chainOfCustody;
    private readonly ILogger<WorkCenterService> _logger;

    public WorkCenterService(
        AppDbContext db,
        ITenantContext tenantContext,
        IChainOfCustodyService chainOfCustody,
        ILogger<WorkCenterService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _chainOfCustody = chainOfCustody;
        _logger = logger;
    }

    public async Task<Result<WorkCenter>> CreateAsync(CreateWorkCenterRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code))
            return Result.Failure<WorkCenter>("Code is required.");
        if (string.IsNullOrWhiteSpace(r.Name))
            return Result.Failure<WorkCenter>("Name is required.");
        if (r.LocationId <= 0)
            return Result.Failure<WorkCenter>("LocationId is required — every Work Center physically lives at a site.");
        if (!_tenantContext.VisibleCompanyIds.Contains(r.CompanyId))
            return Result.Failure<WorkCenter>("Company is not in your tenant scope.");

        // PR #5c.1 — site-prefixed dup check (mirrors the new UNIQUE index).
        var dup = await _db.WorkCenters
            .AnyAsync(w => w.CompanyId == r.CompanyId && w.LocationId == r.LocationId && w.Code == r.Code, ct);
        if (dup) return Result.Failure<WorkCenter>($"WorkCenter with code '{r.Code}' already exists at this site.");

        var wc = new WorkCenter
        {
            CompanyId = r.CompanyId,
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            Description = r.Description,
            Type = r.Type,
            CapacityModel = r.CapacityModel,
            Status = WorkCenterStatus.Active,
            CalendarId = r.CalendarId,
            LocationId = r.LocationId,
            OwningDepartmentId = r.OwningDepartmentId,
            StandardCostRatePerHour = r.StandardCostRatePerHour,
            OverheadRatePerHour = r.OverheadRatePerHour,
            CurrencyCode = string.IsNullOrEmpty(r.CurrencyCode) ? "USD" : r.CurrencyCode,
            CreatedBy = r.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        _db.WorkCenters.Add(wc);
        await _db.SaveChangesAsync(ct);
        return Result.Success(wc);
    }

    public async Task<Result<WorkCenter>> UpdateHeaderAsync(UpdateWorkCenterHeaderRequest r, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.Id == r.WorkCenterId, ct);
        if (wc is null) return Result.Failure<WorkCenter>("WorkCenter not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(wc.CompanyId))
            return Result.Failure<WorkCenter>("WorkCenter is not in your tenant scope.");
        if (r.LocationId <= 0)
            return Result.Failure<WorkCenter>("LocationId is required.");

        wc.Name = r.Name?.Trim() ?? wc.Name;
        wc.Description = r.Description;
        wc.Type = r.Type;
        wc.CapacityModel = r.CapacityModel;
        wc.CalendarId = r.CalendarId;
        wc.LocationId = r.LocationId;
        wc.OwningDepartmentId = r.OwningDepartmentId;
        wc.EfficiencyPct = r.EfficiencyPct;
        wc.UtilizationPct = r.UtilizationPct;
        wc.StandardCostRatePerHour = r.StandardCostRatePerHour;
        wc.OverheadRatePerHour = r.OverheadRatePerHour;
        wc.CurrencyCode = string.IsNullOrEmpty(r.CurrencyCode) ? "USD" : r.CurrencyCode;
        wc.ModifiedAt = DateTime.UtcNow;
        wc.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(wc);
    }

    public async Task<Result<WorkCenter>> UpdateStatusAsync(UpdateWorkCenterStatusRequest r, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.Id == r.WorkCenterId, ct);
        if (wc is null) return Result.Failure<WorkCenter>("WorkCenter not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(wc.CompanyId))
            return Result.Failure<WorkCenter>("WorkCenter is not in your tenant scope.");
        if (wc.Status == WorkCenterStatus.Retired && r.NewStatus != WorkCenterStatus.Retired)
            return Result.Failure<WorkCenter>("Retired is terminal; can't reactivate. Create a new WorkCenter.");
        wc.Status = r.NewStatus;
        wc.IsActive = r.NewStatus == WorkCenterStatus.Active;
        wc.ModifiedAt = DateTime.UtcNow;
        wc.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(wc);
    }

    public async Task<Result<WorkCenterAssetLink>> LinkAssetAsync(LinkAssetRequest r, CancellationToken ct)
    {
        var wc = await _db.WorkCenters.FirstOrDefaultAsync(w => w.Id == r.WorkCenterId, ct);
        if (wc is null) return Result.Failure<WorkCenterAssetLink>("WorkCenter not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(wc.CompanyId))
            return Result.Failure<WorkCenterAssetLink>("WorkCenter is not in your tenant scope.");

        // If primary requested, close any active primary first.
        if (r.IsPrimary)
        {
            var existingPrimary = await _db.WorkCenterAssetLinks
                .Where(l => l.WorkCenterId == r.WorkCenterId && l.IsPrimary && l.EffectiveTo == null)
                .ToListAsync(ct);
            foreach (var p in existingPrimary)
            {
                p.IsPrimary = false;
            }
        }

        var link = new WorkCenterAssetLink
        {
            WorkCenterId = r.WorkCenterId,
            AssetId = r.AssetId,
            IsPrimary = r.IsPrimary,
            EffectiveFrom = DateTime.UtcNow,
            CreatedBy = r.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };
        _db.WorkCenterAssetLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        // PR #5c.1 — emit WC→Asset chain edge per the BIC entity checklist + ADR-022.
        // Failure-isolated: log warning but don't fail the link write.
        try
        {
            await _chainOfCustody.RecordEdgeAsync(new RecordEdgeRequest(
                FromNodeType: "WorkCenter",
                FromEntityId: wc.Id,
                FromLabel: wc.Code,
                ToNodeType: "Asset",
                ToEntityId: r.AssetId,
                ToLabel: "Asset-" + r.AssetId,
                EdgeType: ChainEdgeTypes.WorkCenterUsesAsset), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chain edge emission failed for WorkCenter→Asset link (non-fatal)");
        }
        return Result.Success(link);
    }
}
