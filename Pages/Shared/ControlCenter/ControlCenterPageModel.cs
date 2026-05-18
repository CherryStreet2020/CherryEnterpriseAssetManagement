using System.Collections.Generic;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.Primitives;

namespace Abs.FixedAssets.Pages.Shared.ControlCenter;

// ADR-016 §D1 + ADR-014 §D1 — base PageModel for any Control Center page.
//
// Composition hierarchy:
//
//     PageModel                                      (ASP.NET Core)
//      └── VoiceReadyPageModel                       (ADR-014 D1 — voice context)
//           └── ControlCenterPageModel                (ADR-016 D1 — four quadrants)
//                ├── ReceivingControlCenterModel      (Sprint 11 PR #5)
//                ├── PurchasingControlCenterModel     (Sprint 12)
//                ├── MaintenanceControlCenterModel    (Sprint 13)
//                ├── PlanningControlCenterModel       (Sprint 14)
//                ├── SchedulingControlCenterModel     (Sprint 15)
//                ├── InventoryControlCenterModel      (Sprint 16)
//                ├── QualityControlCenterModel        (Sprint 17)
//                ├── ShippingControlCenterModel       (Sprint 18)
//                ├── ApArControlCenterModel           (Sprint 19)
//                └── HrCrewControlCenterModel         (Sprint 20)
//
// Each concrete subclass:
//   1. Implements ControlCenterCode (e.g. "RECEIVING") and ControlCenterTitle.
//   2. Populates Shell.KpiStrip, Shell.ExceptionLane, Shell.Drawer, Shell.ActivityFeed in OnGet.
//   3. May override BuildContextPayload() to add ControlCenter-specific keys
//      to the voice-AI context (entity in focus, current filter, etc).
//
// The Razor page renders Shell via the @await Html.PartialAsync("_ControlCenterShell", Model.Shell) call.
public abstract class ControlCenterPageModel : VoiceReadyPageModel
{
    /// <summary>
    /// Short uppercase code identifying this Control Center
    /// (e.g. "RECEIVING", "PURCHASING"). Surfaced to voice context.
    /// </summary>
    public abstract string ControlCenterCode { get; }

    /// <summary>
    /// Display title for this Control Center (e.g. "Receiving Control Center").
    /// </summary>
    public abstract string ControlCenterTitle { get; }

    /// <summary>
    /// The four-quadrant shell. Subclasses populate this in OnGet.
    /// </summary>
    public ControlCenterShellModel Shell { get; set; } = new();

    /// <summary>
    /// Adds Control-Center identity (code) to the voice context as a default
    /// when the subclass hasn't already set an entity in focus. Subclasses
    /// usually override to set EntityType / EntityId to the receipt / WO /
    /// PR / batch the user is looking at; this default is the fallback so
    /// the AI knows which Control Center it's in even when no row is
    /// focused.
    ///
    /// VoiceContextPayload uses init-only properties (ADR-014 D1), so this
    /// override constructs a new payload by copying the base values and
    /// overriding the entity fields.
    /// </summary>
    public override VoiceContextPayload BuildContextPayload()
    {
        var baseCtx = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route        = baseCtx.Route,
            UserId       = baseCtx.UserId,
            Roles        = baseCtx.Roles,
            TenantId     = baseCtx.TenantId,
            EntityType   = baseCtx.EntityType ?? "ControlCenter",
            EntityId     = baseCtx.EntityId   ?? ControlCenterCode,
            RelatedIds   = baseCtx.RelatedIds,
            FocusedField = baseCtx.FocusedField,
            Tab          = baseCtx.Tab,
            BuiltAt      = baseCtx.BuiltAt,
        };
    }
}
