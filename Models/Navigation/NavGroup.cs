using System.Collections.Generic;

namespace Abs.FixedAssets.Models.Navigation;

// Sprint 13.5 PR #4 — unified nav group descriptor.
//
// Companion to ControlCenterRegistry. ControlCenterRegistry remains the
// single source of truth for the Cockpits (live + future). NavRegistry
// captures EVERY OTHER sidebar item — Today / Operations satellites /
// Insights / Master Data / Settings — in one place instead of being
// hard-coded across 200+ lines of _ModernLayout.cshtml.
//
// Why a separate registry from ControlCenterRegistry:
//   The Control Center spine is a stable Q3-2026 contract (ADR-017 §D2)
//   and ships as a horizontal — flipping IsLive is the deploy event.
//   Other nav items churn more (a Reports menu item move shouldn't
//   require touching ADR-017 data). Two registries, one render path.
//
// Cross-refs:
//   - docs/ADR-017-control-center-first-information-architecture.md
//   - docs/research/luxury-cockpit-ux.md §4 (Cherry Bar + sidebar shape)

/// <summary>
/// One top-level group in the sidebar (e.g. "Today", "Operations",
/// "Master Data"). Renders as a collapsible section with header + icon
/// + items. The active state expands on page-load if any item route
/// matches the current page.
/// </summary>
public sealed record NavGroup(
    string Code,                       // e.g. "TODAY", "MASTER_DATA"
    string Title,                      // e.g. "Today"
    string IconClass,                  // FontAwesome (e.g. "fas fa-sun")
    int SortOrder,                     // 10 / 20 / 30 / 40 / 50 — five groups
    IReadOnlyList<NavItem> Items,
    bool DefaultExpanded = false);     // first group can start expanded

/// <summary>
/// One leaf row inside a NavGroup.  RoutePrefix lets a parent item
/// stay "active" when the user drills into a child page
/// (e.g. /Production matches /Production/{anything}).
/// </summary>
public sealed record NavItem(
    string Title,                      // e.g. "Production Orders"
    string Route,                      // e.g. "/Production"
    string IconClass,                  // FontAwesome (e.g. "fas fa-industry")
    string? RoutePrefix = null,        // defaults to Route — override only when needed
    string? Badge = null,              // optional inline chip ("new", "beta", "12")
    string? BadgeClass = null,         // optional chip styling
    bool RequiresFeatureFlag = false,  // when true, look up on Company.Enable*
    string? FeatureFlag = null);       // e.g. "EnablePurchasing"
