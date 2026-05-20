using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Abs.FixedAssets.Models.Embeddings;

// Sprint 12C / ADR-020 §D2 + ADR-021 §D2 — polymorphic embedding row.
//
// One row per (EntityType, EntityId, ModelVersion). Carries:
//   - The actual vector (halfvec(1024) — Voyage voyage-3-large native).
//   - The source text used to produce the embedding (debug + cache key).
//   - A SHA-256 content hash for skip-if-unchanged.
//   - RLS partition key (TenantId).
//   - Model version stamp for migration safety.
//
// Tenant isolation enforced via PG RLS policy keyed off
// current_setting('app.tenant_id'). See migration for the CREATE POLICY.
//
// Why halfvec(1024) over vector(1024):
//   16-bit floats per dimension = 50% storage win at <0.1% recall loss
//   on the MTEB benchmark. Practically free.
//
// Why HNSW index over StreamingDiskANN:
//   pgvectorscale (DiskANN) is not on Replit's available extension list
//   (verified 2026-05-20). HNSW is in pgvector core, fine for <10M
//   vectors. Promotion to DiskANN is a Phase 5 ADR-024 move.
public class Embedding
{
    public long Id { get; set; }

    // What entity this row describes — e.g. "ReceiptProfile", "Item",
    // "Vendor", "WorkOrder", "AuditLog.AiCommandText".
    [Required]
    [StringLength(64)]
    public string EntityType { get; set; } = string.Empty;

    // FK-shaped reference to the source entity. No FK constraint —
    // source entity may be soft-deleted; we keep embeddings for audit.
    public long EntityId { get; set; }

    // RLS partition key.
    public int TenantId { get; set; }

    // Identifies which embedding model + version produced this vector.
    // Examples: "voyage-3-large/v1", "openai-text-embedding-3-large/v1".
    // Used to drive the 7-day dual-write migration playbook per ADR-021 §D6.
    [Required]
    [StringLength(64)]
    public string ModelVersion { get; set; } = string.Empty;

    // SHA-256 of source text (lowercase hex). 64 chars.
    // Drives the skip-if-unchanged behavior per ADR-021 §D5.
    [Required]
    [Column(TypeName = "char(64)")]
    public string ContentHash { get; set; } = string.Empty;

    // The actual vector. Pgvector 0.3.2 maps to Postgres halfvec via
    // the [Column(TypeName = "halfvec(1024)")] attribute.
    [Required]
    [Column(TypeName = "halfvec(1024)")]
    public Pgvector.HalfVector Embedding_ { get; set; } = new Pgvector.HalfVector(new ReadOnlyMemory<Half>(new Half[1024]));

    // The text that was sent to the embedder. Kept for debug + cache
    // invalidation. Same RLS scope as the entity it describes.
    public string? SourceText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
