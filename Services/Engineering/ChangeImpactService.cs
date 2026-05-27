// Sprint 14.3 PR-7 (2026-05-27) — ChangeImpactService implementation.
// Walks the full chain of custody from ECO → affected items → in-flight PROs,
// active deviations/waivers/concessions, open CARs, and document versions.
// Triggers FAI re-qualification per AS9102 §3.2 on form/fit/function changes.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Quality;
using Abs.FixedAssets.Services.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Engineering
{
    public class ChangeImpactService : IChangeImpactService
    {
        private readonly AppDbContext _db;
        private readonly IFaiService _faiService;
        private readonly ILogger<ChangeImpactService> _log;

        public ChangeImpactService(
            AppDbContext db,
            IFaiService faiService,
            ILogger<ChangeImpactService> log)
        {
            _db = db;
            _faiService = faiService;
            _log = log;
        }

        public async Task<Result<ChangeImpactAnalysis>> AnalyzeEcoImpactAsync(
            int ecoId, string analysisNumber, string analyzedBy, CancellationToken ct = default)
        {
            // ---- Load ECO with line items ----
            var eco = await _db.Set<EngineeringChangeOrder>()
                .Include(e => e.LineItems)
                .FirstOrDefaultAsync(e => e.Id == ecoId, ct);

            if (eco == null)
                return Result.Failure<ChangeImpactAnalysis>($"ECO {ecoId} not found.");

            // ---- Check for duplicate analysis ----
            var existing = await _db.Set<ChangeImpactAnalysis>()
                .AnyAsync(a => a.EcoId == ecoId && a.CompanyId == eco.CompanyId, ct);
            if (existing)
                return Result.Failure<ChangeImpactAnalysis>(
                    $"Impact analysis already exists for ECO {eco.EcoNumber}.");

            // ---- Collect distinct affected item IDs from ECO line items ----
            var affectedItemIds = (eco.LineItems ?? Enumerable.Empty<EcoLineItem>())
                .Where(li => li.AffectedItemId.HasValue)
                .Select(li => li.AffectedItemId!.Value)
                .Distinct()
                .ToList();

            var affectedDocVersionIds = (eco.LineItems ?? Enumerable.Empty<EcoLineItem>())
                .Where(li => li.AffectedDocumentVersionId.HasValue)
                .Select(li => li.AffectedDocumentVersionId!.Value)
                .Distinct()
                .ToList();

            // ---- Create analysis header ----
            var analysis = new ChangeImpactAnalysis
            {
                CompanyId = eco.CompanyId,
                AnalysisNumber = analysisNumber,
                EcoId = ecoId,
                Status = ImpactAnalysisStatus.Pending,
                RequiresFaiRetrigger = eco.RequiresFaiRetrigger,
                RequiresCustomerNotice = eco.RequiresCustomerNotice,
                AnalyzedAtUtc = DateTime.UtcNow,
                AnalyzedBy = analyzedBy,
                CreatedBy = analyzedBy,
            };

            _db.Set<ChangeImpactAnalysis>().Add(analysis);
            await _db.SaveChangesAsync(ct); // flush to get analysis.Id

            var lines = new List<ChangeImpactLine>();

            // ---- 1. In-flight Production Orders ----
            if (affectedItemIds.Count > 0)
            {
                var activeStatuses = new[]
                {
                    ProductionOrderStatus.Planned,
                    ProductionOrderStatus.Firmed,
                    ProductionOrderStatus.Released,
                    ProductionOrderStatus.InProgress,
                    ProductionOrderStatus.OnHold,
                };

                var affectedPros = await _db.Set<ProductionOrder>()
                    .Where(p => p.CompanyId == eco.CompanyId
                        && p.ItemId.HasValue
                        && affectedItemIds.Contains(p.ItemId.Value)
                        && activeStatuses.Contains(p.Status))
                    .Select(p => new { p.Id, p.OrderNumber, p.Title, p.Status, p.ItemId, p.QuantityOrdered, p.QuantityCompleted })
                    .ToListAsync(ct);

                foreach (var pro in affectedPros)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.ProductionOrder,
                        Severity = pro.Status == ProductionOrderStatus.InProgress
                            ? ImpactSeverity.Critical
                            : ImpactSeverity.Warning,
                        AffectedEntityId = pro.Id,
                        AffectedEntityDescription = $"{pro.OrderNumber} '{pro.Title}' — {pro.Status}, {pro.QuantityOrdered - pro.QuantityCompleted} remaining",
                        AffectedItemId = pro.ItemId,
                        RecommendedAction = pro.Status == ProductionOrderStatus.InProgress
                            ? "Review in-process material. Hold if revision change affects F/F/F. Disposition existing WIP."
                            : "Update BOM snapshot to new revision before release.",
                        CreatedBy = analyzedBy,
                    });
                }

                // ---- 2. Active Deviations ----
                var activeDevStatuses = new[] { DeviationStatus.Approved, DeviationStatus.Active };
                var affectedDevs = await _db.Set<Deviation>()
                    .Where(d => d.CompanyId == eco.CompanyId
                        && d.ItemId.HasValue
                        && affectedItemIds.Contains(d.ItemId.Value)
                        && activeDevStatuses.Contains(d.Status))
                    .Select(d => new { d.Id, d.DeviationNumber, d.Title, d.Status, d.ItemId, d.Type, d.ExpirationDateUtc })
                    .ToListAsync(ct);

                foreach (var dev in affectedDevs)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.Deviation,
                        Severity = ImpactSeverity.Warning,
                        AffectedEntityId = dev.Id,
                        AffectedEntityDescription = $"{dev.DeviationNumber} '{dev.Title}' — {dev.Type} {dev.Status}, expires {dev.ExpirationDateUtc:yyyy-MM-dd}",
                        AffectedItemId = dev.ItemId,
                        RecommendedAction = "Review deviation scope against ECO change. Close or extend as appropriate.",
                        CreatedBy = analyzedBy,
                    });
                }

                // ---- 3. Active Waivers ----
                var affectedWaivers = await _db.Set<Waiver>()
                    .Where(w => w.CompanyId == eco.CompanyId
                        && w.ItemId.HasValue
                        && affectedItemIds.Contains(w.ItemId.Value)
                        && (w.Status == WaiverStatus.Approved || w.Status == WaiverStatus.Active))
                    .Select(w => new { w.Id, w.WaiverNumber, w.Title, w.Status, w.ItemId })
                    .ToListAsync(ct);

                foreach (var waiver in affectedWaivers)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.Waiver,
                        Severity = ImpactSeverity.Warning,
                        AffectedEntityId = waiver.Id,
                        AffectedEntityDescription = $"{waiver.WaiverNumber} '{waiver.Title}' — {waiver.Status}",
                        AffectedItemId = waiver.ItemId,
                        RecommendedAction = "Review waiver scope against ECO. Customer re-approval may be required.",
                        CreatedBy = analyzedBy,
                    });
                }

                // ---- 4. Active Concessions ----
                var affectedConcessions = await _db.Set<Concession>()
                    .Where(c => c.CompanyId == eco.CompanyId
                        && c.ItemId.HasValue
                        && affectedItemIds.Contains(c.ItemId.Value)
                        && (c.Status == ConcessionStatus.Accepted || c.Status == ConcessionStatus.CustomerReview))
                    .Select(c => new { c.Id, c.ConcessionNumber, c.Title, c.Status, c.ItemId })
                    .ToListAsync(ct);

                foreach (var con in affectedConcessions)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.Concession,
                        Severity = ImpactSeverity.Warning,
                        AffectedEntityId = con.Id,
                        AffectedEntityDescription = $"{con.ConcessionNumber} '{con.Title}' — {con.Status}",
                        AffectedItemId = con.ItemId,
                        RecommendedAction = "Review concession scope. ECO may render concession unnecessary.",
                        CreatedBy = analyzedBy,
                    });
                }

                // ---- 5. Open CARs ----
                var openCarStatuses = new[]
                {
                    CarStatus.Issued, CarStatus.UnderInvestigation, CarStatus.RootCauseIdentified,
                    CarStatus.CorrectiveActionPlanned, CarStatus.ImplementationInProgress, CarStatus.VerificationPending,
                };
                var affectedCars = await _db.Set<CorrectiveActionRequest>()
                    .Where(c => c.CompanyId == eco.CompanyId
                        && c.ItemId.HasValue
                        && affectedItemIds.Contains(c.ItemId.Value)
                        && openCarStatuses.Contains(c.Status))
                    .Select(c => new { c.Id, c.CarNumber, c.Title, c.Status, c.ItemId, c.Severity })
                    .ToListAsync(ct);

                foreach (var car in affectedCars)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.CorrectiveAction,
                        Severity = car.Severity == CarSeverity.Critical
                            ? ImpactSeverity.Critical
                            : ImpactSeverity.Warning,
                        AffectedEntityId = car.Id,
                        AffectedEntityDescription = $"{car.CarNumber} '{car.Title}' — {car.Status} {car.Severity}",
                        AffectedItemId = car.ItemId,
                        RecommendedAction = "Review CAR root cause. ECO may address the underlying non-conformance.",
                        CreatedBy = analyzedBy,
                    });
                }

                // ---- 6. FAI Re-trigger lines (one per affected item) ----
                if (eco.RequiresFaiRetrigger)
                {
                    foreach (var itemId in affectedItemIds)
                    {
                        // Fetch item details for the description
                        var item = await _db.Set<Item>()
                            .Where(i => i.Id == itemId)
                            .Select(i => new { i.Id, i.PartNumber, i.Description })
                            .FirstOrDefaultAsync(ct);

                        if (item != null)
                        {
                            lines.Add(new ChangeImpactLine
                            {
                                ChangeImpactAnalysisId = analysis.Id,
                                LineType = ImpactLineType.FaiRetrigger,
                                Severity = ImpactSeverity.Critical,
                                AffectedEntityId = item.Id,
                                AffectedEntityDescription = $"FAI required: {item.PartNumber} '{item.Description}' — form/fit/function change per AS9102 §3.2",
                                AffectedItemId = item.Id,
                                RecommendedAction = "Trigger FAI re-qualification. First article run required before production release.",
                                CreatedBy = analyzedBy,
                            });
                        }
                    }
                }
            }

            // ---- 7. Affected Document Versions ----
            if (affectedDocVersionIds.Count > 0)
            {
                var docVersions = await _db.Set<DocumentVersion>()
                    .Where(dv => affectedDocVersionIds.Contains(dv.Id))
                    .Select(dv => new { dv.Id, dv.DocumentId, dv.RevisionCode, dv.FileName })
                    .ToListAsync(ct);

                foreach (var dv in docVersions)
                {
                    lines.Add(new ChangeImpactLine
                    {
                        ChangeImpactAnalysisId = analysis.Id,
                        LineType = ImpactLineType.DocumentVersion,
                        Severity = ImpactSeverity.Warning,
                        AffectedEntityId = dv.Id,
                        AffectedEntityDescription = $"Document version {dv.RevisionCode} '{dv.FileName}' — redline/supersede required",
                        RecommendedAction = "Create redline markup or release new revision via IDocumentService.",
                        CreatedBy = analyzedBy,
                    });
                }
            }

            // ---- Persist lines + update counters ----
            if (lines.Count > 0)
            {
                _db.Set<ChangeImpactLine>().AddRange(lines);
            }

            analysis.TotalImpactLines = lines.Count;
            analysis.CriticalImpactLines = lines.Count(l => l.Severity == ImpactSeverity.Critical);
            analysis.AffectedProductionOrderCount = lines.Count(l => l.LineType == ImpactLineType.ProductionOrder);
            analysis.AffectedDeviationCount = lines.Count(l =>
                l.LineType == ImpactLineType.Deviation
                || l.LineType == ImpactLineType.Waiver
                || l.LineType == ImpactLineType.Concession);
            analysis.AffectedCarCount = lines.Count(l => l.LineType == ImpactLineType.CorrectiveAction);
            analysis.AffectedDocumentCount = lines.Count(l => l.LineType == ImpactLineType.DocumentVersion);
            analysis.AffectedCustomerCount = lines.Count(l => l.LineType == ImpactLineType.CustomerNotice);
            analysis.Status = analysis.CriticalImpactLines > 0
                ? ImpactAnalysisStatus.ActionRequired
                : ImpactAnalysisStatus.Complete;
            analysis.UpdatedAt = DateTime.UtcNow;
            analysis.UpdatedBy = analyzedBy;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Impact analysis {Number} for ECO {EcoNumber}: {Total} lines ({Critical} critical). " +
                "PROs={Pros} Deviations={Devs} CARs={Cars} Docs={Docs} FAI={Fai}",
                analysis.AnalysisNumber, eco.EcoNumber, analysis.TotalImpactLines,
                analysis.CriticalImpactLines, analysis.AffectedProductionOrderCount,
                analysis.AffectedDeviationCount, analysis.AffectedCarCount,
                analysis.AffectedDocumentCount,
                lines.Count(l => l.LineType == ImpactLineType.FaiRetrigger));

            return Result.Success(analysis);
        }

        public async Task<Result<ChangeImpactAnalysis>> TriggerFaiRetriggerAsync(
            int analysisId, string triggeredBy, CancellationToken ct = default)
        {
            var analysis = await _db.Set<ChangeImpactAnalysis>()
                .Include(a => a.Lines)
                .Include(a => a.Eco)
                .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

            if (analysis == null)
                return Result.Failure<ChangeImpactAnalysis>($"Analysis {analysisId} not found.");

            if (!analysis.RequiresFaiRetrigger)
                return Result.Failure<ChangeImpactAnalysis>("This analysis does not require FAI re-trigger.");

            if (analysis.FaiTriggeredAtUtc.HasValue)
                return Result.Failure<ChangeImpactAnalysis>(
                    $"FAI already triggered at {analysis.FaiTriggeredAtUtc:u} by {analysis.FaiTriggeredBy}.");

            var faiLines = analysis.Lines
                .Where(l => l.LineType == ImpactLineType.FaiRetrigger && !l.IsResolved)
                .ToList();

            if (faiLines.Count == 0)
                return Result.Failure<ChangeImpactAnalysis>("No unresolved FAI re-trigger lines found.");

            int faiCount = 0;
            foreach (var line in faiLines)
            {
                if (!line.AffectedItemId.HasValue) continue;

                var item = await _db.Set<Item>()
                    .FirstOrDefaultAsync(i => i.Id == line.AffectedItemId.Value, ct);
                if (item == null) continue;

                // Determine FAI reason from ECR flags (form/fit/function = DesignChange; process-only = ProcessChange)
                var reason = FaiReason.DesignChange;
                if (analysis.Eco != null)
                {
                    var ecr = await _db.Set<EngineeringChangeRequest>()
                        .FirstOrDefaultAsync(r => r.Id == analysis.Eco.SourceEcrId, ct);
                    if (ecr != null && !ecr.AffectsForm && !ecr.AffectsFit && !ecr.AffectsFunction)
                        reason = FaiReason.ProcessChange;
                }

                var faiReq = new FaiCreateRequest(
                    CompanyId: analysis.CompanyId,
                    TenantId: analysis.TenantId,
                    ItemId: item.Id,
                    CustomerProjectId: null,
                    CustomerId: null,
                    PartNumberSnapshot: item.PartNumber ?? $"ITEM-{item.Id}",
                    PartNameSnapshot: item.Description,
                    DrawingNumberSnapshot: null,
                    DrawingRevSnapshot: null,
                    Type: FaiType.Full,
                    PartType: FaiPartType.Detail,
                    Reason: reason,
                    ReasonText: $"ECO {analysis.Eco?.EcoNumber} — engineering change requires FAI re-qualification per AS9102 §3.2");

                var faiReport = await _faiService.CreateAsync(faiReq, 0, triggeredBy, ct);

                line.TriggeredFaiReportId = faiReport.Id;
                line.IsResolved = true;
                line.ActionTaken = $"FAI Report {faiReport.Id} created — Reason={reason}";
                line.ResolvedAtUtc = DateTime.UtcNow;
                line.ResolvedBy = triggeredBy;
                faiCount++;
            }

            analysis.FaiTriggeredAtUtc = DateTime.UtcNow;
            analysis.FaiTriggeredBy = triggeredBy;
            analysis.FaiReportsCreated = faiCount;
            analysis.ResolvedImpactLines = analysis.Lines.Count(l => l.IsResolved);
            if (analysis.Lines.All(l => l.IsResolved || l.Severity == ImpactSeverity.Info))
                analysis.Status = ImpactAnalysisStatus.AllResolved;
            analysis.UpdatedAt = DateTime.UtcNow;
            analysis.UpdatedBy = triggeredBy;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "FAI re-trigger for analysis {Id}: {Count} FAI reports created for ECO {Eco}",
                analysis.Id, faiCount, analysis.Eco?.EcoNumber);

            return Result.Success(analysis);
        }

        public async Task<Result<ChangeImpactLine>> ResolveImpactLineAsync(
            int lineId, string actionTaken, string resolvedBy, CancellationToken ct = default)
        {
            var line = await _db.Set<ChangeImpactLine>()
                .FirstOrDefaultAsync(l => l.Id == lineId, ct);
            if (line == null)
                return Result.Failure<ChangeImpactLine>($"Impact line {lineId} not found.");
            if (line.IsResolved)
                return Result.Failure<ChangeImpactLine>($"Impact line {lineId} already resolved.");

            line.IsResolved = true;
            line.ActionTaken = actionTaken;
            line.ResolvedAtUtc = DateTime.UtcNow;
            line.ResolvedBy = resolvedBy;

            // Update parent counters
            var analysis = await _db.Set<ChangeImpactAnalysis>()
                .Include(a => a.Lines)
                .FirstOrDefaultAsync(a => a.Id == line.ChangeImpactAnalysisId, ct);
            if (analysis != null)
            {
                analysis.ResolvedImpactLines = analysis.Lines.Count(l => l.IsResolved);
                if (analysis.Lines.All(l => l.IsResolved || l.Severity == ImpactSeverity.Info))
                {
                    analysis.Status = ImpactAnalysisStatus.AllResolved;
                    analysis.CompletedAtUtc = DateTime.UtcNow;
                    analysis.CompletedBy = resolvedBy;
                }
                analysis.UpdatedAt = DateTime.UtcNow;
                analysis.UpdatedBy = resolvedBy;
            }

            await _db.SaveChangesAsync(ct);
            return Result.Success(line);
        }

        public async Task<ChangeImpactAnalysis?> GetAnalysisAsync(int analysisId, CancellationToken ct = default)
            => await _db.Set<ChangeImpactAnalysis>()
                .Include(a => a.Lines)
                .Include(a => a.Eco)
                .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

        public async Task<ChangeImpactAnalysis?> GetAnalysisForEcoAsync(int ecoId, CancellationToken ct = default)
            => await _db.Set<ChangeImpactAnalysis>()
                .Include(a => a.Lines)
                .Include(a => a.Eco)
                .FirstOrDefaultAsync(a => a.EcoId == ecoId, ct);
    }
}
