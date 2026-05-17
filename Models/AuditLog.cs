using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    [StringLength(100)]
    public string? Username { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    // ADR-014 D3 — AI-on-behalf-of metadata. NULL for direct-user
    // actions. Populated when the future voice-AI layer mediates an
    // action. The human is ALWAYS the principal Actor (Username
    // above). This matches Microsoft Purview CopilotInteraction
    // schema convention.

    public ActorKind ActorKind { get; set; } = ActorKind.User;

    // FK-shaped reference to Users.Id (int). No FK constraint —
    // history survives user deletion (like the existing Username).
    public int? OnBehalfOfUserId { get; set; }

    // Correlates rows from one multi-turn voice conversation.
    public Guid? AiSessionId { get; set; }

    // Raw natural-language utterance. May contain PII.
    public string? AiCommandText { get; set; }

    [StringLength(64)]
    public string? AiModelVersion { get; set; }

    [StringLength(128)]
    public string? AiToolName { get; set; }

    [Column(TypeName = "decimal(4,3)")]
    public decimal? AiConfidence { get; set; }
}

[Table("PeriodLocks")]
public class PeriodLock
{
    [Key]
    public int Id { get; set; }

    public int Period { get; set; }

    public DateTime LockedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? LockedBy { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public bool IsLocked { get; set; } = true;
}
