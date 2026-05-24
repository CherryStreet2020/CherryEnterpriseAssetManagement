// Sprint 13.5 PR #5d — LaborEntry + ReasonCode tables.
//
// AUTHORITY:
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
//   - memory: reference_bic_entity_checklist.md (6-point gate)
//   - memory: feedback_replit_autodiff_destructive_on_populated_tables.md
//     (this migration goes through psql pre-apply pattern, NOT Replit auto-diff)
//
// TABLES CREATED:
//   1. LaborEntries  — operator clock-in/clock-out events (TENANT-SCOPED).
//      Direct CompanyId + LocationId NOT NULL per BIC. One-open-clock-in
//      rule enforced via partial UNIQUE on (CompanyId, OperatorUserId)
//      WHERE ClockOutAt IS NULL.
//
//   2. ReasonCodes   — Scrap/Rework/Downtime/Hold reason catalog
//      (CROSS-TENANT REFERENCE PATTERN like MaterialMaster). Nullable
//      CompanyId. Two partial UNIQUEs:
//         IX_ReasonCodes_System_Category_Code  WHERE CompanyId IS NULL
//         IX_ReasonCodes_Company_Category_Code WHERE CompanyId IS NOT NULL
//      (NO COALESCE-in-index — Replit prod-validator gotcha.)
//
// NO HARDCODED TENANT DATA — system reason codes seed via
// seed/reference-data/reason-codes.json in PR #5c.4 (dev-only pipeline).

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524130000_AddLaborAndReasonCodes")]
    public partial class AddLaborAndReasonCodes : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) LaborEntries — operator clock-in/out events
            // ============================================================

            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""LaborEntries"" (
                    ""Id""                     serial          PRIMARY KEY,
                    ""CompanyId""              integer         NOT NULL,
                    ""LocationId""             integer         NOT NULL,
                    ""ProductionOperationId""  integer         NOT NULL REFERENCES ""ProductionOperations""(""Id"") ON DELETE RESTRICT,
                    ""OperatorUserId""         integer         NOT NULL REFERENCES ""Users""(""Id"") ON DELETE RESTRICT,
                    ""LaborTypeId""            integer         NULL,
                    ""ClockInAt""              timestamptz     NOT NULL,
                    ""ClockOutAt""             timestamptz     NULL,
                    ""DurationMins""           numeric(10,2)   NULL,
                    ""Notes""                  varchar(2000)   NULL,
                    ""CreatedAt""              timestamptz     NOT NULL DEFAULT now(),
                    ""CreatedBy""              varchar(100)    NULL,
                    ""ModifiedAt""             timestamptz     NULL,
                    ""ModifiedBy""             varchar(100)    NULL
                );
            ");

            // BIC checks — tenant trio must be non-negative.
            mb.Sql(@"
                ALTER TABLE ""LaborEntries""
                    ADD CONSTRAINT ""CK_LaborEntries_CompanyIdNonNeg""  CHECK (""CompanyId""  >= 0),
                    ADD CONSTRAINT ""CK_LaborEntries_LocationIdNonNeg"" CHECK (""LocationId"" >= 0);
            ");

            // Clock window sanity: ClockOut must be after ClockIn when set.
            mb.Sql(@"
                ALTER TABLE ""LaborEntries""
                    ADD CONSTRAINT ""CK_LaborEntries_ClockWindow""
                    CHECK (""ClockOutAt"" IS NULL OR ""ClockOutAt"" >= ""ClockInAt"");
            ");

            // Indexes — query patterns: "all labor on this op", "my recent
            // entries", "currently clocked-in operators at this location".
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_LaborEntries_Op_ClockIn""
                ON ""LaborEntries""(""ProductionOperationId"", ""ClockInAt"" DESC);
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_LaborEntries_Operator_ClockIn""
                ON ""LaborEntries""(""OperatorUserId"", ""ClockInAt"" DESC);
            ");
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_LaborEntries_Company_Location""
                ON ""LaborEntries""(""CompanyId"", ""LocationId"");
            ");

            // ONE-OPEN-CLOCK-IN RULE: partial UNIQUE on
            // (CompanyId, OperatorUserId) WHERE ClockOutAt IS NULL.
            // An operator can't be clocked into two operations at once.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LaborEntries_OpenClockIn_Unique""
                ON ""LaborEntries""(""CompanyId"", ""OperatorUserId"")
                WHERE ""ClockOutAt"" IS NULL;
            ");

            // ============================================================
            // 2) ReasonCodes — cross-tenant reference pattern (nullable Company)
            // ============================================================

            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ReasonCodes"" (
                    ""Id""             serial          PRIMARY KEY,
                    ""CompanyId""      integer         NULL,
                    ""Code""           varchar(32)     NOT NULL,
                    ""Description""    varchar(200)    NOT NULL,
                    ""Category""       integer         NOT NULL DEFAULT 0,
                    ""SortOrder""      integer         NOT NULL DEFAULT 100,
                    ""IsActive""       boolean         NOT NULL DEFAULT true,
                    ""CreatedAt""      timestamptz     NOT NULL DEFAULT now(),
                    ""CreatedBy""      varchar(100)    NULL,
                    ""ModifiedAt""     timestamptz     NULL,
                    ""ModifiedBy""     varchar(100)    NULL
                );
            ");

            // Sanity: Category must be a known enum value (0..3 + 99).
            mb.Sql(@"
                ALTER TABLE ""ReasonCodes""
                    ADD CONSTRAINT ""CK_ReasonCodes_Category""
                    CHECK (""Category"" IN (0, 1, 2, 3, 99));
            ");

            // Partial Company index for tenant lookups (NULL = system codes).
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ReasonCodes_Company""
                ON ""ReasonCodes""(""CompanyId"")
                WHERE ""CompanyId"" IS NOT NULL;
            ");

            // Category lookup (filter by Scrap / Rework / Downtime / Hold / Other).
            mb.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ReasonCodes_Category""
                ON ""ReasonCodes""(""Category"");
            ");

            // Two partial UNIQUEs (system + tenant scope).
            // NO COALESCE-in-index — Replit gotcha lesson from PR #5c.1.1.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReasonCodes_System_Category_Code""
                ON ""ReasonCodes""(""Category"", ""Code"")
                WHERE ""CompanyId"" IS NULL;
            ");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReasonCodes_Company_Category_Code""
                ON ""ReasonCodes""(""CompanyId"", ""Category"", ""Code"")
                WHERE ""CompanyId"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP TABLE IF EXISTS ""LaborEntries"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""ReasonCodes"" CASCADE;");
        }
    }
}
