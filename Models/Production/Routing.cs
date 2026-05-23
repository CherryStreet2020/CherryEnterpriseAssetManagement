using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5c — Routing (the MES manufacturing method)
//
// ADR-013 §"Manufacturing Domain" + MES research synthesis 2026-05-23 (Oracle
// WIS_WORK_DEFINITIONS, SAP S/4HANA Routings + Master Recipes, Epicor Method
// of Manufacturing, D365 Routes, Plex Routings, Siemens Opcenter Process Plans).
//
// A Routing is the standard sequence of operations a part flows through to
// become a finished good. Mirrors SAP "group counter" / Epicor "alt method"
// versioning — one Item can have multiple Routings (different process plans
// for different production scenarios), one marked IsDefault.
//
// MULTI-VERTICAL POLYMORPHISM (key design decision):
//
//   RoutingType        | Child entity                          | Use case
//   -------------------|---------------------------------------|------------
//   Discrete           | RoutingOperation                      | Job-shop, machine shop, fabricator
//   RepetitiveDiscrete | RoutingOperation (line-rate adjusted) | Auto OEM, line assembly
//   CapitalETO         | RoutingOperation (revisable)          | Engineer-to-order capital equipment
//   Process            | RecipePhase (PR #1.5 — ORTHOGONAL)    | Chem, food, pharma
//
// For Process orders the Routing entity is OPTIONAL — those orders use the
// existing Recipe + RecipePhase pattern instead. We don't fork the data model;
// we let RoutingType drive which child entities are walked at runtime.
//
// SNAPSHOT DISCIPLINE: at ProductionOrder release time, each RoutingOperation
// is copied into ProductionOperation rows (NOT dynamic-lookup). This way
// editing the Routing master AFTER release doesn't retroactively change
// in-flight orders. Each ProductionOperation also stamps RoutingRevisionSnapshot.
// =============================================================================
public enum RoutingType
{
    Discrete = 0,            // Job-shop / machine-shop / fab (the dominant case)
    RepetitiveDiscrete = 1,  // Auto OEM / line assembly
    CapitalETO = 2,          // Engineer-to-order capital equipment (multi-month projects)
    Process = 3,             // Recipe-driven batch (uses RecipePhase, not RoutingOperation)
}

public enum RoutingStatus
{
    Draft = 0,            // Engineer editing — no production allowed
    UnderReview = 1,      // Submitted for approval
    Approved = 2,         // Approved but not yet released for production
    Released = 3,         // Live — production orders can use it
    Obsolete = 4,         // Superseded by a newer revision — read-only history
}

[Table("Routings")]
public class Routing
{
    public int Id { get; set; }

    // Tenancy.
    public int CompanyId { get; set; }

    // Identification + versioning.
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;        // e.g. "ROUT-BRACKET-001"

    [Required, MaxLength(10)]
    public string RevisionNumber { get; set; } = "A";       // SAP group counter convention (A, B, C...)

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;        // e.g. "Bracket Assy v2"

    [MaxLength(2000)]
    public string? Description { get; set; }

    // What this routing produces.
    public int ItemId { get; set; }                         // FK → Item (the FG being produced)

    public RoutingType Type { get; set; } = RoutingType.Discrete;
    public RoutingStatus Status { get; set; } = RoutingStatus.Draft;

    // PR #5c.1: Site scoping per the BIC checklist.
    //   - LocationId NULL + IsSiteWideTemplate=TRUE → company-wide engineering template
    //     (SAP's "BOM with no plant" pattern). Production orders at any site can release
    //     from it; the order's LocationId snapshots onto each ProductionOperation.
    //   - LocationId NOT NULL + IsSiteWideTemplate=FALSE → site-scoped routing.
    //     ProductionOrder.LocationId must match at release.
    //   - LocationId NULL + IsSiteWideTemplate=FALSE → not allowed (CHECK constraint).
    public int? LocationId { get; set; }
    public bool IsSiteWideTemplate { get; set; } = false;

    // Lifecycle.
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [MaxLength(100)]
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Scaling.
    public decimal LotBaseSize { get; set; } = 1;           // RoutingOperation times scale to this batch size
    [MaxLength(10)]
    public string? UnitOfMeasure { get; set; }              // "EA", "LB", "KG"

    // Multiple routings per item — one is the default for new orders.
    public bool IsDefault { get; set; } = false;

    // For "copy from" UX (smart-default composer per the BIC research).
    public int? SourceRoutingId { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
    public bool IsActive { get; set; } = true;

    // Nav.
    public ICollection<RoutingOperation> Operations { get; set; } = new List<RoutingOperation>();
}
