// Theme B11 Wave R3-8 — admin probe for FK-backed operation capability requirements.
// Lock-16 corollary: every button writes.
//
//   1) Require machine capability — latest routing op requires MILL-5AX-SIM (MachineCapability).
//   2) Backfill from CSV          — converts the latest op's RequiredSkillCodes CSV into
//                                    FK-backed LaborSkill requirements (stamps a realistic CSV first if empty).
//   3) Require special process    — latest op requires WELD-AWS-D17.1-AL (SpecialProcess, mandatory).
//   R) Reload
//
// RoutingOperation has no CompanyId — it is tenant-scoped THROUGH Routing.CompanyId.
// The requirement rows carry their own tenant trio. Company-scoped pickers
// (R1-2 Codex lesson). ControlPlaneExempt (writes via AppDbContext).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B11 R3-8 operation capability requirements. " +
    "Reads/writes OperationCapabilityRequirement (+ ensures Capability rows, + backfills " +
    "RoutingOperation CSV) via AppDbContext, tenant-scoped through ITenantContext.VisibleCompanyIds.")]
public sealed class OperationRequirementProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<OperationRequirementProbeModel> _logger;

    public OperationRequirementProbeModel(AppDbContext db, ITenantContext tenant, ILogger<OperationRequirementProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int RequirementCount { get; private set; }
    public int MandatoryCount { get; private set; }
    public int OpsWithReqsCount { get; private set; }
    public int LatestOpId { get; private set; }
    public IReadOnlyList<ReqRow> RequirementSample { get; private set; } = Array.Empty<ReqRow>();

    public sealed record ReqRow(int Id, int RoutingOperationId, string CapabilityCode,
        CapabilityRequirementType Type, CapabilityProficiency MinProficiency, bool IsMandatory,
        int? ToolId, decimal? EnvMin, decimal? EnvMax)
    {
        // Formatted in C# after materialization — never inside the EF projection
        // (decimal?.ToString() does not translate to SQL).
        public string? EnvelopeRange =>
            (EnvMin == null && EnvMax == null) ? null
            : $"{(EnvMin?.ToString() ?? "*")}–{(EnvMax?.ToString() ?? "*")}";
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private int CompanyId() => _tenant.VisibleCompanyIds.FirstOrDefault();

    private IQueryable<OperationCapabilityRequirement> ScopedReqs() =>
        _db.OperationCapabilityRequirements.Where(r => _tenant.VisibleCompanyIds.Contains(r.CompanyId));

    // RoutingOperation is tenant-scoped through its Routing's CompanyId.
    private IQueryable<RoutingOperation> ScopedOps() =>
        from op in _db.RoutingOperations
        join r in _db.Routings on op.RoutingId equals r.Id
        where _tenant.VisibleCompanyIds.Contains(r.CompanyId)
        select op;

    private Task<RoutingOperation?> LatestOpAsync(CancellationToken ct) =>
        ScopedOps().OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        RequirementCount = await ScopedReqs().CountAsync(ct);
        MandatoryCount = await ScopedReqs().CountAsync(r => r.IsMandatory, ct);
        OpsWithReqsCount = await ScopedReqs().Select(r => r.RoutingOperationId).Distinct().CountAsync(ct);
        LatestOpId = (await LatestOpAsync(ct))?.Id ?? 0;

        RequirementSample = await ScopedReqs()
            .OrderByDescending(r => r.Id).Take(10)
            .Select(r => new ReqRow(r.Id, r.RoutingOperationId, r.Capability!.Code,
                r.RequirementType, r.MinProficiency, r.IsMandatory, r.ToolId,
                r.RequiredEnvelopeMin, r.RequiredEnvelopeMax))
            .ToListAsync(ct);
    }

    private async Task<Capability> EnsureCapabilityAsync(int companyId, string code, string name,
        CapabilityCategory category, bool special, bool needsQual, string? std, CancellationToken ct)
    {
        var existing = await _db.Capabilities
            .Where(c => c.CompanyId == companyId && c.Code == code)
            .OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (existing != null) return existing;
        var cap = new Capability
        {
            CompanyId = companyId, Category = category, Code = code, Name = name,
            IsSpecialProcess = special, RequiresQualification = needsQual,
            DefaultQualificationValidityMonths = needsQual ? 24 : (int?)null,
            GoverningStandard = std, CreatedBy = "OperationRequirementProbe",
        };
        _db.Capabilities.Add(cap);
        await _db.SaveChangesAsync(ct);
        return cap;
    }

    // 1) Latest op REQUIRES the 5-axis milling capability (machine-side)
    public async Task<IActionResult> OnPostRequireMachineAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var op = await LatestOpAsync(ct);
        if (op == null) { Set(false, "No RoutingOperation in tenant — seed a routing first."); await LoadStatsAsync(ct); return Page(); }

        var cap = await EnsureCapabilityAsync(companyId, "MILL-5AX-SIM", "5-axis simultaneous milling",
            CapabilityCategory.Machining, special: false, needsQual: false, std: null, ct);

        var existing = await ScopedReqs().FirstOrDefaultAsync(r =>
            r.RoutingOperationId == op.Id && r.CapabilityId == cap.Id &&
            r.RequirementType == CapabilityRequirementType.MachineCapability, ct);
        if (existing != null) { Set(true, $"Op #{op.Id} already requires {cap.Code} (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var req = new OperationCapabilityRequirement
        {
            CompanyId = companyId,
            RoutingOperationId = op.Id,
            CapabilityId = cap.Id,
            RequirementType = CapabilityRequirementType.MachineCapability,
            MinProficiency = CapabilityProficiency.Qualified,
            IsMandatory = true,
            RequiredEnvelopeMin = 5m,    // needs a resource achieving the full 5 axes
            Notes = "Op requires a 5-axis-capable machine.",
            CreatedBy = "OperationRequirementProbe",
        };
        _db.OperationCapabilityRequirements.Add(req);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Op #{op.Id} now REQUIRES {cap.Code} (machine capability, mandatory, envelope ≥5 axes). " +
                  "R3-9 will return only 5-axis-capable resources as eligible for this op.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) Backfill the latest op's RequiredSkillCodes CSV into FK-backed LaborSkill requirements
    public async Task<IActionResult> OnPostBackfillCsvAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var op = await LatestOpAsync(ct);
        if (op == null) { Set(false, "No RoutingOperation in tenant — seed a routing first."); await LoadStatsAsync(ct); return Page(); }

        // Stamp a realistic legacy CSV if the op has none, so the conversion path is demonstrable.
        var stamped = false;
        if (string.IsNullOrWhiteSpace(op.RequiredSkillCodes))
        {
            op.RequiredSkillCodes = "CNC_OP, FANUC_PROG";
            op.ModifiedAt = DateTime.UtcNow;
            stamped = true;
        }

        var codes = (op.RequiredSkillCodes ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();

        var created = 0;
        foreach (var code in codes)
        {
            // Labor skills aren't machine capabilities — categorize neutrally (Other).
            var cap = await EnsureCapabilityAsync(companyId, code, code.Replace('_', ' '),
                CapabilityCategory.Other, special: false, needsQual: false, std: null, ct);
            var dup = await ScopedReqs().FirstOrDefaultAsync(r =>
                r.RoutingOperationId == op.Id && r.CapabilityId == cap.Id &&
                r.RequirementType == CapabilityRequirementType.LaborSkill, ct);
            if (dup != null) continue;
            _db.OperationCapabilityRequirements.Add(new OperationCapabilityRequirement
            {
                CompanyId = companyId,
                RoutingOperationId = op.Id,
                CapabilityId = cap.Id,
                RequirementType = CapabilityRequirementType.LaborSkill,
                MinProficiency = CapabilityProficiency.Qualified,
                IsMandatory = true,
                Notes = $"Backfilled from RoutingOperation.RequiredSkillCodes CSV ('{code}').",
                CreatedBy = "OperationRequirementProbe",
            });
            created++;
        }
        await _db.SaveChangesAsync(ct);

        var stampNote = stamped ? " (stamped a sample CSV 'CNC_OP, FANUC_PROG' first — op had none)" : "";
        Set(true, $"Backfilled {created} LaborSkill requirement(s) on op #{op.Id} from its RequiredSkillCodes CSV{stampNote}. " +
                  "The CSV stays readable; the FK rows are now the source of truth.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) Latest op REQUIRES the AWS D17.1 special process (must be a qualified, in-cert resource)
    public async Task<IActionResult> OnPostRequireSpecialProcessAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var op = await LatestOpAsync(ct);
        if (op == null) { Set(false, "No RoutingOperation in tenant — seed a routing first."); await LoadStatsAsync(ct); return Page(); }

        var cap = await EnsureCapabilityAsync(companyId, "WELD-AWS-D17.1-AL", "AWS D17.1 aluminum TIG welding",
            CapabilityCategory.Welding, special: true, needsQual: true, std: "AWS D17.1", ct);

        var existing = await ScopedReqs().FirstOrDefaultAsync(r =>
            r.RoutingOperationId == op.Id && r.CapabilityId == cap.Id &&
            r.RequirementType == CapabilityRequirementType.SpecialProcess, ct);
        if (existing != null) { Set(true, $"Op #{op.Id} already requires {cap.Code} (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var req = new OperationCapabilityRequirement
        {
            CompanyId = companyId,
            RoutingOperationId = op.Id,
            CapabilityId = cap.Id,
            RequirementType = CapabilityRequirementType.SpecialProcess,
            MinProficiency = CapabilityProficiency.Qualified,
            IsMandatory = true,
            Notes = "AS9100 special process — only currently-certified resources are eligible.",
            CreatedBy = "OperationRequirementProbe",
        };
        _db.OperationCapabilityRequirements.Add(req);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Op #{op.Id} now REQUIRES {cap.Code} (special process, mandatory). " +
                  "R3-9 will exclude any resource whose AWS D17.1 cert is expired — currency is enforced.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
