using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // 2026-05-15: ItemTransactions.TransactionNumber was varchar(20).
    // Every receive-flow ItemTransaction insert from ReceivingPostingService
    // and every WO-issue ItemTransaction insert from
    // /WorkOrders/Details::OnPostIssueMaterialAsync threw PostgresException
    // 22001 ("value too long for type character varying(20)") because both
    // sites generate numbers like "GR7-L1-{DateTime.UtcNow.Ticks}" (~24-27
    // chars). The error was swallowed by the inner try/catch in the page
    // handler, so receipts persisted but JE + inventory + outbox never
    // committed.
    //
    // Widen to varchar(60). Industrial transaction numbers (SAP MM IBLNR,
    // Oracle EBS TXN_REF, Maximo MATRECTRANS) are typically 30-50 chars —
    // 60 gives us headroom for prefix + composite key + timestamp.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260515150000_WidenItemTransactionNumber")]
    public partial class WidenItemTransactionNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ItemTransactions""
                ALTER COLUMN ""TransactionNumber"" TYPE character varying(60);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Truncation risk on downgrade. Use a CASE expression to trim
            // existing rows rather than fail. This is a one-way migration
            // in practice but we keep Down honest.
            migrationBuilder.Sql(@"
                UPDATE ""ItemTransactions"" SET ""TransactionNumber"" = LEFT(""TransactionNumber"", 20)
                WHERE LENGTH(""TransactionNumber"") > 20;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""ItemTransactions""
                ALTER COLUMN ""TransactionNumber"" TYPE character varying(20);
            ");
        }
    }
}
