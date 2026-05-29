// Theme B11 Wave R3-9 — admin probe for the capability-match resolver (CLOSES Wave R3).
// Lock-16 corollary: every button writes (the run button writes the scenario the
// first time it is exercised via "Set up worked example").
//
//   1) Set up worked example  — idempotently builds the disruptor demo: the latest
//      routing op requires 5-axis milling (mandatory, envelope ≥5) + CMM inspection
//      (preferred), and 5 resources with varied profiles (eligible ×2, ineligible by
//      envelope, ineligible by expired cert, excluded by Inactive).
//   2) Run match              — calls ICapabilityMatchService.GetEligibleResourcesAsync
//      on the latest op and renders eligible (ranked) vs ineligible (with reasons).
//   R) Reload
//
// RoutingOperation has no CompanyId — scoped THROUGH Routing.CompanyId. Writes are
// stamped with the op's OWN company (R1-2/R3-8 multi-company discipline).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R3-9 capability-match resolver. " +
    "Builds a worked example (capabilities, op requirements, resources + qualifications) " +
    "and runs ICapabilityMatchService, all tenant-scoped through the op's Routing.CompanyId.")]
public sealed class CapabilityMatchProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICapabilityMatchService _match;
    private readonly ILogger<CapabilityMatchProbeModel> _logger;

    public CapabilityMatchProbeModel(AppDbContext db, ITenantContext tenant,
        ICapabilityMatchService match, ILogger<CapabilityMatchProbeModel> logger)
    {
        _db = db; _tenant = tenant; _match = match; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int LatestOpId { get; private set; }
    public CapabilityMatchResult? MatchResult { get; private set; }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<RoutingOperation> ScopedOps() =>
        from op in _db.RoutingOperations
        join r in _db.Routings on op.RoutingId equals r.Id
        where _tenant.VisibleCompanyIds.Contains(r.CompanyId)
        select op;

    private Task<RoutingOperation?> LatestOpAsync(CancellationToken ct) =>
        ScopedOps().OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);

    private Task<int> OpCompanyAsync(RoutingOperation op, CancellationToken ct) =>
        _db.Routings.Where(r => r.Id == op.RoutingId).Select(r => r.CompanyId).FirstAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        LatestOpId = (await LatestOpAsync(ct))?.Id ?? 0;
    }

    // ── ensure helpers (idempotent by natural key, scoped to the op's company) ──

    private async Task<Capability> EnsureCapabilityAsync(int companyId, string code, string name,
        CapabilityCategory category, bool special, bool needsQual, bool parameterized,
        string? uom, decimal? envMin, string? std, CancellationToken ct)
    {
        var cap = await _db.Capabilities
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Code == code, ct);
        if (cap != null) return cap;
        cap = new Capability
        {
            CompanyId = companyId, Category = category, Code = code, Name = name,
            IsSpecialProcess = special, RequiresQualification = needsQual,
            DefaultQualificationValidityMonths = needsQual ? 24 : (int?)null,
            IsParameterized = parameterized, EnvelopeUom = uom, EnvelopeMin = envMin,
            GoverningStandard = std, CreatedBy = "CapabilityMatchProbe",
        };
        _db.Capabilities.Add(cap);
        await _db.SaveChangesAsync(ct);
        return cap;
    }

    private async Task<ProductionResource> EnsureResourceAsync(int companyId, string code, string name,
        ProductionResourceStatus status, int? workCenterId, CancellationToken ct)
    {
        var res = await _db.ProductionResources
            .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Code == code, ct);
        if (res == null)
        {
            res = new ProductionResource
            {
                CompanyId = companyId, ResourceKind = ResourceKind.Machine,
                Code = code, Name = name, Status = status, WorkCenterId = workCenterId,
            };
            _db.ProductionResources.Add(res);
        }
        else
        {
            res.Status = status; res.WorkCenterId = workCenterId; res.ModifiedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return res;
    }

    private async Task EnsureResourceCapabilityAsync(int companyId, int resourceId, int capabilityId,
        CapabilityProficiency proficiency, DateTime? expiresOn, decimal? envelopeValue, CancellationToken ct)
    {
        var rc = await _db.ResourceCapabilities.FirstOrDefaultAsync(
            x => x.ProductionResourceId == resourceId && x.CapabilityId == capabilityId, ct);
        if (rc == null)
        {
            rc = new ResourceCapability
            {
                CompanyId = companyId, ProductionResourceId = resourceId, CapabilityId = capabilityId,
                Proficiency = proficiency, ExpiresOnUtc = expiresOn, EnvelopeValue = envelopeValue,
                QualifiedOnUtc = DateTime.UtcNow.Date, CreatedBy = "CapabilityMatchProbe",
            };
            _db.ResourceCapabilities.Add(rc);
        }
        else
        {
            rc.Proficiency = proficiency; rc.ExpiresOnUtc = expiresOn;
            rc.EnvelopeValue = envelopeValue; rc.IsActive = true; rc.ModifiedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureRequirementAsync(int companyId, int opId, int capabilityId,
        CapabilityRequirementType type, bool mandatory, CapabilityProficiency minProf,
        decimal? envMin, CancellationToken ct)
    {
        var req = await _db.OperationCapabilityRequirements.FirstOrDefaultAsync(
            r => r.RoutingOperationId == opId && r.CapabilityId == capabilityId && r.RequirementType == type, ct);
        if (req == null)
        {
            req = new OperationCapabilityRequirement
            {
                CompanyId = companyId, RoutingOperationId = opId, CapabilityId = capabilityId,
                RequirementType = type, IsMandatory = mandatory, MinProficiency = minProf,
                RequiredEnvelopeMin = envMin, CreatedBy = "CapabilityMatchProbe",
            };
            _db.OperationCapabilityRequirements.Add(req);
        }
        else
        {
            req.IsMandatory = mandatory; req.MinProficiency = minProf;
            req.RequiredEnvelopeMin = envMin; req.ModifiedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    // 1) Build the worked example
    public async Task<IActionResult> OnPostSetupAsync(CancellationToken ct)
    {
        var op = await LatestOpAsync(ct);
        if (op == null) { Set(false, "No RoutingOperation in tenant — seed a routing first."); await LoadStatsAsync(ct); return Page(); }
        var companyId = await OpCompanyAsync(op, ct);

        // Capabilities: 5-axis milling (parameterized, ≥5 axes) + CMM inspection (preferred).
        var mill5 = await EnsureCapabilityAsync(companyId, "MILL-5AX-SIM", "5-axis simultaneous milling",
            CapabilityCategory.Machining, special: false, needsQual: false,
            parameterized: true, uom: "axes", envMin: 5m, std: null, ct);
        var cmm = await EnsureCapabilityAsync(companyId, "INSP-CMM", "CMM in-process inspection",
            CapabilityCategory.Inspection, special: false, needsQual: false,
            parameterized: false, uom: null, envMin: null, std: "AS9102", ct);

        // Op requirements: 5-axis mandatory (envelope ≥5) + CMM preferred (non-mandatory).
        await EnsureRequirementAsync(companyId, op.Id, mill5.Id, CapabilityRequirementType.MachineCapability,
            mandatory: true, CapabilityProficiency.Qualified, envMin: 5m, ct);
        await EnsureRequirementAsync(companyId, op.Id, cmm.Id, CapabilityRequirementType.Inspection,
            mandatory: false, CapabilityProficiency.Qualified, envMin: null, ct);

        // Resources with varied profiles (real machine families). To keep the worked
        // example SELF-CONTAINED regardless of any requirements already on this op
        // (e.g. residue from prior probes), each demo resource is granted EVERY
        // mandatory capability the op currently requires — current + Qualified — and
        // the intended defect is applied ONLY to the 5-axis (MILL) capability. That way
        // A/B come out eligible and C/D fail specifically on the envelope / expiry.
        var future = DateTime.UtcNow.Date.AddMonths(18);
        var past = DateTime.UtcNow.Date.AddDays(-30);

        var mandatoryCapIds = await _db.OperationCapabilityRequirements
            .Where(r => r.RoutingOperationId == op.Id && r.IsMandatory)
            .Select(r => r.CapabilityId).Distinct().ToListAsync(ct);

        // Grant a resource all mandatory caps; the MILL cap gets the per-resource profile.
        async Task GrantAllAsync(int resId, CapabilityProficiency millProf, DateTime? millExpiry, decimal? millEnv)
        {
            foreach (var capId in mandatoryCapIds)
            {
                if (capId == mill5.Id)
                    await EnsureResourceCapabilityAsync(companyId, resId, capId, millProf, millExpiry, millEnv, ct);
                else
                    await EnsureResourceCapabilityAsync(companyId, resId, capId, CapabilityProficiency.Qualified, null, null, ct);
            }
        }

        // A — full 5-axis Expert + CMM, current, on the op's WC → eligible, top rank.
        var a = await EnsureResourceAsync(companyId, "HAAS-UMC750-A", "Haas UMC-750 5-axis (cell A)",
            ProductionResourceStatus.Active, op.WorkCenterId, ct);
        await GrantAllAsync(a.Id, CapabilityProficiency.Expert, null, 5m);
        await EnsureResourceCapabilityAsync(companyId, a.Id, cmm.Id, CapabilityProficiency.Qualified, null, null, ct);

        // B — 5-axis Qualified, current, no CMM → eligible, lower rank.
        var b = await EnsureResourceAsync(companyId, "HAAS-UMC750-B", "Haas UMC-750 5-axis (cell B)",
            ProductionResourceStatus.Active, null, ct);
        await GrantAllAsync(b.Id, CapabilityProficiency.Qualified, null, 5m);

        // C — claims 5-axis but only 3 simultaneous axes (3+2 positional) → ineligible (envelope).
        var c = await EnsureResourceAsync(companyId, "MAZAK-VTC-1", "Mazak VTC-300 (3+2 positional)",
            ProductionResourceStatus.Active, null, ct);
        await GrantAllAsync(c.Id, CapabilityProficiency.Qualified, null, 3m);

        // D — full 5-axis Expert but calibration LAPSED → ineligible (expired).
        var d = await EnsureResourceAsync(companyId, "DECKEL-DMU-1", "DMG DECKEL DMU-50 5-axis",
            ProductionResourceStatus.Active, null, ct);
        await GrantAllAsync(d.Id, CapabilityProficiency.Expert, past, 5m);

        // Z — full 5-axis Expert current but resource Inactive → excluded from candidate pool.
        var z = await EnsureResourceAsync(companyId, "HAAS-UMC750-Z", "Haas UMC-750 5-axis (mothballed)",
            ProductionResourceStatus.Inactive, null, ct);
        await GrantAllAsync(z.Id, CapabilityProficiency.Expert, future, 5m);

        Set(true, $"Worked example ready on op #{op.Id}: requires 5-axis milling (≥5 axes, mandatory) + CMM (preferred); " +
                  $"{mandatoryCapIds.Count} mandatory capability(ies) total. 5 resources seeded (each granted all mandatory caps) — " +
                  "A/B eligible, MAZAK (3 axes) + DECKEL (expired) ineligible on 5-axis, HAAS-Z excluded (Inactive). Now click Run match.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) Run the resolver on the latest op
    public async Task<IActionResult> OnPostRunMatchAsync(CancellationToken ct)
    {
        var op = await LatestOpAsync(ct);
        if (op == null) { Set(false, "No RoutingOperation in tenant — seed a routing first."); await LoadStatsAsync(ct); return Page(); }

        var result = await _match.GetEligibleResourcesAsync(op.Id, asOfUtc: null, ct);
        await LoadStatsAsync(ct);
        if (result.IsFailure) { Set(false, result.Error); return Page(); }

        MatchResult = result.Value;
        Set(true, $"Match on op #{MatchResult!.RoutingOperationId} ({MatchResult.MandatoryRequirementCount} mandatory requirement(s), " +
                  $"{MatchResult.CandidateResourceCount} active candidate(s)): {MatchResult.Eligible.Count} eligible, {MatchResult.Ineligible.Count} ineligible.");
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }
}
