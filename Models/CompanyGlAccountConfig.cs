using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Per-company configuration of which GL account string to use for
    /// each <see cref="GlAccountKind"/> posting purpose. Read by
    /// <see cref="Abs.FixedAssets.Services.IGlAccountResolver"/> as the
    /// per-tenant cascade rung between Book defaults and the industry-default
    /// constants.
    ///
    /// Seeded on tenant creation by <c>MasterDataBootstrapService</c> with
    /// the industry-default chart of accounts (see
    /// <c>docs/adr/ADR-003-central-gl-account-resolver.md</c>).
    /// </summary>
    [Table("CompanyGlAccountConfigs")]
    public class CompanyGlAccountConfig
    {
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required]
        public GlAccountKind AccountKind { get; set; }

        [Required, StringLength(20)]
        public string GlAccount { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Strongly-typed enum of every posting purpose the resolver can
    /// answer for. New posting flows that need a GL account string must
    /// add a value here and seed defaults via the migration that creates
    /// the use case.
    ///
    /// See ADR-003 §D-3-1 for the canonical list.
    /// </summary>
    public enum GlAccountKind
    {
        // Asset side
        AssetCost = 100,
        AccumulatedDepreciation = 110,
        DepreciationExpense = 120,
        GainOnDisposal = 130,
        LossOnDisposal = 140,

        // Inventory / receiving side
        Inventory = 200,
        GrAccrued = 210,                   // Goods received not yet invoiced
        DirectExpense = 220,               // Non-stock item direct charge
        WipExpense = 230,                  // Work-in-progress (PO line ties to a WO)

        // AP side
        AccountsPayable = 300,
        Cash = 310,
        PurchasePriceVariance = 320,

        // CIP side
        CipPending = 400,                  // Construction-in-progress accumulator

        // Maintenance side
        MaintenanceLabor = 500,
        MaintenanceMaterials = 510,
        MaintenanceOutsideVendor = 520,
        // Credit-side counterpart for internally-staffed maintenance labor.
        // The DR posts to MaintenanceLabor (expense); the CR sits in
        // AccruedLabor (liability) until payroll processing pays it down
        // against Cash. Added in PR #92 to complete the WO cost-rollup
        // pair with PR #89's materials posting.
        AccruedLabor = 530,
    }
}
