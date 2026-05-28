// Sprint 15.2 PR-9 — admin probe demonstrating _CockpitSubcontractPanel partial.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. AppDbContext used for tenant-scoped reads. All validation logic flows through ISubcontractValidationService + ISubcontractFlowService + ISubcontractCostingService.")]
public sealed class SubcontractCockpitPanelProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubcontractFlowService _flow;
    private readonly ISubcontractCostingService _costing;
    private readonly ISubcontractValidationService _validation;
    private readonly ILogger<SubcontractCockpitPanelProbeModel> _logger;

    public SubcontractCockpitPanelProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        ISubcontractFlowService flow,
        ISubcontractCostingService costing,
        ISubcontractValidationService validation,
        ILogger<SubcontractCockpitPanelProbeModel> logger)
    {
        _db = db;
        _tenant = tenant;
        _flow = flow;
        _costing = costing;
        _validation = validation;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)] public int OpId { get; set; } = 1;

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public SubcontractCockpitPanelModel? PanelData { get; private set; }

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadPanelAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadAsync(CancellationToken ct)
    {
        await LoadPanelAsync(ct);
        return Page();
    }

    private async Task LoadPanelAsync(CancellationToken ct)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Include(s => s.Supplier)
            .Where(s => s.Id == OpId && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
        {
            Set(false, $"SubcontractOperation #{OpId} not found or out of tenant scope.");
            return;
        }

        var flow = await _flow.GetFlowStateAsync(op.Id, ct);
        var costs = await _costing.GetCostSummaryAsync(op.Id, ct);
        var rules = await _validation.RunAllAsync(op.Id, ct);

        PanelData = new SubcontractCockpitPanelModel
        {
            Op = op,
            SupplierName = op.Supplier?.Name ?? $"Supplier #{op.SupplierId?.ToString() ?? "—"}",
            FlowState = flow.IsSuccess ? flow.Value : null,
            CostSummary = costs.IsSuccess ? costs.Value : null,
            Validations = rules.IsSuccess ? rules.Value : null,
        };

        var ruleCounts = rules.IsSuccess && rules.Value != null
            ? $"{rules.Value.Count(r => r.Outcome == SubcontractValidationOutcome.Pass)} Pass / " +
              $"{rules.Value.Count(r => r.Outcome == SubcontractValidationOutcome.Warn)} Warn / " +
              $"{rules.Value.Count(r => r.Outcome == SubcontractValidationOutcome.Block)} Block"
            : "n/a";
        Set(true, $"Loaded subcontract panel for op #{op.Id} (PRO #{op.ProductionOrderId}, seq {op.OperationSequence}). §24 rules: {ruleCounts}.");
    }
}
