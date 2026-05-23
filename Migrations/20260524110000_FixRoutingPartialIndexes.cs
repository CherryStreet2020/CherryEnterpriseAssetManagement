// Sprint 13.5 PR #5c.1.1 — HOTFIX for the COALESCE-in-index pattern that
// tripped Replit's prod-migration validator.
//
// PR #5c.1 (Migration 20260524100000) created `IX_Routings_Company_Location_Code_Rev`
// as `(CompanyId, COALESCE(LocationId, 0), Code, RevisionNumber)` to enforce a
// composite UNIQUE that treated NULL LocationId (company-wide template) as 0.
//
// That index ran fine via EF Core on dev but Replit's prod validator's
// migration parser stripped the column quoting around `CompanyId / Code /
// RevisionNumber` (only `"LocationId"` retained its quotes because of the
// COALESCE wrapper) and the resulting `column "companyid" does not exist`
// rejection blocked prod deployment.
//
// FIX: replace the single COALESCE-in-index with two PARTIAL indexes —
//   - `IX_Routings_Site_Code_Rev` UNIQUE ON (CompanyId, LocationId, Code, RevisionNumber)
//     WHERE LocationId IS NOT NULL — site-scoped routings
//   - `IX_Routings_Template_Code_Rev` UNIQUE ON (CompanyId, Code, RevisionNumber)
//     WHERE LocationId IS NULL — company-wide engineering templates
// Functionally equivalent semantics, no COALESCE expression, validator-safe.
//
// Idempotent: DROP IF EXISTS the bad index (dev DBs that ran PR #5c.1 have it),
// CREATE IF NOT EXISTS the two partial indexes.
//
// Cross-refs:
//   - Migrations/20260524100000_LineageHardeningRoutingWorkCenterOp.cs (PR #5c.1 — the buggy index)
//   - memory: reference_hard_rules (Replit publish validator quoting gotcha — adding now)

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524110000_FixRoutingPartialIndexes")]
    public partial class FixRoutingPartialIndexes : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // Drop the bad COALESCE-in-index version (PR #5c.1 created it on dev).
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_Routings_Company_Location_Code_Rev"";");

            // Site-scoped routings: unique per (Company, Site, Code, Rev).
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Site_Code_Rev""
                ON ""Routings""(""CompanyId"", ""LocationId"", ""Code"", ""RevisionNumber"")
                WHERE ""LocationId"" IS NOT NULL;
            ");

            // Company-wide engineering templates: unique per (Company, Code, Rev),
            // one template per code+rev across the whole company.
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Template_Code_Rev""
                ON ""Routings""(""CompanyId"", ""Code"", ""RevisionNumber"")
                WHERE ""LocationId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_Routings_Template_Code_Rev"";");
            mb.Sql(@"DROP INDEX IF EXISTS ""IX_Routings_Site_Code_Rev"";");
            mb.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Company_Location_Code_Rev""
                ON ""Routings""(""CompanyId"", COALESCE(""LocationId"", 0), ""Code"", ""RevisionNumber"");
            ");
        }
    }
}
