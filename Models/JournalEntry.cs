using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    /// <summary>
    /// Header record for a journal batch/entry.
    /// </summary>
    public class JournalEntry
    {
        public int Id { get; set; }

        /// <summary>
        /// Optional link to the Book this journal belongs to (e.g., GAAP, TAX).
        /// </summary>
        public int? BookId { get; set; }
        public Book? Book { get; set; }

        /// <summary>
        /// Period in yyyymm form (e.g., 202508 for Aug 2025). Simple, sortable, and what the UI expects.
        /// </summary>
        public int Period { get; set; }

        /// <summary>
        /// Batch identifier (required).
        /// </summary>
        [Required, StringLength(30)]
        public string Batch { get; set; } = string.Empty;

        /// <summary>
        /// Free-form reference (check number, run id, etc.).
        /// </summary>
        [StringLength(50)]
        public string? Reference { get; set; }

        /// <summary>
        /// Source system or generator (e.g., "Depreciation", "Manual").
        /// </summary>
        [StringLength(30)]
        public string? Source { get; set; }

        /// <summary>
        /// Posting date shown in UI; defaults to UTC date.
        /// </summary>
        public DateTime PostingDate { get; set; } = DateTime.UtcNow.Date;

        /// <summary>
        /// When the entry was created (the UI shows this).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Header description.
        /// </summary>
        [StringLength(200)]
        public string? Description { get; set; }

        public List<JournalLine> Lines { get; set; } = new();
    }
}