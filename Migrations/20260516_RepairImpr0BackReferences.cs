using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // PR #108 / B-26: One-shot repair migration for IMPR:0 back-references.
    //
    // PR #99 fixed the forward-going bug where CapitalImprovement.Id was read
    // BEFORE SaveChangesAsync, producing CustomField2 = "IMPR:0" on the linked
    // MaintenanceEvent instead of the actual improvement id. This migration
    // walks every MaintenanceEvent row still carrying IMPR:0 and repairs the
    // back-reference by joining to CapitalImprovement on:
    //   - AssetId (must match the MaintenanceEvent's AssetId)
    //   - ImprovementDate within ±3 days of the MaintenanceEvent's CreatedAt
    //   - Cost within $1.00 of the WO's COALESCE(ActualCost, EstimatedCost, 0)
    //
    // Only updates rows that have EXACTLY ONE candidate. Ambiguous (2+) and
    // no-match cases are left as IMPR:0 for admin review — auto-picking would
    // risk reattaching to the wrong improvement on a heavy-traffic asset.
    //
    // Idempotent on retry (the next run finds no IMPR:0 rows). Raw SQL pattern
    // matches PR #67 / #82 / #101 / #104.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516_RepairImpr0BackReferences")]
    public partial class RepairImpr0BackReferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Single-statement repair: a CTE counts candidates per orphan, and
            // the UPDATE applies only where exactly one match exists. Ambiguous
            // rows (cnt > 1) and no-match rows (cnt = 0) are left untouched.
            // The probe_cost expression mirrors the C# fallback chain:
            // ActualCost > EstimatedCost > 0.
            migrationBuilder.Sql(@"
                WITH orphans AS (
                    SELECT
                        me.""Id"" AS me_id,
                        me.""AssetId"" AS asset_id,
                        me.""CreatedAt"" AS created_at,
                        COALESCE(me.""ActualCost"", me.""EstimatedCost"", 0) AS probe_cost
                    FROM ""MaintenanceEvents"" me
                    WHERE me.""CustomField2"" = 'IMPR:0'
                ),
                candidates AS (
                    SELECT
                        o.me_id,
                        o.asset_id,
                        ci.""Id"" AS ci_id,
                        COUNT(*) OVER (PARTITION BY o.me_id) AS match_count
                    FROM orphans o
                    JOIN ""CapitalImprovements"" ci
                        ON ci.""AssetId"" = o.asset_id
                       AND ci.""ImprovementDate"" >= o.created_at - INTERVAL '3 days'
                       AND ci.""ImprovementDate"" <= o.created_at + INTERVAL '3 days'
                       AND ABS(ci.""Cost"" - o.probe_cost) <= 1.0
                ),
                unique_matches AS (
                    SELECT me_id, ci_id
                    FROM candidates
                    WHERE match_count = 1
                )
                UPDATE ""MaintenanceEvents"" me
                SET ""CustomField2"" = 'IMPR:' || um.ci_id
                FROM unique_matches um
                WHERE me.""Id"" = um.me_id;
            ");

            // Log the post-repair state so the next operator can see what was
            // left behind for admin review. A plain SELECT in a migration is a
            // no-op for DDL but EF Core will execute it. The output goes to
            // the migration log in the Postgres server logs, not the app log;
            // for a richer report run the same query manually after migration.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    remaining int;
                BEGIN
                    SELECT COUNT(*) INTO remaining FROM ""MaintenanceEvents"" WHERE ""CustomField2"" = 'IMPR:0';
                    IF remaining > 0 THEN
                        RAISE NOTICE 'RepairImpr0BackReferences: % rows still IMPR:0 (no match or ambiguous) — see admin review notes', remaining;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration is a no-op. The Up direction is informational
            // (it merely re-labels a free-text column) and there's no clean
            // way to recover the pre-migration "IMPR:0" sentinel because the
            // mapping was inherently lossy. Rolling back the parent PR would
            // simply leave the now-correct ids in place, which is harmless.
        }
    }
}
