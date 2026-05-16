using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // PR #101: Multi-tenant API key scoping.
    //
    // Pre-#101, ApiKey had no tenant binding. AssetsApiController authenticated
    // a bearer token and then queried _context.Assets.AsQueryable() with no
    // scoping — a valid key from Tenant A would return every asset across every
    // tenant. This migration adds TenantId (NOT NULL) and CompanyId (NULL) so
    // each key declares which tenant issued it and, optionally, which single
    // company it is restricted to.
    //
    // Existing rows are backfilled with TenantId = 0. Zero is the sentinel for
    // "issued before scoping was enforced" — ApiService.ValidateKeyAsync still
    // returns those rows, but RequireApiKeyWithTenantScope() in
    // AssetsApiController refuses them with a 403 instructing the admin to
    // re-issue. We do not silently bind orphaned keys to an arbitrary tenant.
    //
    // Raw SQL, same shape as PR #67 (20260515150000_WidenItemTransactionNumber)
    // and PR #82 (20260515211802_WidenJournalEntryBatch). No EF scaffolding —
    // EF would otherwise emit a model snapshot churn the team has agreed to
    // defer until RLS (PR #120).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_AddTenantIdToApiKey")]
    public partial class AddTenantIdToApiKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApiKeys""
                ADD COLUMN IF NOT EXISTS ""TenantId"" integer NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApiKeys""
                ADD COLUMN IF NOT EXISTS ""CompanyId"" integer NULL;
            ");
            // Backfill is implicit via DEFAULT 0 on ADD COLUMN, but be explicit
            // about intent for any rows EF Core created with the column pre-
            // existing (e.g. partial roll-forward states).
            migrationBuilder.Sql(@"
                UPDATE ""ApiKeys"" SET ""TenantId"" = 0 WHERE ""TenantId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApiKeys"" DROP COLUMN IF EXISTS ""CompanyId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApiKeys"" DROP COLUMN IF EXISTS ""TenantId"";
            ");
        }
    }
}
