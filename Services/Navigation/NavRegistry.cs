using System.Collections.Generic;
using Abs.FixedAssets.Models.Navigation;

namespace Abs.FixedAssets.Services.Navigation;

// Sprint 13.5 PR #4 — unified nav registry.
//
// Single source-of-truth for every sidebar item OUTSIDE the Control Center
// spine. Replaces the ~200 lines of hard-coded HTML that used to live in
// `Pages/Shared/_ModernLayout.cshtml` for Assets / Finance / Materials /
// Work Management / Projects / AI etc.  Adding a nav item is now a one-line
// edit to this file — no Razor changes.
//
// Five top-level groups, in the order they appear in the sidebar:
//
//   1. Today        — what needs YOUR attention right now (dashboard,
//                     approvals, alerts).  Always at the top.
//   2. Operations   — the active work.  Cockpits live here (handled by
//                     ControlCenterRegistry, rendered separately).  This
//                     group surfaces the OTHER day-to-day pages that
//                     aren't Cockpits yet.
//   3. Insights     — reports, analytics, dashboards.
//   4. Master Data  — the reference data home.  One stop for Items /
//                     Vendors / Customers / Countries / WorkCalendars /
//                     Carriers / etc.  Previously scattered across
//                     /Admin (31 pages, no grouping).
//   5. Settings     — system admin, integrations, users.
//
// Disabled / not-yet-shipped control centers are HIDDEN from the sidebar
// (PR #4 design call — visible-but-disabled created visual noise per Dean's
// "menu is a mess" complaint).  When a control center ships, flip its
// IsLive in ControlCenterRegistry and it auto-appears.
//
// Cross-refs:
//   - docs/ADR-017-control-center-first-information-architecture.md
//   - docs/research/luxury-cockpit-ux.md
//   - Models/Navigation/NavGroup.cs
public static class NavRegistry
{
    public static IReadOnlyList<NavGroup> All { get; } = new[]
    {
        // ============================================================
        // 1. TODAY — what needs your attention
        // ============================================================
        new NavGroup(
            Code: "TODAY",
            Title: "Today",
            IconClass: "fas fa-sun",
            SortOrder: 10,
            DefaultExpanded: true,
            Items: new[]
            {
                new NavItem("Dashboard",      "/Index",      "fas fa-gauge-high", RoutePrefix: "/Index"),
                new NavItem("Approvals",      "/Approvals",  "fas fa-circle-check"),
                new NavItem("Notifications",  "/Notifications", "fas fa-bell"),
            }),

        // ============================================================
        // 2. OPERATIONS — daily-work pages that aren't Cockpits yet
        //
        // The Cockpit spine (Receiving / Production / Customer Projects /
        // Purchasing / Maintenance / etc.) is rendered ABOVE this section
        // by _NavControlCenters.cshtml from ControlCenterRegistry.  This
        // group surfaces day-to-day pages outside that spine.
        // ============================================================
        new NavGroup(
            Code: "OPERATIONS",
            Title: "Operations",
            IconClass: "fas fa-bolt",
            SortOrder: 20,
            Items: new[]
            {
                new NavItem("Work Orders",         "/WorkOrders",    "fas fa-wrench"),
                new NavItem("Maintenance Schedule","/Maintenance/Schedule", "fas fa-calendar-days", RoutePrefix: "/Maintenance"),
                new NavItem("Work Requests",       "/Maintenance/WorkRequests", "fas fa-clipboard-list"),
                new NavItem("Assets",              "/Assets",        "fas fa-boxes-stacked"),
                new NavItem("Plant Floor",         "/Plant/Floor",   "fas fa-industry"),
                new NavItem("Bulk Operations",     "/BulkOperations","fas fa-layer-group"),
            }),

        // ============================================================
        // 3. FINANCE — books, journals, periods, AP
        //
        // Kept as a separate group rather than folded into Operations —
        // accountant/controller users live here and have different
        // mental models.  Future v1.1 may add a "Books" Control Center.
        // ============================================================
        new NavGroup(
            Code: "FINANCE",
            Title: "Finance",
            IconClass: "fas fa-calculator",
            SortOrder: 25,
            Items: new[]
            {
                new NavItem("General Ledger",     "/Books",          "fas fa-book"),
                new NavItem("Journals",           "/Journals",       "fas fa-pen-to-square"),
                new NavItem("Accounts Payable",   "/AccountsPayable","fas fa-file-invoice-dollar"),
                new NavItem("Periods",            "/Periods",        "fas fa-calendar-check"),
                new NavItem("CIP",                "/CIP",            "fas fa-hammer", RoutePrefix: "/CIP"),
                new NavItem("CCA",                "/CCA",            "fas fa-percent"),
                new NavItem("US Tax",             "/UsTax",          "fas fa-landmark"),
            }),

        // ============================================================
        // 4. INSIGHTS — reports + analytics
        // ============================================================
        new NavGroup(
            Code: "INSIGHTS",
            Title: "Insights",
            IconClass: "fas fa-chart-pie",
            SortOrder: 30,
            Items: new[]
            {
                new NavItem("Reports",        "/Reports",      "fas fa-file-chart-column"),
                new NavItem("Audit Log",      "/Admin/AuditLog", "fas fa-clipboard-check"),
            }),

        // ============================================================
        // 5. MASTER DATA — the consolidated reference-data home
        //
        // Previously scattered: Vendors lived under /Admin/Vendors,
        // Items under /Materials, Customers nowhere obvious.  Now ONE
        // stop for every reference-data surface.  PRA-1 / PRA-2 add
        // Carriers / Countries / Subdivisions / WorkCalendars /
        // Holidays — all surfaced here.
        // ============================================================
        new NavGroup(
            Code: "MASTER_DATA",
            Title: "Master Data",
            IconClass: "fas fa-database",
            SortOrder: 40,
            Items: new[]
            {
                new NavItem("Items",           "/Materials",                "fas fa-cubes", RoutePrefix: "/Materials"),
                new NavItem("Item Categories", "/Admin/ItemCategories",     "fas fa-tags"),
                new NavItem("Customers",       "/Admin/Customers",          "fas fa-user-tie", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Vendors",         "/Admin/Vendors",            "fas fa-truck-front"),
                new NavItem("Manufacturers",   "/Admin/Manufacturers",      "fas fa-helmet-safety"),
                new NavItem("Carriers",        "/Admin/Carriers",           "fas fa-truck", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Countries",       "/Admin/Countries",          "fas fa-globe", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Work Calendars",  "/Admin/WorkCalendars",      "fas fa-calendar", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Work Centers",    "/Admin/WorkCenters",        "fas fa-industry", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Routings",        "/Admin/Routings",           "fas fa-route", Badge: "new", BadgeClass: "nav-badge--new"),
                new NavItem("Locations",       "/Admin/Locations",          "fas fa-location-dot"),
                new NavItem("GL Accounts",     "/Admin/GlAccounts",         "fas fa-list-ol"),
                new NavItem("Numbering",       "/Admin/NumberSequences",    "fas fa-hashtag"),
            }),

        // ============================================================
        // 6. AI & INTEGRATIONS
        // ============================================================
        new NavGroup(
            Code: "AI_INTEGRATIONS",
            Title: "AI & Integrations",
            IconClass: "fas fa-brain",
            SortOrder: 45,
            Items: new[]
            {
                new NavItem("AI Workspace",   "/AI",                     "fas fa-robot"),
                new NavItem("API Reference",  "/API",                    "fas fa-code"),
                new NavItem("Webhooks",       "/Admin/Webhooks",         "fas fa-bolt"),
                new NavItem("Integrations",   "/Admin/Integrations",     "fas fa-plug"),
            }),

        // ============================================================
        // 7. SETTINGS — admin, users, system config
        // ============================================================
        new NavGroup(
            Code: "SETTINGS",
            Title: "Settings",
            IconClass: "fas fa-gear",
            SortOrder: 50,
            Items: new[]
            {
                new NavItem("Companies",       "/Admin/Companies",       "fas fa-building"),
                new NavItem("Users",           "/Admin/Users",           "fas fa-users"),
                new NavItem("Roles",           "/Admin/Roles",           "fas fa-user-shield"),
                new NavItem("System Settings", "/Admin/SystemSettings",  "fas fa-sliders"),
                new NavItem("Help",            "/Help",                  "fas fa-circle-question"),
            }),
    };
}
