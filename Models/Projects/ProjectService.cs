// Theme B9 Wave 6 PR-18 (2026-05-31) — Project service handoff + warranty.
// CLOSES B9.
//
// ProjectServiceHandoff / ProjectWarranty (research §15). For manufactured
// equipment the project doesn't end at shipment — it hands off to the EAM /
// service world: installed asset, serial, commissioning, warranty window,
// as-built package, training + customer sign-off. The closeout gate
// ("cannot close an equipment project with an incomplete service handoff")
// lives in the service + the canonical CustomerProjectService.UpdateStatusAsync
// close path. The AI project-review (PR-18) composes a data-driven review onto
// the existing CustomerProject AI-summary fields.
//
// Conventions (ProjectQuality precedent): tenant-scoped THROUGH the project (no
// CompanyId); CASCADE from the project; the handoff peg on warranty + the WBS
// phase peg are SET NULL (one cascade path per row); the installed-asset id is a
// soft denormalized reference (no FK — the Asset lives in the EAM module); xmin;
// enum 0 member == model/DB default; per-project monotonic numbers.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectServiceHandoff — the equipment handoff record. Closing the project
    // requires every handoff to be SignedOff (the closeout gate).
    // =====================================================================
    [Table("ProjectServiceHandoffs")]
    public class ProjectServiceHandoff
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int HandoffNumber { get; set; }

        [StringLength(200)] public string? Title { get; set; }

        // Soft reference to the installed Asset in the EAM module (no FK — the
        // asset is a separate aggregate; this is a denormalized pointer).
        public int? InstalledAssetId { get; set; }

        [StringLength(120)] public string? SerialNumber { get; set; }
        [StringLength(120)] public string? CustomerAssetNumber { get; set; }
        [StringLength(200)] public string? InstallLocation { get; set; }

        public DateTime? InstallDate { get; set; }
        public DateTime? CommissioningDate { get; set; }

        [StringLength(200)] public string? ServiceContractReference { get; set; }
        [StringLength(200)] public string? PmTemplateReference { get; set; }
        [StringLength(200)] public string? AsBuiltBomReference { get; set; }
        [StringLength(200)] public string? AsBuiltDrawingReference { get; set; }

        public bool StartupChecklistComplete { get; set; } = false;
        public bool TrainingCompleted { get; set; } = false;

        public bool CustomerSignoff { get; set; } = false;
        [StringLength(120)] public string? CustomerSignoffBy { get; set; }
        public DateTime? CustomerSignoffAt { get; set; }

        public ProjectHandoffStatus Status { get; set; } = ProjectHandoffStatus.Draft;

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectWarranty — the warranty record tied to a handoff (or the project).
    // =====================================================================
    [Table("ProjectWarranties")]
    public class ProjectWarranty
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int WarrantyNumber { get; set; }

        // The handoff this warranty covers (SET NULL).
        public int? ProjectServiceHandoffId { get; set; }
        public ProjectServiceHandoff? ServiceHandoff { get; set; }

        [StringLength(200)] public string? Title { get; set; }
        public ProjectWarrantyType WarrantyType { get; set; } = ProjectWarrantyType.Full;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        [StringLength(120)] public string? Provider { get; set; }
        public string? Terms { get; set; }

        public ProjectWarrantyStatus Status { get; set; } = ProjectWarrantyStatus.Pending;
        public int ClaimCount { get; set; } = 0;

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== the DB default).
    // ---------------------------------------------------------------------

    public enum ProjectHandoffStatus : short
    {
        Draft = 0,
        Commissioned = 1,
        SignedOff = 2,    // complete — clears the closeout gate
        Closed = 3        // TERMINAL
    }

    public enum ProjectWarrantyType : short
    {
        Full = 0,
        Parts = 1,
        Labor = 2,
        Extended = 3
    }

    public enum ProjectWarrantyStatus : short
    {
        Pending = 0,
        Active = 1,
        Expired = 2,
        Claimed = 3,
        Void = 4          // TERMINAL
    }
}
