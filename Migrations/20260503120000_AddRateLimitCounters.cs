using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Creates "RateLimitCounters" used by PostgresLoginRateLimiter.
    // Idempotent (CREATE ... IF NOT EXISTS).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260503120000_AddRateLimitCounters")]
    public partial class AddRateLimitCounters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RateLimitCounters"" (
                    ""Id""             bigserial      PRIMARY KEY,
                    ""PartitionKey""   varchar(256)   NOT NULL,
                    ""WindowStartUtc"" timestamptz    NOT NULL,
                    ""Count""          integer        NOT NULL DEFAULT 0,
                    ""CreatedAtUtc""   timestamptz    NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
                    ""UpdatedAtUtc""   timestamptz    NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_RateLimitCounters_Partition_Window""
                ON ""RateLimitCounters"" (""PartitionKey"", ""WindowStartUtc"");
            ");

            // Index supports the periodic cleanup of stale rows (older than
            // a few minutes) without a sequential scan.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_RateLimitCounters_WindowStartUtc""
                ON ""RateLimitCounters"" (""WindowStartUtc"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RateLimitCounters_WindowStartUtc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_RateLimitCounters_Partition_Window"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RateLimitCounters"";");
        }
    }
}
