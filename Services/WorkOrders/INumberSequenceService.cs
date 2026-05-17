using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.WorkOrders
{
    // ADR-012 v0.2 / PR #119.5 — Atomic WO-number generator.
    //
    // The contract is dead simple: give me the next number for
    // (classification, tenantId). I'll handle the locking, the
    // year rollover, the formatting, and the audit trail.
    //
    // The unified WorkOrder save path (Phase F) calls NextAsync
    // exactly once when creating a new WO. The returned string is
    // assigned to MaintenanceEvent.WorkOrderNumber.
    public interface INumberSequenceService
    {
        // Returns the next formatted number (e.g. "PM-2026-001234",
        // "AFE-2026-0007", "NCR-2026-0042"). Atomic: concurrent calls
        // for the same bucket never return the same number.
        Task<string> NextAsync(
            WorkOrderClassification classification,
            int? tenantId,
            CancellationToken ct = default);
    }
}
