using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Models.AssetImport
{
    // ================================================================
    // Sprint 13.5 PR #337 — per-row staging for a bulk-asset import.
    //
    // One row per Excel data row (row 2..N). Carries the raw text from
    // the workbook plus the resolution targets (ManufacturerId, SiteId,
    // etc.) and any validation errors. On commit, `CommittedAssetId` is
    // stamped with the new Asset.Id for audit traceability.
    //
    // Spec: docs/research/asset-import-pr337-spec-2026-05-25.md
    // ================================================================

    public enum AssetImportRowStatus : short
    {
        Pending = 0,    // Just parsed, not yet validated
        Valid = 1,      // Ready to commit
        Error = 2,      // Cannot commit until errors are resolved (re-upload)
        Committed = 3   // Promoted to Asset
    }

    [Table("AssetImportRows")]
    [Index(nameof(BatchId), nameof(RowNumber), Name = "IX_AssetImportRows_BatchId_RowNumber")]
    public class AssetImportRow
    {
        public long Id { get; set; }

        public int BatchId { get; set; }
        public AssetImportBatch? Batch { get; set; }

        // Excel row number (1-based; data rows start at 2)
        public int RowNumber { get; set; }

        public AssetImportRowStatus Status { get; set; } = AssetImportRowStatus.Pending;

        // ---- Raw text columns mirroring the Excel template ----

        [StringLength(50)]
        public string? AssetNumber { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? LongDescription { get; set; }

        [StringLength(200)]
        public string? Model { get; set; }

        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        public string? TagNumber { get; set; }

        [StringLength(200)]
        public string? ManufacturerName { get; set; }
        public int? ResolvedManufacturerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AcquisitionCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ReplacementCost { get; set; }

        [StringLength(3)]
        public string? Currency { get; set; }

        public DateTime? PurchaseDate { get; set; }
        public DateTime? InServiceDate { get; set; }

        public int? FiscalPurchaseYear { get; set; }
        public int? UsefulLifeMonths { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [StringLength(50)]
        public string? LocationCode { get; set; }
        public int? ResolvedLocationId { get; set; }

        [StringLength(50)]
        public string? DepartmentCode { get; set; }
        public int? ResolvedDepartmentId { get; set; }

        [StringLength(50)]
        public string? SiteCode { get; set; }
        public int? ResolvedSiteId { get; set; }

        [StringLength(20)]
        public string? StatusSource { get; set; }
        public int? ResolvedStatus { get; set; }

        // Newline-delimited "ColumnName: reason" entries
        public string? ValidationErrors { get; set; }

        // Populated on commit
        public int? CommittedAssetId { get; set; }
    }
}
