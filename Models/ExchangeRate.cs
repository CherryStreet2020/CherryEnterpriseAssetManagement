using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class ExchangeRate
    {
        public int Id { get; set; }

        [Required, StringLength(3)]
        public string FromCurrency { get; set; } = "USD";

        [Required, StringLength(3)]
        public string ToCurrency { get; set; } = "CAD";

        public decimal Rate { get; set; }

        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpirationDate { get; set; }

        [StringLength(100)]
        public string? Source { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }
}
