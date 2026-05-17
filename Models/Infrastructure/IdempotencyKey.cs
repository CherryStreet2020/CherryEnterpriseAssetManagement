using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Infrastructure;

// ADR-014 D4 — Stripe-pattern idempotency keys.
//
// Per brandur.org and Stripe's published pattern: client mints a UUID
// per logical operation, server stores (UserId, Key) UNIQUE with the
// request hash + cached response. Same key + same payload returns the
// cached response. Same key + different payload raises a conflict.
//
// Lives in the same Postgres database as the data being mutated —
// required for ACID dedup. Redis or in-memory cache fails the
// "worker crashes mid-execution" recovery property.
//
// TTL 24 hours matches Stripe's default + brandur.org guidance.
//
// Used by IIdempotencyMediator (Services/Infrastructure/). Apply to
// every mutation in Phase F that the voice layer will eventually
// trigger.
//
// Reference: ADR-014 §"Decisions" D4 + brandur.org/idempotency-keys.
[Table("IdempotencyKeys")]
public class IdempotencyKey
{
    // Composite PK on (UserId, Key) enforced in OnModelCreating.
    public int UserId { get; set; }

    public Guid Key { get; set; }

    // SHA-256 hash of the canonical-JSON request payload. Same key +
    // same hash = return cached response. Same key + different hash =
    // 409 Conflict.
    public byte[] RequestHash { get; set; } = Array.Empty<byte>();

    // Cached response, populated after the inner work completes.
    public int? ResponseStatus { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ResponseBody { get; set; }

    // Set when the row is inserted; cleared when CompletedAt is set.
    // Used to detect "another worker is currently executing this key" —
    // see IdempotencyMediator implementation.
    public DateTime? LockedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // 24 hours from insert by default.
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}
