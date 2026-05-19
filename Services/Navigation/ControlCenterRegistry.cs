using System.Collections.Generic;
using Abs.FixedAssets.Models.Navigation;

namespace Abs.FixedAssets.Services.Navigation;

// ADR-017 §D2 — registry of the v1 launch-scope Control Centers.
//
// Order is intentional: 1 live (Receiving) at top, then 6 placeholders for
// Sprints 12-18 in sprint order. The sidebar shape is stable across the
// next five sprints — each shipping sprint flips its line from
// IsLive=false to IsLive=true (one-line edit per sprint), no IA churn.
//
// v2 post-launch Control Centers (Quality, AP/AR, HR/Crew) are intentionally
// NOT in this list per Dean's 2026-05-18 launch-scope lock — CherryAI v1 is
// EAM + Manufacturing Operations disruption that CHANNELS INTO the customer's
// existing financial ERP, not a full-ERP replacement. See:
//   memory/project_launch_scope_and_positioning.md
//   docs/ADR-017-control-center-first-information-architecture.md §D2, §D3
public static class ControlCenterRegistry
{
    public static IReadOnlyList<ControlCenterDescriptor> All { get; } = new[]
    {
        new ControlCenterDescriptor(
            Code: "RECEIVING",
            Title: "Receiving",
            Route: "/Receiving",
            IconClass: "fas fa-truck-ramp-box",
            SprintNumber: 11,
            IsLive: true,
            StatusChip: "live"),

        new ControlCenterDescriptor(
            Code: "PURCHASING",
            Title: "Purchasing",
            Route: "/Purchasing/ControlCenter",
            IconClass: "fas fa-file-invoice",
            SprintNumber: 12,
            IsLive: false,
            StatusChip: "soon · Sprint 12"),

        new ControlCenterDescriptor(
            Code: "MAINTENANCE",
            Title: "Maintenance",
            Route: "/Maintenance/ControlCenter",
            IconClass: "fas fa-wrench",
            SprintNumber: 13,
            IsLive: false,
            StatusChip: "soon · Sprint 13"),

        new ControlCenterDescriptor(
            Code: "PLANNING",
            Title: "Planning",
            Route: "/Planning/ControlCenter",
            IconClass: "fas fa-chart-line",
            SprintNumber: 14,
            IsLive: false,
            StatusChip: "soon · Sprint 14"),

        new ControlCenterDescriptor(
            Code: "SCHEDULING",
            Title: "Scheduling",
            Route: "/Scheduling/ControlCenter",
            IconClass: "fas fa-calendar-days",
            SprintNumber: 15,
            IsLive: false,
            StatusChip: "soon · Sprint 15"),

        new ControlCenterDescriptor(
            Code: "INVENTORY",
            Title: "Inventory",
            Route: "/Inventory/ControlCenter",
            IconClass: "fas fa-warehouse",
            SprintNumber: 16,
            IsLive: false,
            StatusChip: "soon · Sprint 16"),

        new ControlCenterDescriptor(
            Code: "SHIPPING",
            Title: "Shipping",
            Route: "/Shipping/ControlCenter",
            IconClass: "fas fa-paper-plane",
            SprintNumber: 18,
            IsLive: false,
            StatusChip: "soon · Sprint 18"),
    };
}
