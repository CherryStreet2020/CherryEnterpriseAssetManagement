using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-003: central GL account resolver. Adds CompanyGlAccountConfigs
    // table — one row per (CompanyId, GlAccountKind) — as the per-tenant
    // rung in the resolver cascade. See docs/adr/ADR-003-central-gl-account-resolver.md.
    //
    // Schema additive. Backfill seeds one row per existing company × every
    // GlAccountKind enum value, using the industry-default chart of accounts
    // (mirrors GlAccountResolver.IndustryDefaults). Existing posting sites
    // continue to read their hardcoded strings until each S1/S2 migration
    // PR routes them through the resolver.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508150000_AddCompanyGlAccountConfigs")]
    public partial class AddCompanyGlAccountConfigs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""CompanyGlAccountConfigs"" (
                    ""Id"" serial PRIMARY KEY,
                    ""CompanyId"" integer NOT NULL,
                    ""AccountKind"" integer NOT NULL,
                    ""GlAccount"" varchar(20) NOT NULL,
                    ""Notes"" varchar(500) NULL,
                    ""CreatedAt"" timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC'),
                    ""UpdatedAt"" timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'UTC')
                );
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_CompanyGlAccountConfigs_Companies_CompanyId'
                    ) THEN
                        ALTER TABLE ""CompanyGlAccountConfigs""
                        ADD CONSTRAINT ""FK_CompanyGlAccountConfigs_Companies_CompanyId""
                        FOREIGN KEY (""CompanyId"")
                        REFERENCES ""Companies"" (""Id"")
                        ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_CompanyGlAccountConfigs_CompanyKind""
                ON ""CompanyGlAccountConfigs"" (""CompanyId"", ""AccountKind"");
            ");

            // Seed the industry-default chart for every existing company.
            // Each kind is keyed by its enum int (matches Models/CompanyGlAccountConfig.cs).
            // Idempotent: skips rows that already exist for (CompanyId, AccountKind).
            //   AssetCost=100 1500
            //   AccumulatedDepreciation=110 1510
            //   DepreciationExpense=120 6500
            //   GainOnDisposal=130 4500
            //   LossOnDisposal=140 6510
            //   Inventory=200 1300
            //   GrAccrued=210 2150
            //   DirectExpense=220 6000
            //   WipExpense=230 1410
            //   AccountsPayable=300 2000
            //   Cash=310 1110
            //   PurchasePriceVariance=320 5900
            //   CipPending=400 1400
            //   MaintenanceLabor=500 6200
            //   MaintenanceMaterials=510 6210
            //   MaintenanceOutsideVendor=520 6220
            migrationBuilder.Sql(@"
                INSERT INTO ""CompanyGlAccountConfigs"" (""CompanyId"", ""AccountKind"", ""GlAccount"", ""CreatedAt"", ""UpdatedAt"")
                SELECT c.""Id"", k.kind, k.acct, (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""Companies"" c
                CROSS JOIN (VALUES
                    (100, '1500'),
                    (110, '1510'),
                    (120, '6500'),
                    (130, '4500'),
                    (140, '6510'),
                    (200, '1300'),
                    (210, '2150'),
                    (220, '6000'),
                    (230, '1410'),
                    (300, '2000'),
                    (310, '1110'),
                    (320, '5900'),
                    (400, '1400'),
                    (500, '6200'),
                    (510, '6210'),
                    (520, '6220')
                ) AS k(kind, acct)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""CompanyGlAccountConfigs"" existing
                    WHERE existing.""CompanyId"" = c.""Id""
                      AND existing.""AccountKind"" = k.kind
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CompanyGlAccountConfigs"";");
        }
    }
}
