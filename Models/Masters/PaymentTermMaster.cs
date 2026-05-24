using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-6 — PaymentTerm master.
    //
    // Replaces the 7-value `Vendor.PaymentTerms` enum + the orphan `int?
    // PaymentTermId` FK on Customer + Vendor (added by PRA-1 but with no
    // target table). Real PaymentTerm has structure the enum can't carry:
    //   - DueDays + DiscountPct + DiscountDays — "2/10 N30" (2% discount
    //     if paid within 10 days; full payment due in 30 days)
    //   - BasisDate — InvoiceDate / MonthEnd / ReceiptDate / ShipDate. EOM
    //     terms ("Net 30 EOM") need MonthEnd; some industries use ShipDate.
    //   - Multi-cut schedules (deferred to JSON column for v1) — e.g.
    //     "25/25/25/25 N90" (25% at each of day 30/60/90/120).
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system, set = tenant.
    //
    // FOREWARD-LOOKING:
    //   - Customer.PaymentTermId + Vendor.PaymentTermId (existing nullable
    //     FKs from PRA-1) now have a real target table.
    //   - Vendor.PaymentTerms enum stays for back-compat; service layer reads
    //     PaymentTermId first, falls back to enum (DEF-008 pattern). Cleanup
    //     PR will deprecate the enum once all read paths migrate.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.2
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("PaymentTermMasters")]
    public class PaymentTermMaster
    {
        public int Id { get; set; }

        // NULL = system row, set = tenant-specific.
        public int? CompanyId { get; set; }

        // Stable code for matching + display (e.g. "NET30", "2/10 N30",
        // "EOM-30", "PREPAID", "COD").
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Days until full payment is due, computed from BasisDate.
        public int DueDays { get; set; } = 30;

        // Cash discount percentage (e.g. 0.0200 for 2%). Stored at 4dp.
        [Column(TypeName = "numeric(7,4)")]
        public decimal DiscountPct { get; set; } = 0m;

        // Days within which the discount applies. 0 = no discount.
        public int DiscountDays { get; set; } = 0;

        // What date the due-day count anchors against.
        public PaymentTermBasisDate BasisDate { get; set; } = PaymentTermBasisDate.InvoiceDate;

        // Free-form JSON for multi-cut schedules (deferred from v1 implementation
        // until a customer needs it). Shape proposal:
        //   [{"day":30,"pct":25},{"day":60,"pct":25},{"day":90,"pct":25},{"day":120,"pct":25}]
        [Column(TypeName = "jsonb")]
        public string? MultiCutScheduleJson { get; set; }

        // Optional currency restriction — when set, this term only applies for
        // payments in that currency (rare; mostly for international vendors who
        // have separate terms per currency).
        public int? CurrencyId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum PaymentTermBasisDate
    {
        InvoiceDate = 0,
        MonthEnd = 1,         // Net 30 EOM — clock starts at the last day of the invoice month
        ReceiptDate = 2,      // Goods receipt date (some industries)
        ShipDate = 3          // Ship-from-our-dock date (consignment / FOB)
    }
}
