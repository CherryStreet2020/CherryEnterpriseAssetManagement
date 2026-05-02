using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Detail line for a journal entry.
    /// </summary>
    public class JournalLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }
        public JournalEntry? JournalEntry { get; set; }

        /// <summary>
        /// Line number (the UI reads this as LineNo).
        /// </summary>
        public int LineNo { get; set; }

        [Required, StringLength(50)]
        public string Account { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        // Explicit precision for SQL Server to avoid truncation warnings
        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }
    }
}