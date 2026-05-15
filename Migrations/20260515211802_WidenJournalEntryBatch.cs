using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // 2026-05-15: JournalEntries.Batch was varchar(30). ApPostingService
    // builds Batch = $"AP-APR-{invoice.InvoiceNumber}", which overflowed
    // when InvoiceNumber exceeded 23 chars (PostgresException 22001 "value
    // too long for type character varying(30)"). Surfaced during PR #82
    // verification with invoice "E2E-INV-FIXVERIFY-205700" (24 chars
    // → 31-char Batch).
    //
    // The bug was latent on main pre-#82 because the broken zero-dollar
    // approval JE happened to use short test invoice numbers and slid
    // under the cap by accident. Now that #82 produces real JEs reliably,
    // long invoice numbers will reliably 500 without this widening.
    //
    // Widen to varchar(60). Industrial AP batch keys (SAP MM BELNR + suffix,
    // Oracle EBS BATCH_NAME, Maximo INVOICE) routinely run 30–50 chars;
    // 60 gives headroom for prefix + composite key + sequence without
    // further migration churn. Same widening dimension as
    // 20260515150000_WidenItemTransactionNumber (PR #67).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260515211802_WidenJournalEntryBatch")]
    public partial class WidenJournalEntryBatch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""JournalEntries""
                ALTER COLUMN ""Batch"" TYPE character varying(60);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Truncation risk on downgrade. Trim oversized rows first so the
            // type narrowing succeeds. This is a one-way migration in practice
            // but we keep Down honest. Same shape as
            // 20260515150000_WidenItemTransactionNumber's Down().
            migrationBuilder.Sql(@"
                UPDATE ""JournalEntries"" SET ""Batch"" = LEFT(""Batch"", 30)
                WHERE LENGTH(""Batch"") > 30;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""JournalEntries""
                ALTER COLUMN ""Batch"" TYPE character varying(30);
            ");
        }
    }
}
