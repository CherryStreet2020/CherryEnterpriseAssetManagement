// Theme B11 Wave R3-9 (2026-05-29) — Capability-match resolver impl (CLOSES Wave R3).
//
// Loads the op's requirements + the company's Active resources (with their
// qualifications) and evaluates eligibility in C# — the IsCurrentAsOf / proficiency
// / envelope logic is clearest in memory and the candidate pool is small. See the
// interface file for the design rationale.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public sealed class CapabilityMatchService : ICapabilityMatchService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CapabilityMatchService> _log;

        public CapabilityMatchService(AppDbContext db, ILogger<CapabilityMatchService> log)
        {
            _db = db; _log = log;
        }

        public async Task<Result<CapabilityMatchResult>> GetEligibleResourcesAsync(
            int routingOperationId, DateTime? asOfUtc = null, CancellationToken ct = default)
        {
            var asOf = asOfUtc ?? DateTime.UtcNow;

            // 1) Resolve the op + its company (tenant scope) + WC. RoutingOperation has
            //    no CompanyId — it is scoped THROUGH Routing.CompanyId.
            var opInfo = await (
                from op in _db.RoutingOperations
                join r in _db.Routings on op.RoutingId equals r.Id
                where op.Id == routingOperationId
                select new { op.Id, op.Description, op.WorkCenterId, CompanyId = r.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (opInfo == null)
                return Result.Failure<CapabilityMatchResult>($"RoutingOperation #{routingOperationId} not found.");

            // 2) The op's capability requirements (+ capability code for messaging).
            var requirements = await (
                from req in _db.OperationCapabilityRequirements
                where req.RoutingOperationId == routingOperationId
                join cap in _db.Capabilities on req.CapabilityId equals cap.Id
                select new ReqInfo(
                    req.Id, req.CapabilityId, cap.Code, req.RequirementType, req.MinProficiency,
                    req.IsMandatory, req.RequiredEnvelopeMin, req.RequiredEnvelopeMax))
                .ToListAsync(ct);

            var mandatoryCount = requirements.Count(r => r.IsMandatory);

            // 3) Candidate pool: Active resources in the op's company.
            var resources = await _db.ProductionResources
                .Where(r => r.CompanyId == opInfo.CompanyId && r.Status == ProductionResourceStatus.Active)
                .Select(r => new { r.Id, r.Code, r.Name, r.ResourceKind, r.WorkCenterId })
                .ToListAsync(ct);

            // 4) All qualifications for those resources, in one query.
            var resourceIds = resources.Select(r => r.Id).ToList();
            var quals = await _db.ResourceCapabilities
                .Where(rc => resourceIds.Contains(rc.ProductionResourceId))
                .Select(rc => new QualInfo(
                    rc.ProductionResourceId, rc.CapabilityId, rc.Proficiency,
                    rc.IsActive, rc.ExpiresOnUtc, rc.EnvelopeValue))
                .ToListAsync(ct);
            var qualsByResource = quals.ToLookup(q => q.ResourceId);

            // 5) Evaluate each resource against the requirements.
            var eligible = new List<ResourceEligibility>();
            var ineligible = new List<ResourceEligibility>();

            foreach (var res in resources)
            {
                var held = qualsByResource[res.Id].ToList();
                var matches = new List<RequirementMatch>(requirements.Count);
                var allMandatorySatisfied = true;
                string? firstUnmet = null;
                var score = 0;

                foreach (var req in requirements)
                {
                    var (satisfied, detail, proficiencyBonus) = Evaluate(req, held, asOf);
                    matches.Add(new RequirementMatch(
                        req.RequirementId, req.CapabilityCode, req.RequirementType,
                        req.IsMandatory, satisfied, detail));

                    if (satisfied)
                        score += proficiencyBonus + (req.IsMandatory ? 0 : 2); // prefs add a small rank bump
                    else if (req.IsMandatory)
                    {
                        allMandatorySatisfied = false;
                        firstUnmet ??= $"{req.CapabilityCode}: {detail}";
                    }
                }

                // Resources already on the op's work center get a tie-break bump.
                if (res.WorkCenterId != null && res.WorkCenterId == opInfo.WorkCenterId)
                    score += 5;

                var row = new ResourceEligibility(
                    res.Id, res.Code, res.Name, res.ResourceKind,
                    allMandatorySatisfied, score, matches,
                    allMandatorySatisfied ? null : firstUnmet);

                if (allMandatorySatisfied) eligible.Add(row);
                else ineligible.Add(row);
            }

            // 6) Rank: eligible by score desc then code; ineligible by code.
            var eligibleRanked = eligible
                .OrderByDescending(e => e.Score).ThenBy(e => e.ResourceCode).ToList();
            var ineligibleSorted = ineligible
                .OrderBy(e => e.ResourceCode).ToList();

            return Result.Success(new CapabilityMatchResult(
                opInfo.Id, opInfo.Description, asOf, mandatoryCount, resources.Count,
                eligibleRanked, ineligibleSorted));
        }

        /// <summary>Evaluate one requirement against a resource's held qualifications.</summary>
        private static (bool satisfied, string detail, int proficiencyBonus) Evaluate(
            ReqInfo req, List<QualInfo> held, DateTime asOf)
        {
            var rc = held.FirstOrDefault(q => q.CapabilityId == req.CapabilityId);
            if (rc == null)
                return (false, "capability not held", 0);
            if (!rc.IsActive)
                return (false, "qualification inactive", 0);
            if (rc.ExpiresOnUtc != null && rc.ExpiresOnUtc <= asOf)
                return (false, $"cert expired {rc.ExpiresOnUtc:yyyy-MM-dd}", 0);
            if (rc.Proficiency < req.MinProficiency)
                return (false, $"proficiency {rc.Proficiency} < required {req.MinProficiency}", 0);

            // Parametric envelope: if the requirement bounds it, the resource's achieved
            // value must exist and fall within [min, max].
            if (req.RequiredEnvelopeMin != null || req.RequiredEnvelopeMax != null)
            {
                if (rc.EnvelopeValue == null)
                    return (false, "no envelope value to satisfy the required bound", 0);
                if (req.RequiredEnvelopeMin != null && rc.EnvelopeValue < req.RequiredEnvelopeMin)
                    return (false, $"envelope {rc.EnvelopeValue} < required ≥{req.RequiredEnvelopeMin}", 0);
                if (req.RequiredEnvelopeMax != null && rc.EnvelopeValue > req.RequiredEnvelopeMax)
                    return (false, $"envelope {rc.EnvelopeValue} > required ≤{req.RequiredEnvelopeMax}", 0);
            }

            var bonus = (int)rc.Proficiency + 1;  // raw enum Provisional=0..Expert=2; +1 → 1..3 bonus
            return (true, $"holds {req.CapabilityCode} ({rc.Proficiency})", bonus);
        }

        // Flat projections (avoid pulling whole entities into memory).
        private sealed record ReqInfo(
            int RequirementId, int CapabilityId, string CapabilityCode,
            CapabilityRequirementType RequirementType, CapabilityProficiency MinProficiency,
            bool IsMandatory, decimal? RequiredEnvelopeMin, decimal? RequiredEnvelopeMax);

        private sealed record QualInfo(
            int ResourceId, int CapabilityId, CapabilityProficiency Proficiency,
            bool IsActive, DateTime? ExpiresOnUtc, decimal? EnvelopeValue);
    }
}
