using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D4 — pluggable grouping strategy for a Cockpit queue.
//
// The default is ByTimeLens<T> (Overdue / Today / This Week / Later), used by
// Receiving + Purchasing + Shipping (everything that's time-driven). Roles
// whose work is NOT time-driven implement their own lens:
//   - Maintenance: ByPriorityAndSlaLens<T> (P1 critical / P1 / P2 / P3+)
//   - Inventory:   ByZoneAndCountAgeLens<T>
//   - Quality:     ByLotStatusLens<T>
//
// Multiple lenses may be registered per page; the queue header shows a lens
// picker dropdown when count > 1. Default Receiving Cockpit ships with the
// time lens only; multi-lens land in the sprint that first needs it.
public interface ICockpitLens<TRow>
{
    // Stable machine key — used in URL state + lens picker dropdown value.
    string Code { get; }

    // Human-readable label — shown in the lens picker dropdown.
    string Label { get; }

    // Group the rows. Empty buckets MAY be returned (e.g. "Overdue (0)") or
    // skipped — implementer's choice. ByTimeLens skips empty buckets.
    IReadOnlyList<CockpitGroup<TRow>> Group(IReadOnlyList<TRow> rows);
}
