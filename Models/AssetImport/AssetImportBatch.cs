using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Models.AssetImport
{
    // ================================================================
    // Sprint 13.5 PR #337 — /Admin/AssetImport feature.
    //
    // Header entity for a bulk-asset Excel upload. The user uploads an
    // .xlsx, the service parses + validates + stages every row, then the
    // user reviews on /Admin/AssetImport/Preview/{id} and clicks Commit
    // to convert valid rows into real `Asset` entities.
    //
    // Tenant-trio compliant per BIC entity checklist. Lock 12 — typed
    // mb.CreateTable so the snapshot stays in sync.
    //
    // Spec: docs/research/asset-import-pr337-spec-2026-05-25.md
    // ================================================================

    public enum AssetImportBatchStatus : short
    {
        Draft = 0,        // Just parsed, not yet validated
        Validated = 1,    // Validation pass complete; preview available
        Committed = 2,    // Valid rows promoted to Assets
        Failed = 3,       // Commit ran but rolled back due to mid-loop failure
        Discarded = 4     // User abandoned the batch
    }

    [Table("AssetImportBatches")]
    [Index(nameof(CompanyId), nameof(CreatedAt), Name = "IX_AssetImportBatches_CompanyId_CreatedAt")]
    public class AssetImportBatch
    {
        public int Id { get; set; }

        // ---- Tenant trio (BIC checklist §1) ----
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? OrganizationId { get; set; }
        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        // ---- Provenance ----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        [Required, StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [StringLength(64)]
        public string? SheetName { get; set; }

        // ---- Lifecycle ----
        public AssetImportBatchStatus Status { get; set; } = AssetImportBatchStatus.Draft;

        public int RowCount { get; set; }
        public int ValidRowCount { get; set; }
        public int ErrorRowCount { get; set; }

        public DateTime? ValidatedAt { get; set; }

        public DateTime? CommittedAt { get; set; }
        public int? CommittedByUserId { get; set; }
        public User? CommittedByUser { get; set; }

        public DateTime? DiscardedAt { get; set; }
        public int? DiscardedByUserId { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // Navigation
        public ICollection<AssetImportRow>? Rows { get; set; }
    }
}
