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
}
