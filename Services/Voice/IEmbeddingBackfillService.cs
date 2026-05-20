using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 §D3 — change-data-capture queue interface.
//
// Producers call EnqueueAsync on save. Consumer (EmbeddingWorker)
// calls ProcessPendingAsync on a poll loop. Hash-based skip-if-
// unchanged keeps the queue tiny under steady-state load.
public interface IEmbeddingBackfillService
{
    /// <summary>
    /// Enqueue one (EntityType, EntityId, sourceText) for the worker
    /// to embed. Idempotent on (EntityType, EntityId, ContentHash) —
    /// if the same hash is already queued, no-op.
    /// </summary>
    Task EnqueueAsync(
        string entityType,
        long entityId,
        int tenantId,
        string sourceText,
        CancellationToken ct);

    /// <summary>
    /// Worker entry point. Reads up to batchSize rows, calls Voyage,
    /// upserts to Embeddings, deletes from queue. Returns the number
    /// of rows successfully processed (failed rows stay in the queue
    /// with Attempts++ + LastError set).
    /// </summary>
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct);

    /// <summary>
    /// Admin one-shot to enqueue every existing ReceiptProfile.
    /// Used by the Sprint 12C PR #1 admin backfill endpoint.
    /// </summary>
    Task<int> EnqueueAllReceiptProfilesAsync(CancellationToken ct);

    /// <summary>
    /// Admin one-shot to enqueue every existing Item. Sprint 12C PR #2.
    /// Source-text shape per ADR-021 Appendix A.
    /// </summary>
    Task<int> EnqueueAllItemsAsync(CancellationToken ct);

    /// <summary>
    /// Admin one-shot to enqueue every existing Vendor. Sprint 12C PR #2.
    /// Source-text shape per ADR-021 Appendix A.
    /// </summary>
    Task<int> EnqueueAllVendorsAsync(CancellationToken ct);

    /// <summary>
    /// Admin one-shot to enqueue every existing WorkOrder. Sprint 12C PR #2.
    /// Source-text shape per ADR-021 Appendix A. Note: the .NET class is
    /// `WorkOrder` (table renamed from MaintenanceEvents in PR #119.7).
    /// </summary>
    Task<int> EnqueueAllWorkOrdersAsync(CancellationToken ct);

    /// <summary>
    /// Admin one-shot to enqueue every existing AuditLog row whose
    /// AiCommandText is non-null + non-empty. Sprint 12C PR #2.
    /// Used to retrieve historical voice utterances for the hybrid
    /// intent router (Sprint 12C PR #3).
    /// </summary>
    Task<int> EnqueueAllAuditAiCommandsAsync(CancellationToken ct);
}
