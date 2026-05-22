using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Abs.FixedAssets.Services.Voice;

// Sprint 12C / ADR-021 — embedding queue + worker logic.
//
// ProcessPendingAsync:
//   1. Lease up to batchSize rows from PendingEmbeddings using
//      `FOR UPDATE SKIP LOCKED` so multi-instance workers don't fight.
//   2. For each row, check if Embeddings already has a matching
//      (EntityType, EntityId, ModelVersion, ContentHash) — if yes,
//      delete the queue row (idempotent re-enqueue).
//   3. Group the remaining rows into batches of 32 (Voyage's
//      practical sweet spot per ADR-021 §D4).
//   4. Call Voyage. On success, upsert Embeddings rows + delete the
//      corresponding queue rows.
//   5. On failure, bump Attempts + set LastError + leave the row.
//      The 5-attempt cap is enforced via the SELECT WHERE clause.
public sealed class EmbeddingBackfillService : IEmbeddingBackfillService
{
    private const string ModelVersion = "voyage-3-large/v1";
    private const int MaxAttempts = 5;
    private const int VoyageBatchSize = 32;

    private readonly AppDbContext _db;
    private readonly IVoyageClient _voyage;
    private readonly ILogger<EmbeddingBackfillService> _logger;

    public EmbeddingBackfillService(
        AppDbContext db,
        IVoyageClient voyage,
        ILogger<EmbeddingBackfillService> logger)
    {
        _db = db;
        _voyage = voyage;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        string entityType,
        long entityId,
        int tenantId,
        string sourceText,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceText)) return;
        var hash = EmbeddingSourceText.ComputeHash(sourceText);

        // De-dupe: if an existing Embeddings row already has this exact
        // hash, no need to enqueue.
        var existingHash = await _db.Embeddings
            .Where(e => e.EntityType == entityType &&
                        e.EntityId == entityId &&
                        e.ModelVersion == ModelVersion)
            .Select(e => e.ContentHash)
            .FirstOrDefaultAsync(ct);
        if (existingHash == hash) return;

        // De-dupe queue: if same (EntityType, EntityId, ContentHash)
        // already queued + not failed-out, no-op.
        var alreadyQueued = await _db.PendingEmbeddings.AnyAsync(p =>
            p.EntityType == entityType &&
            p.EntityId == entityId &&
            p.ContentHash == hash &&
            p.Attempts < MaxAttempts, ct);
        if (alreadyQueued) return;

        _db.PendingEmbeddings.Add(new PendingEmbedding
        {
            EntityType = entityType,
            EntityId = entityId,
            TenantId = tenantId,
            SourceText = sourceText,
            ContentHash = hash,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct)
    {
        // Lease rows. Postgres-specific FOR UPDATE SKIP LOCKED so
        // multi-instance workers don't process the same row twice.
        var leased = await _db.PendingEmbeddings
            .FromSqlInterpolated($@"
                SELECT * FROM ""PendingEmbeddings""
                WHERE ""Attempts"" < {MaxAttempts}
                ORDER BY ""EnqueuedAt""
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED")
            .AsNoTracking()
            .ToListAsync(ct);

        if (leased.Count == 0) return 0;

        // Batch the Voyage call (32 at a time per ADR-021 §D4).
        int succeeded = 0;
        foreach (var batch in leased.Chunk(VoyageBatchSize))
        {
            var successfullyProcessedIds = new List<long>();
            try
            {
                var texts = batch.Select(b => b.SourceText).ToList();
                var vectors = await _voyage.EmbedDocumentsAsync(texts, ct);

                // PR #282 — Sprint 12C bootstrap-loss bug fix.
                //
                // Phase 1: stage the upsert intent in the EF change tracker
                // for every row in the batch. UpsertEmbeddingAsync now checks
                // .Local first so within-batch duplicate (EntityType, EntityId)
                // keys collapse to ONE tracked entity (last write wins) instead
                // of three Add() calls that violate the
                // ix_embeddings_entity_model UNIQUE constraint on SaveChanges.
                //
                // Phase 2: flush the Embeddings change set.
                //
                // Phase 3 (ONLY on Phase 2 success): delete the corresponding
                // PendingEmbeddings rows. The pre-fix code deleted BEFORE
                // SaveChangesAsync, which meant a failed flush silently
                // destroyed the queue rows (the bug that prevented Intent
                // prototype seeding for the past two days).
                for (int i = 0; i < batch.Length; i++)
                {
                    var pending = batch[i];
                    var vector = vectors[i];
                    if (vector.Length != 1024)
                    {
                        _logger.LogWarning(
                            "Voyage returned vector of unexpected dimension {Dim} (expected 1024). Skipping {Entity}#{Id}.",
                            vector.Length, pending.EntityType, pending.EntityId);
                        continue;
                    }

                    var half = ToHalfVector(vector);
                    await UpsertEmbeddingAsync(pending, half, ct);
                    successfullyProcessedIds.Add(pending.Id);
                }

                // Phase 2 — flush Embeddings upserts before deleting queue rows.
                await _db.SaveChangesAsync(ct);

                // Phase 3 — only NOW remove the queue rows that successfully
                // landed in Embeddings. If Phase 2 threw, we land in the catch
                // block below and the queue rows survive for the next retry.
                if (successfullyProcessedIds.Count > 0)
                {
                    await _db.PendingEmbeddings
                        .Where(p => successfullyProcessedIds.Contains(p.Id))
                        .ExecuteDeleteAsync(ct);
                    succeeded += successfullyProcessedIds.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Voyage embed batch of {Count} failed; bumping Attempts on the batch.",
                    batch.Length);

                // Bump Attempts + set LastError on all rows in the failed batch.
                // Queue rows are intact because Phase 3 only deletes after a
                // successful Phase 2.
                var ids = batch.Select(b => b.Id).ToList();
                foreach (var id in ids)
                {
                    await _db.PendingEmbeddings
                        .Where(p => p.Id == id)
                        .ExecuteUpdateAsync(setter => setter
                            .SetProperty(p => p.Attempts, p => p.Attempts + 1)
                            .SetProperty(p => p.LastAttemptAt, _ => DateTime.UtcNow)
                            .SetProperty(p => p.LastError, _ => Truncate(ex.Message, 480)),
                            ct);
                }
            }
        }

        return succeeded;
    }

    public async Task<int> EnqueueAllReceiptProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.Set<Abs.FixedAssets.Models.Production.ReceiptProfile>()
            .AsNoTracking()
            .ToListAsync(ct);

        int enqueued = 0;
        foreach (var p in profiles)
        {
            var srcText = EmbeddingSourceText.ForReceiptProfile(p);
            // TenantId: ReceiptProfile is shared catalog (no per-tenant
            // column today). Use 0 = shared/global per ADR-020 §D6
            // convention. RLS policy still applies (a future per-tenant
            // ReceiptProfile would use its own TenantId).
            int tenantId = 0;
            var beforeCount = _db.PendingEmbeddings.Local.Count;
            await EnqueueAsync(
                EmbeddingSourceText.EntityTypeReceiptProfile,
                p.Id, tenantId, srcText, ct);
            // EnqueueAsync uses Add() which goes to Local — but it also
            // SaveChanges, so the Local count won't grow. Track via the
            // database side.
            enqueued++;
        }

        _logger.LogInformation(
            "Enqueued {Count} ReceiptProfiles for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    public async Task<int> EnqueueAllItemsAsync(CancellationToken ct)
    {
        // Stream pages to avoid loading the full Item table into memory.
        // CompanyId becomes TenantId when present; falls back to 0
        // (shared/global) for unattributed rows.
        int enqueued = 0;
        const int PageSize = 500;
        int skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await _db.Items
                .AsNoTracking()
                .OrderBy(i => i.Id)
                .Skip(skip).Take(PageSize)
                .ToListAsync(ct);
            if (page.Count == 0) break;

            foreach (var item in page)
            {
                var srcText = EmbeddingSourceText.ForItem(item);
                int tenantId = 0; // Item has no CompanyId on the model today.
                await EnqueueAsync(
                    EmbeddingSourceText.EntityTypeItem,
                    item.Id, tenantId, srcText, ct);
                enqueued++;
            }
            skip += page.Count;
        }

        _logger.LogInformation(
            "Enqueued {Count} Items for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    public async Task<int> EnqueueAllVendorsAsync(CancellationToken ct)
    {
        // Vendor.CompanyId IS the tenant when set (multi-tenant scoped
        // vendors), 0 otherwise (shared / pre-multi-tenant rows).
        int enqueued = 0;
        const int PageSize = 500;
        int skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await _db.Vendors
                .AsNoTracking()
                .OrderBy(v => v.Id)
                .Skip(skip).Take(PageSize)
                .ToListAsync(ct);
            if (page.Count == 0) break;

            foreach (var vendor in page)
            {
                var srcText = EmbeddingSourceText.ForVendor(vendor);
                int tenantId = vendor.CompanyId ?? 0;
                await EnqueueAsync(
                    EmbeddingSourceText.EntityTypeVendor,
                    vendor.Id, tenantId, srcText, ct);
                enqueued++;
            }
            skip += page.Count;
        }

        _logger.LogInformation(
            "Enqueued {Count} Vendors for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    public async Task<int> EnqueueAllWorkOrdersAsync(CancellationToken ct)
    {
        // WorkOrder tenancy flows through Asset.CompanyId per the model
        // comments. For Sprint 12C PR #2 we use 0 (shared/global) — the
        // backfill is single-tenant on the current Replit deploy. Full
        // multi-tenant scoping belongs to Sprint 12.5 (RLS + tenant_id).
        int enqueued = 0;
        const int PageSize = 500;
        int skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await _db.WorkOrders
                .AsNoTracking()
                .OrderBy(w => w.Id)
                .Skip(skip).Take(PageSize)
                .ToListAsync(ct);
            if (page.Count == 0) break;

            foreach (var wo in page)
            {
                var srcText = EmbeddingSourceText.ForWorkOrder(wo);
                int tenantId = 0;
                await EnqueueAsync(
                    EmbeddingSourceText.EntityTypeWorkOrder,
                    wo.Id, tenantId, srcText, ct);
                enqueued++;
            }
            skip += page.Count;
        }

        _logger.LogInformation(
            "Enqueued {Count} WorkOrders for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    public async Task<int> EnqueueAllAuditAiCommandsAsync(CancellationToken ct)
    {
        // Only AuditLog rows whose AiCommandText is non-null + non-empty
        // are embeddable. Pre-filter at SQL level so we don't ship empty
        // rows over the wire. Use a projection to avoid loading the heavy
        // BeforeJson / AfterJson payloads we don't need.
        int enqueued = 0;
        const int PageSize = 500;
        int skip = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await _db.AuditLogs
                .AsNoTracking()
                .Where(a => a.AiCommandText != null && a.AiCommandText != "")
                .OrderBy(a => a.Id)
                .Skip(skip).Take(PageSize)
                .Select(a => new { a.Id, a.AiCommandText })
                .ToListAsync(ct);
            if (page.Count == 0) break;

            foreach (var row in page)
            {
                var srcText = EmbeddingSourceText.ForAiCommand(row.AiCommandText);
                if (string.IsNullOrWhiteSpace(srcText)) continue;
                int tenantId = 0; // AuditLog is not tenant-scoped in v1.
                await EnqueueAsync(
                    EmbeddingSourceText.EntityTypeAiCommand,
                    row.Id, tenantId, srcText, ct);
                enqueued++;
            }
            skip += page.Count;
        }

        _logger.LogInformation(
            "Enqueued {Count} AuditLog AI command utterances for embedding (model: {Model})",
            enqueued, ModelVersion);
        return enqueued;
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private async Task UpsertEmbeddingAsync(
        PendingEmbedding pending,
        HalfVector vector,
        CancellationToken ct)
    {
        // PR #282 — Sprint 12C bootstrap-loss bug fix.
        //
        // BEFORE going to the database, check the EF change tracker. If a
        // prior iteration in the SAME batch already staged an Add() for this
        // (EntityType, EntityId, ModelVersion) tuple, mutate THAT entity
        // instead of staging a second Add. Two Add()s with the same key
        // would violate ix_embeddings_entity_model UNIQUE on SaveChanges,
        // which is exactly what was silently destroying the 12-row Intent
        // prototype batch on every restart (3 rows per IntentKind).
        var localExisting = _db.Embeddings.Local.FirstOrDefault(e =>
            e.EntityType == pending.EntityType &&
            e.EntityId == pending.EntityId &&
            e.ModelVersion == ModelVersion);

        if (localExisting is not null)
        {
            localExisting.ContentHash = pending.ContentHash;
            localExisting.Embedding_ = vector;
            localExisting.SourceText = pending.SourceText;
            return;
        }

        var existing = await _db.Embeddings.FirstOrDefaultAsync(e =>
            e.EntityType == pending.EntityType &&
            e.EntityId == pending.EntityId &&
            e.ModelVersion == ModelVersion, ct);

        if (existing is null)
        {
            _db.Embeddings.Add(new Embedding
            {
                EntityType = pending.EntityType,
                EntityId = pending.EntityId,
                TenantId = pending.TenantId,
                ModelVersion = ModelVersion,
                ContentHash = pending.ContentHash,
                Embedding_ = vector,
                SourceText = pending.SourceText,
            });
        }
        else
        {
            existing.ContentHash = pending.ContentHash;
            existing.Embedding_ = vector;
            existing.SourceText = pending.SourceText;
        }
    }

    private static HalfVector ToHalfVector(float[] floats)
    {
        var halves = new Half[floats.Length];
        for (int i = 0; i < floats.Length; i++)
        {
            halves[i] = (Half)floats[i];
        }
        return new HalfVector(new ReadOnlyMemory<Half>(halves));
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max];
    }
}
