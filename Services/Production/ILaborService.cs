// Sprint 13.5 PR #5d — ILaborService + impl.
//
// The shop-floor labor service the Operator Workbench writes to.
// Distinct from LaborConfig.cs (lookup/rate-table service): this one
// is the EVENT writer — clock-in / clock-out / per-operator aggregates.
//
// Methods (4 v1):
//   1. ClockInAsync       — operator starts work on a ProductionOperation
//   2. ClockOutAsync      — operator stops work on the current open entry
//   3. GetActiveAsync     — what's the operator's current open clock-in
//   4. GetTodayTotalsAsync — sum of setup/run mins + completed qty today
//                           (for the Workbench KPI band)
//
// TENANT SCOPING: ProductionOperation.CompanyIdSnapshot (set at release
// time per PR #5c.2) is the tenant scope. Service refuses any operation
// whose snapshot doesn't intersect _tenantContext.VisibleCompanyIds.
//
// ONE-OPEN-CLOCK-IN RULE: enforced both at the service layer (pre-check
// before insert) AND at the DB level (partial UNIQUE in the migration).
// Service catches the DB conflict and returns a friendly Failure.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public interface ILaborService
{
    Task<Result<LaborEntry>> ClockInAsync(ClockInRequest request, CancellationToken ct);
    Task<Result<LaborEntry>> ClockOutAsync(ClockOutRequest request, CancellationToken ct);
    Task<Result<LaborEntry?>> GetActiveAsync(int operatorUserId, CancellationToken ct);
    Task<Result<LaborTodayTotals>> GetTodayTotalsAsync(int operatorUserId, CancellationToken ct);
}

public sealed record ClockInRequest(
    int ProductionOperationId,
    int OperatorUserId,
    int? LaborTypeId,
    string? Notes,
    string? CreatedBy);

public sealed record ClockOutRequest(
    int LaborEntryId,
    string? Notes,
    string? ModifiedBy);

public sealed record LaborTodayTotals(
    decimal SetupMins,
    decimal RunMins,
    decimal CompletedQty,
    int OpsTouched);

