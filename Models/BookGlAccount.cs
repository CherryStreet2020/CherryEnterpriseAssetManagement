using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Per-book, per-company GL account mapping.
    /// One row per BookId.
    /// </summary>
    public class BookGlAccount
    {
        public int Id { get; set; }

        [Required]
        public int BookId { get; set; }

        // Optional: navigation property if you want it
        public Book? Book { get; set; }

        [Display(Name = "Asset")]
        [StringLength(50)]
        public string? Asset { get; set; }

        [Display(Name = "Accumulated Depreciation")]
        [StringLength(50)]
        public string? AccumulatedDepreciation { get; set; }

        [Display(Name = "Depreciation Expense")]
        [StringLength(50)]
        public string? DepreciationExpense { get; set; }

        [Display(Name = "Gain on Disposal")]
        [StringLength(50)]
        public string? GainOnDisposal { get; set; }

        [Display(Name = "Loss on Disposal")]
        [StringLength(50)]
        public string? LossOnDisposal { get; set; }

        [Display(Name = "Clearing / WIP (optional)")]
        [StringLength(50)]
        public string? Clearing { get; set; }

        [Display(Name = "Construction in Progress (CIP)")]
        [StringLength(50)]
        public string? CIP { get; set; }
    }
}