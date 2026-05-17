using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.WorkOrders;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.2 — Field visibility resolver for the unified
    // WorkOrder form renderer.
    //
    // The Razor renderer asks: "For a WO with Classification=X and tenant Y,
    // what fields do I show, in what sections, in what order, with what
    // labels, marked as required/optional/readonly?"
    //
    // This service is the answer. It loads the WorkOrderFieldVisibility
    // rows once at startup (or on cache invalidation), merges
    // tenant-scoped overrides on top of global defaults, and returns a
    // ready-to-render list grouped by SectionName.
    //
    // Caching: the underlying table is read-mostly. The service uses an
    // in-memory dictionary keyed by (Classification, TenantId) with an
    // explicit Invalidate() entry point for the admin-edit path (Sprint 4).
    public interface IWorkOrderFieldVisibilityService
    {
        // Returns every visible row (Visibility != Hidden) for the given
        // classification + tenant, ordered by SectionName then DisplayOrder.
        // Tenant-scoped rows take precedence over global (TenantId NULL) rows.
        Task<IReadOnlyList<WorkOrderFieldVisibility>> GetLayoutAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default);

        // Returns the rendered list grouped by section, in section-then-order.
        // Convenience wrapper for the Razor renderer.
        Task<IReadOnlyList<SectionLayout>> GetSectionedLayoutAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default);

        // Returns the resolved visibility for a single field. Used by the
        // status engine (PR #119.3) to test whether a field is required
        // before allowing a status transition.
        Task<FieldVisibility> GetFieldVisibilityAsync(
            WorkOrderClassification classification,
            string fieldName,
            int? tenantId,
            CancellationToken ct = default);

        // Invalidates the cache. Called by the admin-edit endpoint when
        // a customer changes their field-visibility config.
        void Invalidate(int? tenantId = null);
    }

    // Renderer-friendly section view: a section label + the fields that
    // belong to it, already ordered.
    public sealed record SectionLayout(
        string SectionName,
        IReadOnlyList<WorkOrderFieldVisibility> Fields);
}
