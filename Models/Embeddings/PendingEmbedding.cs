using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Embeddings;

// Sprint 12C / ADR-021 §D3 — change-data-capture queue table for the
// external embedding worker.
//
// Producers (entity services, voice endpoint, admin backfill endpoint)
// INSERT one row per (entity that needs embedding).
//
// Consumer (EmbeddingWorker BackgroundService) polls every 5s, takes
// a batch of N rows, calls Voyage, upserts into Embeddings, deletes
// from this queue.
//
// Why a table instead of an in-process queue:
//   - Survives process restart (Replit / future Kubernetes pod kills)
//   - Visible to ops via psql ("SELECT * FROM PendingEmbeddings WHERE Attempts > 3")
//   - Multi-instance safe (workers can lease rows via SKIP LOCKED)
//   - Same RLS plane as everything else
//
// Why not a hosted message broker (RabbitMQ, Kafka, Azure Service Bus):
//   - Adds another runtime dependency
//   - Postgres SKIP LOCKED is good enough at our scale (<1M embeds/day)
//   - Same DB as the data being embedded = same backup, same transaction boundary
public class PendingEmbedding
{
    public long Id { get; set; }

    [Required]
    [StringLength(64)]
    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public int TenantId { get; set; }

    // The source text snapshot at enqueue time. Worker uses this
    // verbatim — no need to re-fetch the entity if it's been edited
    // in the meantime (the new edit will enqueue its own row anyway).
    [Required]
    public string SourceText { get; set; } = string.Empty;

    // SHA-256 of SourceText. If the corresponding Embeddings row already
    // has this same hash for this (EntityType, EntityId, ModelVersion),
    // the worker skips the Voyage call.
    [Required]
    [Column(TypeName = "char(64)")]
    public string ContentHash { get; set; } = string.Empty;

    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    // How many times the worker has attempted this row. Bumped on
    // retry-eligible failures (429, 5xx, network). Maxes at 5 per
    // ADR-021 §D4; after that LastError is set + row is left for
    // manual triage.
    public int Attempts { get; set; } = 0;

    public DateTime? LastAttemptAt { get; set; }

    // Free-text error string from the last failed attempt. NULL on
    // success (the row is then deleted, so this is only populated for
    // rows that are stuck in the queue).
    [StringLength(500)]
    public string? LastError { get; set; }
}
