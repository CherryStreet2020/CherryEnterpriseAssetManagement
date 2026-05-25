using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Quality
{
    // ================================================================
    // Sprint 13.5 PR #338 — FaiService implementation.
    //
    // Per-tenant monotonic FaiNumber assignment uses a simple LAST+1
    // lookup wrapped in a serializable-isolation transaction. Status
    // regression illegal-transitions are blocked by the
    // fn_block_fai_status_regression Postgres trigger from PR #1.75.
    //
    // Spec: docs/research/fai-ui-pr338-spec-2026-05-25.md
    // ================================================================
    public class FaiService : IFaiService
    {
        private readonly AppDbContext _db;
        private readonly AuditService _audit;
        private readonly ILogger<FaiService> _log;

        // Matches "FAI-2026-00042" → captures the digit run.
        private static readonly Regex FaiNumberPattern =
            new(@"^FAI-\d{4}-(\d+)$", RegexOptions.Compiled);

        public FaiService(AppDbContext db, AuditService audit, ILogger<FaiService> log)
        {
            _db = db;
            _audit = audit;
            _log = log;
        }

        public async Task<IReadOnlyList<FaiReport>> ListAsync(int companyId, int? customerProjectId, CancellationToken ct)
        {
            var q = _db.FaiReports
                .AsNoTracking()
                .Where(f => f.CompanyId == companyId);
            if (customerProjectId.HasValue)
                q = q.Where(f => f.CustomerProjectId == customerProjectId.Value);
            return await q
                .OrderByDescending(f => f.CreatedAt)
                .Take(200)
                .ToListAsync(ct);
        }

        public async Task<FaiReport?> GetByIdAsync(long id, CancellationToken ct)
            => await _db.FaiReports
                .AsNoTracking()
                .Include(f => f.Item)
                .Include(f => f.Customer)
                .Include(f => f.CustomerProject)
                .Include(f => f.ProductionOrder)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

        public async Task<IReadOnlyList<FaiCharacteristic>> GetCharacteristicsAsync(long faiReportId, CancellationToken ct)
            => await _db.FaiCharacteristics
                .AsNoTracking()
                .Where(c => c.FaiReportId == faiReportId)
                .OrderBy(c => c.BalloonNumber)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<FaiProductAccountability>> GetProductAccountabilityAsync(long faiReportId, CancellationToken ct)
            => await _db.FaiProductAccountability
                .AsNoTracking()
                .Where(p => p.FaiReportId == faiReportId)
                .OrderBy(p => p.EntryType)
                .ThenBy(p => p.Id)
                .ToListAsync(ct);

        public async Task<FaiReport> CreateAsync(FaiCreateRequest req, int userId, string? username, CancellationToken ct)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));
            if (string.IsNullOrWhiteSpace(req.PartNumberSnapshot))
                throw new ArgumentException("PartNumberSnapshot required", nameof(req));

            var faiNumber = await NextFaiNumberAsync(req.TenantId, ct);

            var fai = new FaiReport
            {
                CompanyId = req.CompanyId,
                TenantId = req.TenantId,
                FaiNumber = faiNumber,
                Revision = 1,
                Type = req.Type,
                PartType = req.PartType,
                Reason = req.Reason,
                ReasonText = req.ReasonText,
                ItemId = req.ItemId,
                CustomerProjectId = req.CustomerProjectId,
                CustomerId = req.CustomerId,
                PartNumberSnapshot = req.PartNumberSnapshot.Trim(),
                PartNameSnapshot = req.PartNameSnapshot?.Trim(),
                DrawingNumberSnapshot = req.DrawingNumberSnapshot?.Trim(),
                DrawingRevSnapshot = req.DrawingRevSnapshot?.Trim(),
                Status = FaiStatus.Draft,
                CharacteristicCount = 0,
                NonConformCount = 0,
                WaivedCount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };

            _db.FaiReports.Add(fai);
            await _db.SaveChangesAsync(ct);

            try
            {
                await _audit.LogAsync(
                    "FaiReport.Created",
                    before: (object?)null,
                    after: new FaiAuditSnapshot(fai.Id, fai.FaiNumber, fai.PartNumberSnapshot, fai.Status.ToString()),
                    username: username,
                    description: $"Created FAI {fai.FaiNumber} for PartNumber {fai.PartNumberSnapshot}");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log failed for FAI create {FaiId}", fai.Id);
            }

            return fai;
        }

        public async Task<FaiCharacteristic> RecordCharacteristicAsync(long faiReportId, FaiCharacteristic row, int userId, string? username, CancellationToken ct)
        {
            var fai = await _db.FaiReports.FirstOrDefaultAsync(f => f.Id == faiReportId, ct)
                ?? throw new InvalidOperationException($"FAI {faiReportId} not found");
            if (fai.Status == FaiStatus.Voided)
                throw new InvalidOperationException($"FAI {fai.FaiNumber} is Voided — cannot add characteristics");

            row.FaiReportId = faiReportId;
            row.InspectorId = userId;
            row.InspectorName = username;
            row.InspectionDate ??= DateTime.UtcNow;
            row.CreatedAt = DateTime.UtcNow;
            row.CreatedBy = username;
            _db.FaiCharacteristics.Add(row);

            // Move Draft → InProgress on first characteristic.
            if (fai.Status == FaiStatus.Draft)
            {
                fai.Status = FaiStatus.InProgress;
                fai.InspectionStartedAt ??= DateTime.UtcNow;
            }
            fai.CharacteristicCount += 1;
            if (row.Conformance == FaiConformance.NonConforms)
                fai.NonConformCount += 1;
            if (row.Conformance == FaiConformance.Waived || row.Conformance == FaiConformance.Conditional)
                fai.WaivedCount += 1;

            // AI risk score: deterministic from NonConformCount + Status (PR #1.75 spec).
            fai.AiRiskScore = ComputeRiskScore(fai);
            fai.AiRiskTone = ToneFromScore(fai.AiRiskScore);

            await _db.SaveChangesAsync(ct);
            return row;
        }

        public async Task<FaiReport> SubmitAsync(long faiReportId, int submitterUserId, string? submitterName, CancellationToken ct)
        {
            var fai = await _db.FaiReports.FirstOrDefaultAsync(f => f.Id == faiReportId, ct)
                ?? throw new InvalidOperationException($"FAI {faiReportId} not found");
            if (fai.Status != FaiStatus.InProgress && fai.Status != FaiStatus.Draft)
                throw new InvalidOperationException($"FAI {fai.FaiNumber} status is {fai.Status} — only Draft/InProgress can Submit");

            fai.Status = FaiStatus.Submitted;
            fai.SubmittedAt = DateTime.UtcNow;
            fai.SubmittedById = submitterUserId;
            fai.InspectionCompletedAt ??= DateTime.UtcNow;
            fai.ModifiedAt = DateTime.UtcNow;
            fai.ModifiedBy = submitterName;
            await _db.SaveChangesAsync(ct);
            return fai;
        }

        public async Task<FaiReport> SignOffAsync(long faiReportId, int approverUserId, string? approverName, CancellationToken ct)
        {
            var fai = await _db.FaiReports.FirstOrDefaultAsync(f => f.Id == faiReportId, ct)
                ?? throw new InvalidOperationException($"FAI {faiReportId} not found");
            if (fai.Status != FaiStatus.Submitted)
                throw new InvalidOperationException($"FAI {fai.FaiNumber} status is {fai.Status} — only Submitted can be Approved");
            // AS9102 Rev C: SubmittedById != ApprovedById.
            if (fai.SubmittedById.HasValue && fai.SubmittedById.Value == approverUserId)
                throw new InvalidOperationException("AS9102 Rev C: approver cannot be the same person who submitted");

            fai.Status = FaiStatus.Approved;
            fai.ApprovedAt = DateTime.UtcNow;
            fai.ApprovedById = approverUserId;
            fai.ApprovedByName = approverName;
            fai.ModifiedAt = DateTime.UtcNow;
            fai.ModifiedBy = approverName;
            // Refresh risk score on approval state change.
            fai.AiRiskScore = ComputeRiskScore(fai);
            fai.AiRiskTone = ToneFromScore(fai.AiRiskScore);
            await _db.SaveChangesAsync(ct);

            try
            {
                await _audit.LogAsync(
                    "FaiReport.Approved",
                    before: (object?)null,
                    after: new FaiAuditSnapshot(fai.Id, fai.FaiNumber, fai.PartNumberSnapshot, fai.Status.ToString()),
                    username: approverName,
                    description: $"Approved FAI {fai.FaiNumber}");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Audit log failed for FAI sign-off {FaiId}", fai.Id);
            }
            return fai;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private async Task<string> NextFaiNumberAsync(int? tenantId, CancellationToken ct)
        {
            // Simple LAST+1 per tenant per year. Wrapped in serializable-isolation tx.
            var year = DateTime.UtcNow.Year;
            var prefix = $"FAI-{year}-";
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var existing = await _db.FaiReports
                .Where(f => f.TenantId == tenantId && f.FaiNumber.StartsWith(prefix))
                .Select(f => f.FaiNumber)
                .ToListAsync(ct);
            int next = 1;
            foreach (var fn in existing)
            {
                var m = FaiNumberPattern.Match(fn);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n >= next)
                    next = n + 1;
            }
            await tx.CommitAsync(ct);
            return $"{prefix}{next:D5}";
        }

        private static short ComputeRiskScore(FaiReport fai)
        {
            // Deterministic: 0 if Approved + 0 non-conforms; otherwise weighted by
            // non-conforms vs total characteristics + status modifier.
            // Range: 0..100.
            if (fai.CharacteristicCount == 0)
                return fai.Status == FaiStatus.Approved ? (short)0 : (short)10;
            var ncRatio = fai.CharacteristicCount > 0
                ? (double)fai.NonConformCount / fai.CharacteristicCount
                : 0;
            double baseScore = ncRatio * 60;
            double statusBoost = fai.Status switch
            {
                FaiStatus.Rejected => 30,
                FaiStatus.Voided => 25,
                FaiStatus.Conditional => 15,
                FaiStatus.Submitted => 5,
                _ => 0
            };
            var total = Math.Min(100, baseScore + statusBoost);
            return (short)Math.Round(total);
        }

        private static Abs.FixedAssets.Models.Projects.RiskTone ToneFromScore(short? score)
        {
            if (!score.HasValue) return Abs.FixedAssets.Models.Projects.RiskTone.Green;
            if (score.Value >= 50) return Abs.FixedAssets.Models.Projects.RiskTone.Red;
            if (score.Value >= 20) return Abs.FixedAssets.Models.Projects.RiskTone.Amber;
            return Abs.FixedAssets.Models.Projects.RiskTone.Green;
        }

        // Flat DTO for AuditService.
        private sealed record FaiAuditSnapshot(long Id, string FaiNumber, string PartNumber, string Status);
    }
}
