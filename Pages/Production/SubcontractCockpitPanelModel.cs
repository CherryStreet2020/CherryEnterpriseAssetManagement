// Sprint 15.2 PR-9 — view-model bag for _CockpitSubcontractPanel.cshtml.
//
// The partial expects this shape so it can render the BIC-differentiator
// per-op view. The admin probe + the future Cockpit drawer wire-up
// (PR-9 follow-up) populate this model.

using System.Collections.Generic;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;

namespace Abs.FixedAssets.Pages.Production;

public sealed class SubcontractCockpitPanelModel
{
    public required SubcontractOperation Op { get; init; }
    public required string SupplierName { get; init; }
    public FlowStateSummary? FlowState { get; init; }
    public SubcontractCostSummary? CostSummary { get; init; }
    public IReadOnlyList<SubcontractValidationResult>? Validations { get; init; }
}
