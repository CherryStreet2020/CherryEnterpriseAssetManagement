// =============================================================================
// Sprint 13.5 PR #5c.4 — System ReasonCodes seed + MaterialStructure CompanyId
// orphan backfill.
//
// PURE ADDITIVE — no schema changes. The ReasonCodes table was created in PR
// #5d (Migration 20260524130000_AddLaborAndReasonCodes); this migration just
// loads the canonical system rows (CompanyId IS NULL) per
// seed/reference-data/reason-codes.json.
//
// MaterialStructure CompanyId orphan backfill — closes out the PR #5c.2
// grace period. If any MaterialStructure rows have CompanyId IS NULL, fill
// them from the parent Item's CompanyId so the tenant-trio invariant
// holds across the table.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md (cascade plan)
//   - memory: reference_master_files_baseline.md
//   - memory: project_pr_5c2_shipped.md (the grace-period backfill)
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock)
//
// IDEMPOTENT — INSERT ... WHERE NOT EXISTS on every row, UPDATE only where
// CompanyId IS NULL.
//
// NO HARDCODED TENANT DATA — all ReasonCode rows are CompanyId NULL (system).
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524180000_AddSystemReasonCodesPR5c4")]
    public partial class AddSystemReasonCodesPR5c4 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ----------------------------------------------------------------
            // 1) Seed 26 system ReasonCodes (CompanyId IS NULL).
            //
            // Schema (from PR #5d):
            //   CompanyId NULLABLE / Code varchar(32) / Description varchar(200)
            //   Category int (enum: 0=Scrap 1=Rework 2=Downtime 3=Hold 99=Other)
            //   SortOrder / IsActive / CreatedAt / CreatedBy / ModifiedAt / ModifiedBy
            //
            // Idempotency via WHERE NOT EXISTS against the partial UNIQUE shape
            // IX_ReasonCodes_System_Category_Code WHERE CompanyId IS NULL.
            // ----------------------------------------------------------------
            mb.Sql(@"
                -- SCRAP (Category=0)
                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'SC-MATL',   'Material defect — incoming raw material out of spec',                 0, 10, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=0 AND ""Code""='SC-MATL');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'SC-OP',     'Operator error — wrong setup, wrong feed, mis-pick',                  0, 20, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=0 AND ""Code""='SC-OP');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'SC-SETUP',  'Setup error — first-piece out of tolerance',                          0, 30, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=0 AND ""Code""='SC-SETUP');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'SC-EQUIP',  'Equipment failure — tool break, fixture slip, axis drift',            0, 40, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=0 AND ""Code""='SC-EQUIP');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'SC-PROG',   'Program / NC defect — toolpath or offset wrong',                      0, 50, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=0 AND ""Code""='SC-PROG');

                -- REWORK (Category=1)
                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'RW-DIM',    'Dimensional out-of-spec — needs re-machining',                        1, 10, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=1 AND ""Code""='RW-DIM');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'RW-FIN',    'Surface finish below spec — needs polish / re-finish',                1, 20, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=1 AND ""Code""='RW-FIN');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'RW-COSM',   'Cosmetic defect — scratch / mark / discoloration',                    1, 30, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=1 AND ""Code""='RW-COSM');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'RW-ASSY',   'Assembly defect — needs disassembly / rework',                        1, 40, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=1 AND ""Code""='RW-ASSY');

                -- DOWNTIME (Category=2)
                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-SETUP',  'Planned setup — machine being set up for next job',                   2, 10, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-SETUP');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-PM',     'Preventive maintenance — scheduled PM activity',                      2, 20, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-PM');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-BRKDN',  'Unplanned breakdown — machine down for repair',                       2, 30, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-BRKDN');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-MATL',   'Waiting on material — material not at machine',                       2, 40, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-MATL');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-TOOL',   'Waiting on tooling — tool not available or broken',                   2, 50, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-TOOL');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-PROG',   'Waiting on program / NC file',                                        2, 60, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-PROG');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-INSP',   'Waiting on inspection / quality release',                             2, 70, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-INSP');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-BREAK',  'Operator break / shift change',                                       2, 80, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-BREAK');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'DT-MTG',    'Meeting / training / shift huddle',                                   2, 90, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=2 AND ""Code""='DT-MTG');

                -- HOLD (Category=3)
                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-CUST',   'Waiting on customer — confirmation / spec change / hold instruction', 3, 10, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-CUST');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-MATL',   'Waiting on material — incoming PO not received',                      3, 20, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-MATL');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-NCR',    'NCR open — non-conformance under review',                             3, 30, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-NCR');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-ENG',    'Engineering review — drawing or spec being revised',                  3, 40, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-ENG');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-CREDIT', 'Credit hold — customer financial',                                    3, 50, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-CREDIT');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-QUAL',   'Quality hold — pending inspection / disposition',                     3, 60, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-QUAL');

                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'HD-CAP',    'Capacity hold — work center over capacity',                           3, 70, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=3 AND ""Code""='HD-CAP');

                -- OTHER (Category=99)
                INSERT INTO ""ReasonCodes"" (""CompanyId"", ""Code"", ""Description"", ""Category"", ""SortOrder"", ""IsActive"", ""CreatedAt"")
                SELECT NULL, 'OTHER',     'Other — operator to add note in detail field',                        99, 999, TRUE, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""ReasonCodes"" WHERE ""CompanyId"" IS NULL AND ""Category""=99 AND ""Code""='OTHER');
            ");

            // ----------------------------------------------------------------
            // 2) MaterialStructure CompanyId orphan backfill (close PR #5c.2
            //    grace period).
            //
            // PR #5c.2 added CompanyId NOT NULL on most production-domain
            // tables. MaterialStructure was kept nullable to grandfather
            // existing rows for one ship-cycle. This is the second cycle —
            // backfill from the parent Item's CompanyId, then keep nullable
            // until a follow-up PR formally tightens to NOT NULL.
            //
            // Idempotent: UPDATE only WHERE CompanyId IS NULL.
            // ----------------------------------------------------------------
            mb.Sql(@"
                UPDATE ""MaterialStructures"" ms
                SET ""CompanyId"" = i.""CompanyId""
                FROM ""Items"" i
                WHERE ms.""CompanyId"" IS NULL
                  AND ms.""ItemId"" = i.""Id""
                  AND i.""CompanyId"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Remove only the system rows this migration added.
            mb.Sql(@"
                DELETE FROM ""ReasonCodes""
                WHERE ""CompanyId"" IS NULL
                  AND ""Code"" IN (
                    'SC-MATL','SC-OP','SC-SETUP','SC-EQUIP','SC-PROG',
                    'RW-DIM','RW-FIN','RW-COSM','RW-ASSY',
                    'DT-SETUP','DT-PM','DT-BRKDN','DT-MATL','DT-TOOL','DT-PROG','DT-INSP','DT-BREAK','DT-MTG',
                    'HD-CUST','HD-MATL','HD-NCR','HD-ENG','HD-CREDIT','HD-QUAL','HD-CAP',
                    'OTHER'
                  );
            ");
        }
    }
}
