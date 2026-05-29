// Theme B11 Wave R3-7 — admin probe for the capability model.
// Lock-16 corollary: every button writes.
//
//   1) Create capability       — a Capability master (5-axis simultaneous milling).
//   2) Create special process   — a special-process Capability w/ a validity default (AWS D17.1 Al TIG).
//   3) Attach (no expiry)        — a ResourceCapability on the latest resource for a geometric capability.
//   4) Attach (with expiry)      — a ResourceCapability for the special process, cert dated + expiring.
//   R) Reload
//
// Buttons 3/4 prove the two qualification paths: a machine's geometry never
// lapses (ExpiresOnUtc null), a welder's cert does (ExpiresOnUtc set). This is
// the data the R3-9 match service filters on.
//
// Company-scoped pickers (R1-2 Codex lesson). ControlPlaneExempt (writes via AppDbContext).

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
    "Admin diagnostic probe for Theme B11 R3-7 capability model. " +
    "Reads/writes Capability + ResourceCapability via AppDbContext, " +
    "tenant-scoped through ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class CapabilityProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CapabilityProbeModel> _logger;

    public CapabilityProbeModel(AppDbContext db, ITenantContext tenant, ILogger<CapabilityProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int CapabilityCount { get; private set; }
    public int SpecialProcessCount { get; private set; }
    public int ResourceCapabilityCount { get; private set; }
    public int ExpiringCount { get; private set; }
    public IReadOnlyList<CapRow> CapabilitySample { get; private set; } = Array.Empty<CapRow>();
    public IReadOnlyList<RcRow> ResourceCapabilitySample { get; private set; } = Array.Empty<RcRow>();

    public sealed record CapRow(int Id, string Code, string Name, CapabilityCategory Category,
        bool IsSpecialProcess, bool RequiresQualification, int? ValidityMonths, string? Standard);
    public sealed record RcRow(int Id, int ResourceId, string ResourceCode, string CapabilityCode,
        CapabilityProficiency Proficiency, DateTime? QualifiedOnUtc, DateTime? ExpiresOnUtc,
        string? CertificateReference, bool Current);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private int CompanyId() => _tenant.VisibleCompanyIds.FirstOrDefault();

    private IQueryable<Capability> ScopedCaps() =>
        _db.Capabilities.Where(c => _tenant.VisibleCompanyIds.Contains(c.CompanyId));

    private IQueryable<ResourceCapability> ScopedRcs() =>
        _db.ResourceCapabilities.Where(x => _tenant.VisibleCompanyIds.Contains(x.CompanyId));

    private Task<ProductionResource?> LatestResAsync(int companyId, CancellationToken ct) =>
        _db.ProductionResources.Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        CapabilityCount = await ScopedCaps().CountAsync(ct);
        SpecialProcessCount = await ScopedCaps().CountAsync(c => c.IsSpecialProcess, ct);
        ResourceCapabilityCount = await ScopedRcs().CountAsync(ct);
        ExpiringCount = await ScopedRcs().CountAsync(x => x.ExpiresOnUtc != null, ct);

        CapabilitySample = await ScopedCaps()
            .OrderByDescending(c => c.Id).Take(10)
            .Select(c => new CapRow(c.Id, c.Code, c.Name, c.Category,
                c.IsSpecialProcess, c.RequiresQualification, c.DefaultQualificationValidityMonths, c.GoverningStandard))
            .ToListAsync(ct);

        ResourceCapabilitySample = await ScopedRcs()
            .OrderByDescending(x => x.Id).Take(10)
            .Select(x => new RcRow(x.Id, x.ProductionResourceId,
                x.ProductionResource!.Code, x.Capability!.Code,
                x.Proficiency, x.QualifiedOnUtc, x.ExpiresOnUtc, x.CertificateReference,
                x.IsActive && (x.ExpiresOnUtc == null || x.ExpiresOnUtc > now)))
            .ToListAsync(ct);
    }

    // Resolve a capability by code for the current company (used to attach without re-creating).
    private Task<Capability?> CapByCodeAsync(int companyId, string code, CancellationToken ct) =>
        _db.Capabilities.Where(c => c.CompanyId == companyId && c.Code == code)
            .OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);

    private async Task<Capability> EnsureCapabilityAsync(int companyId, int? siteId, Capability template, CancellationToken ct)
    {
        var existing = await CapByCodeAsync(companyId, template.Code, ct);
        if (existing != null) return existing;
        template.CompanyId = companyId;
        template.SiteId = siteId;
        _db.Capabilities.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    // 1) CREATE a geometric (non-lapsing) capability
    public async Task<IActionResult> OnPostCreateCapabilityAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var existing = await CapByCodeAsync(companyId, "MILL-5AX-SIM", ct);
        if (existing != null) { Set(true, $"Capability MILL-5AX-SIM already exists (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var cap = new Capability
        {
            CompanyId = companyId,
            Category = CapabilityCategory.Machining,
            Code = "MILL-5AX-SIM",
            Name = "5-axis simultaneous milling",
            Description = "Full 5-axis simultaneous contouring on a trunnion/UMC platform (e.g. Haas UMC-750).",
            IsSpecialProcess = false,
            RequiresQualification = false,   // geometric — a 5-axis machine is always 5-axis
            IsParameterized = true,
            EnvelopeUom = "axes",
            EnvelopeMin = 5m,
            EnvelopeMax = 5m,
            CreatedBy = "CapabilityProbe",
        };
        _db.Capabilities.Add(cap);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created capability #{cap.Id} ({cap.Code} — {cap.Name}). Geometric capability: no qualification lapse. " +
                  "A routing op can now REQUIRE this (R3-8) and any 5-axis resource will match (R3-9).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) CREATE an AS9100/NADCAP special-process capability with a cert validity default
    public async Task<IActionResult> OnPostCreateSpecialProcessAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var existing = await CapByCodeAsync(companyId, "WELD-AWS-D17.1-AL", ct);
        if (existing != null) { Set(true, $"Capability WELD-AWS-D17.1-AL already exists (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var cap = new Capability
        {
            CompanyId = companyId,
            Category = CapabilityCategory.Welding,
            Code = "WELD-AWS-D17.1-AL",
            Name = "AWS D17.1 aluminum TIG welding",
            Description = "Aerospace fusion welding of aluminum per AWS D17.1 — a controlled special process.",
            IsSpecialProcess = true,
            RequiresQualification = true,                 // a welder's cert is mandatory and lapses
            DefaultQualificationValidityMonths = 24,      // AWS continuity / requal cadence
            GoverningStandard = "AWS D17.1",
            CreatedBy = "CapabilityProbe",
        };
        _db.Capabilities.Add(cap);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Created special-process capability #{cap.Id} ({cap.Code} — {cap.Name}, std {cap.GoverningStandard}). " +
                  $"Holders need a dated cert; default validity {cap.DefaultQualificationValidityMonths} months. " +
                  "The R3-9 match service treats currency as MANDATORY for special processes.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) ATTACH a geometric capability to the latest resource — no expiry
    public async Task<IActionResult> OnPostAttachNoExpiryAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        // Ensure the geometric capability exists (auto-create so the button is self-sufficient).
        var cap = await EnsureCapabilityAsync(companyId, res.SiteId, new Capability
        {
            Category = CapabilityCategory.Machining,
            Code = "MILL-5AX-SIM",
            Name = "5-axis simultaneous milling",
            Description = "Full 5-axis simultaneous contouring on a trunnion/UMC platform.",
            IsParameterized = true, EnvelopeUom = "axes", EnvelopeMin = 5m, EnvelopeMax = 5m,
            CreatedBy = "CapabilityProbe",
        }, ct);

        var existing = await ScopedRcs().FirstOrDefaultAsync(
            x => x.ProductionResourceId == res.Id && x.CapabilityId == cap.Id, ct);
        if (existing != null) { Set(true, $"Resource #{res.Id} ({res.Code}) already holds {cap.Code} (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var rc = new ResourceCapability
        {
            CompanyId = companyId,
            SiteId = res.SiteId,
            ProductionResourceId = res.Id,
            CapabilityId = cap.Id,
            Proficiency = CapabilityProficiency.Expert,
            QualifiedOnUtc = null,            // geometric — capable since inception
            ExpiresOnUtc = null,              // never expires
            EnvelopeValue = 5m,               // achieves the full 5 axes
            QualifiedBy = "OEM acceptance test",
            Notes = "Geometric capability — does not lapse.",
            CreatedBy = "CapabilityProbe",
        };
        _db.ResourceCapabilities.Add(rc);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Attached {cap.Code} to resource #{res.Id} ({res.Code}) — no expiry (geometric). " +
                  "This resource is permanently eligible for ops requiring 5-axis milling.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) ATTACH a special-process capability to the latest resource — dated cert that expires
    public async Task<IActionResult> OnPostAttachWithExpiryAsync(CancellationToken ct)
    {
        var companyId = CompanyId();
        if (companyId == 0) { Set(false, "No tenant-visible company."); await LoadStatsAsync(ct); return Page(); }

        var res = await LatestResAsync(companyId, ct);
        if (res == null) { Set(false, "No ProductionResource yet — run the R2-4/R2-5 probes first."); await LoadStatsAsync(ct); return Page(); }

        var cap = await EnsureCapabilityAsync(companyId, res.SiteId, new Capability
        {
            Category = CapabilityCategory.Welding,
            Code = "WELD-AWS-D17.1-AL",
            Name = "AWS D17.1 aluminum TIG welding",
            Description = "Aerospace fusion welding of aluminum per AWS D17.1 — a controlled special process.",
            IsSpecialProcess = true, RequiresQualification = true,
            DefaultQualificationValidityMonths = 24, GoverningStandard = "AWS D17.1",
            CreatedBy = "CapabilityProbe",
        }, ct);

        var existing = await ScopedRcs().FirstOrDefaultAsync(
            x => x.ProductionResourceId == res.Id && x.CapabilityId == cap.Id, ct);
        if (existing != null) { Set(true, $"Resource #{res.Id} ({res.Code}) already holds {cap.Code} (#{existing.Id})."); await LoadStatsAsync(ct); return Page(); }

        var qualifiedOn = DateTime.UtcNow.Date;
        var months = cap.DefaultQualificationValidityMonths ?? 24;
        var rc = new ResourceCapability
        {
            CompanyId = companyId,
            SiteId = res.SiteId,
            ProductionResourceId = res.Id,
            CapabilityId = cap.Id,
            Proficiency = CapabilityProficiency.Qualified,
            QualifiedOnUtc = qualifiedOn,
            ExpiresOnUtc = qualifiedOn.AddMonths(months),   // cert lapses on the validity cadence
            QualifiedBy = "NADCAP-accredited examiner",
            CertificateReference = "AWS-D17.1-2026-0417",
            Notes = $"Special-process qualification, valid {months} months.",
            CreatedBy = "CapabilityProbe",
        };
        _db.ResourceCapabilities.Add(rc);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Attached {cap.Code} to resource #{res.Id} ({res.Code}) — qualified {qualifiedOn:yyyy-MM-dd}, " +
                  $"expires {rc.ExpiresOnUtc:yyyy-MM-dd} (cert {rc.CertificateReference}). " +
                  "Once past expiry, R3-9 drops this resource from the eligible set automatically.");
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
