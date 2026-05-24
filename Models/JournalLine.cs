using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Masters;

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

        /// <summary>
        /// Legacy account-number string (e.g. "5610"). Set by the
        /// <c>IGlAccountResolver.ResolveAsync</c> path. Kept as the DEF-008
        /// fallback while <see cref="AccountingKeyId"/> rolls out across all
        /// posting services. A future cleanup PR drops this column once every
        /// read path consumes <see cref="AccountingKeyId"/>.
        /// </summary>
        [Required, StringLength(50)]
        public string Account { get; set; } = string.Empty;

        /// <summary>
        /// Sprint 13.5 PRA-5b — segment-keyed posting dimension. NULL until
        /// the posting service resolves it via
        /// <c>IGlAccountResolver.ResolveAccountingKeyAsync</c>. Existing rows
        /// were backfilled by the PRA-5b migration where the legacy
        /// <see cref="Account"/> string and <c>JournalEntry.Book.CompanyId</c>
        /// were both resolvable; orphan rows remain NULL and read through the
        /// legacy <see cref="Account"/> string.
        /// </summary>
        public int? AccountingKeyId { get; set; }
        public AccountingKey? AccountingKey { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        // Explicit precision for SQL Server to avoid truncation warnings
        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; }
    }
}