public sealed class LaborService : ILaborService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LaborService> _logger;

    public LaborService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<LaborService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<LaborEntry>> ClockInAsync(ClockInRequest r, CancellationToken ct)
    {
        // Load the production operation + its tenant snapshot.
        var op = await _db.ProductionOperations
            .Where(x => x.Id == r.ProductionOperationId)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.WorkCenterId,
                x.CompanyIdSnapshot,
                x.LocationIdSnapshot
            })
            .FirstOrDefaultAsync(ct);
        if (op is null)
            return Result.Failure<LaborEntry>($"ProductionOperation {r.ProductionOperationId} not found.");

        // Tenant scope — PR #5c.2 snapshot.
        if (op.CompanyIdSnapshot <= 0 ||
            !_tenantContext.VisibleCompanyIds.Contains(op.CompanyIdSnapshot))
            return Result.Failure<LaborEntry>(
                "ProductionOperation is not in your tenant scope.");

        // Status gate — only legal to clock into an op that's ready for work.
        var legalStates = new[]
        {
            ProductionOperationStatus.Released,
            ProductionOperationStatus.InSetup,
            ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused
        };
        if (!legalStates.Contains(op.Status))
            return Result.Failure<LaborEntry>(
                $"Cannot clock in: operation status is {op.Status}. " +
                $"Allowed: {string.Join(", ", legalStates)}.");

        // One-open-clock-in pre-check (friendlier than the DB-level partial UNIQUE bubble).
        var existingOpen = await _db.LaborEntries
            .Where(le => le.CompanyId == op.CompanyIdSnapshot
                      && le.OperatorUserId == r.OperatorUserId
                      && le.ClockOutAt == null)
            .Select(le => new { le.Id, le.ProductionOperationId })
            .FirstOrDefaultAsync(ct);
        if (existingOpen is not null)
            return Result.Failure<LaborEntry>(
                $"Operator already has an open clock-in on operation #{existingOpen.ProductionOperationId} " +
                $"(LaborEntry #{existingOpen.Id}). Clock out before starting a new one.");

        // Operator user must exist.
        var operatorExists = await _db.Users.AnyAsync(u => u.Id == r.OperatorUserId, ct);
        if (!operatorExists)
            return Result.Failure<LaborEntry>($"Operator user {r.OperatorUserId} not found.");

        var now = DateTime.UtcNow;
        var entry = new LaborEntry
        {
            CompanyId = op.CompanyIdSnapshot,
            LocationId = op.LocationIdSnapshot,
            ProductionOperationId = op.Id,
            OperatorUserId = r.OperatorUserId,
            LaborTypeId = r.LaborTypeId,
            ClockInAt = now,
            Notes = r.Notes,
            CreatedAt = now,
            CreatedBy = r.CreatedBy
        };
        _db.LaborEntries.Add(entry);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "ClockIn failed for operator {OperatorId} on op {OpId} (likely concurrent open clock-in)",
                r.OperatorUserId, r.ProductionOperationId);
            return Result.Failure<LaborEntry>(
                "Clock-in failed (another open clock-in for this operator may have been created concurrently).");
        }

        return Result.Success(entry);
    }

    public async Task<Result<LaborEntry>> ClockOutAsync(ClockOutRequest r, CancellationToken ct)
    {
        var entry = await _db.LaborEntries.FirstOrDefaultAsync(x => x.Id == r.LaborEntryId, ct);
        if (entry is null)
            return Result.Failure<LaborEntry>($"LaborEntry {r.LaborEntryId} not found.");

        if (entry.ClockOutAt is not null)
            return Result.Failure<LaborEntry>(
                $"LaborEntry already clocked out at {entry.ClockOutAt:O}. Cannot re-clock-out.");

        // Tenant scope check.
        if (!_tenantContext.VisibleCompanyIds.Contains(entry.CompanyId))
            return Result.Failure<LaborEntry>("LaborEntry is not in your tenant scope.");

        var now = DateTime.UtcNow;
        entry.ClockOutAt = now;
        entry.DurationMins = Math.Round(
            (decimal)(now - entry.ClockInAt).TotalMinutes, 2);
        if (!string.IsNullOrWhiteSpace(r.Notes))
            entry.Notes = string.IsNullOrEmpty(entry.Notes)
                ? r.Notes
                : entry.Notes + "\n---\n" + r.Notes;
        entry.ModifiedAt = now;
        entry.ModifiedBy = r.ModifiedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(entry);
    }

    public async Task<Result<LaborEntry?>> GetActiveAsync(int operatorUserId, CancellationToken ct)
    {
        var entry = await _db.LaborEntries
            .Where(le => le.OperatorUserId == operatorUserId
                      && le.ClockOutAt == null
                      && _tenantContext.VisibleCompanyIds.Contains(le.CompanyId))
            .FirstOrDefaultAsync(ct);
        return Result.Success<LaborEntry?>(entry);
    }

    public async Task<Result<LaborTodayTotals>> GetTodayTotalsAsync(int operatorUserId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.Date;  // midnight UTC — good enough for v1
        var visible = _tenantContext.VisibleCompanyIds.ToHashSet();

        var entries = await _db.LaborEntries
            .Where(le => le.OperatorUserId == operatorUserId
                      && le.ClockInAt >= since
                      && visible.Contains(le.CompanyId))
            .Select(le => new { le.ProductionOperationId, le.DurationMins, le.ClockOutAt, le.ClockInAt })
            .ToListAsync(ct);

        decimal totalMins = entries.Sum(e =>
            e.DurationMins ?? (decimal)((e.ClockOutAt ?? DateTime.UtcNow) - e.ClockInAt).TotalMinutes);

        // Setup vs Run split: walk the ProductionOperation actuals for each
        // touched op (small N, fine for now).
        var opIds = entries.Select(e => e.ProductionOperationId).Distinct().ToList();
        var ops = await _db.ProductionOperations
            .Where(o => opIds.Contains(o.Id))
            .Select(o => new { o.Id, o.ActualSetupMins, o.ActualRunMins, o.CompletedQty })
            .ToListAsync(ct);

        var totals = new LaborTodayTotals(
            SetupMins:    ops.Sum(o => o.ActualSetupMins),
            RunMins:      ops.Sum(o => o.ActualRunMins),
            CompletedQty: ops.Sum(o => o.CompletedQty),
            OpsTouched:   opIds.Count);

        return Result.Success(totals);
    }
}
