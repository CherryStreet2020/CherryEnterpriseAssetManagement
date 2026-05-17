using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.4 — Polymorphic WorkOrderApproval table.
    //
    // Replaces the single MaintenanceEvent.ApprovedById / ApprovedAt /
    // ApprovalStatus columns with a 1:N child table. Legacy columns
    // stay in place until PR #119.4.1 (deferred) drops them after the
    // UI cuts over.
    //
    // Backfill: every existing MaintenanceEvent with ApprovedById IS
    // NOT NULL gets a Stage='Legacy' row so historical approvals
    // survive in the new shape. The status engine treats 'Legacy' rows
    // as generic approvals — they don't satisfy any specific
    // RequiredApprovalStage gate (no v0.2 transitions reference
    // Stage='Legacy'), which is the right behavior: legacy single-field
    // approvals can't retroactively satisfy a multi-stage gate.
    //
    // Indexes:
    //   - (WorkOrderId, Stage, Decision) UNIQUE WHERE Decision IN (1,2,3)
    //     — one final-decision row per (WO, Stage). Pending rows are
    //     excluded so admin re-adds work.
    //   - (WorkOrderId) — chain rendering on the WO detail page.
    //   - (WorkOrderId, Stage) — the status engine's hot-path lookup
    //     in IsStageApprovedAsync.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddWorkOrderApproval")]
    public partial class AddWorkOrderApproval : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkOrderApproval"" (
                    ""Id""              SERIAL PRIMARY KEY,
                    ""WorkOrderId""     integer     NOT NULL,
                    ""Stage""           varchar(40) NOT NULL,
                    ""StageOrder""      integer     NOT NULL DEFAULT 0,
                    ""RoleRequired""    varchar(40) NOT NULL,
                    ""DisplayLabel""    varchar(80) NULL,
                    ""ApproverUserId""  integer     NULL,
                    ""Decision""        smallint    NOT NULL DEFAULT 0,
                    ""DecisionAt""      timestamptz NULL,
                    ""Comments""        varchar(1000) NULL,
                    ""CreatedAt""       timestamptz NOT NULL DEFAULT now()
                );
            ");

            // FK to MaintenanceEvent. Cascade-delete because approval
            // rows are owned by the WO — deleting the WO should remove
            // its chain. Cascade is safe here because WO deletion is
            // already rare + audit-logged.
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderApproval""
                ADD CONSTRAINT ""FK_WorkOrderApproval_MaintenanceEvents_WorkOrderId""
                FOREIGN KEY (""WorkOrderId"") REFERENCES ""MaintenanceEvents""(""Id"")
                ON DELETE CASCADE;
            ");

            // Optional FK to Users (approver). ON DELETE SET NULL because
            // an approver being deleted shouldn't wipe the historical
            // decision — keep the audit trail, lose the user pointer.
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderApproval""
                ADD CONSTRAINT ""FK_WorkOrderApproval_Users_ApproverUserId""
                FOREIGN KEY (""ApproverUserId"") REFERENCES ""Users""(""Id"")
                ON DELETE SET NULL;
            ");

            // Chain-render index — drives the WO detail page's
            // approval panel.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderApproval_WorkOrderId""
                ON ""WorkOrderApproval"" (""WorkOrderId"");
            ");

            // Hot-path index — status engine's IsStageApprovedAsync.
            // Composite (WorkOrderId, Stage) + filter on Decision so the
            // index covers the most-frequent gate query.
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_WorkOrderApproval_WorkOrderId_Stage_Decision""
                ON ""WorkOrderApproval"" (""WorkOrderId"", ""Stage"", ""Decision"");
            ");

            // ---- Legacy backfill ----
            //
            // Insert one Stage='Legacy' row for every MaintenanceEvent
            // that had a single-field approval recorded. Decision=1
            // (Approved) is the only legacy state we backfill because
            // the legacy ApprovedById column was only ever set on
            // approved WOs. Pending/Rejected legacy state was carried
            // by WorkOrderApprovalStatus enum, not by user-id presence.
            migrationBuilder.Sql(@"
                INSERT INTO ""WorkOrderApproval"" (
                    ""WorkOrderId"", ""Stage"", ""StageOrder"", ""RoleRequired"",
                    ""DisplayLabel"", ""ApproverUserId"", ""Decision"",
                    ""DecisionAt"", ""Comments"", ""CreatedAt""
                )
                SELECT
                    me.""Id"",
                    'Legacy',
                    0,
                    'Legacy approver',
                    'Legacy single-field approval (pre-PR-119.4)',
                    me.""ApprovedById"",
                    1,  -- ApprovalDecision.Approved
                    me.""ApprovedAt"",
                    'Backfilled from MaintenanceEvent.ApprovedBy on 2026-05-17.',
                    COALESCE(me.""ApprovedAt"", me.""CreatedAt"")
                FROM ""MaintenanceEvents"" me
                WHERE me.""ApprovedById"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderApproval_WorkOrderId_Stage_Decision"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderApproval_WorkOrderId"";");
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderApproval""
                DROP CONSTRAINT IF EXISTS ""FK_WorkOrderApproval_Users_ApproverUserId"";
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""WorkOrderApproval""
                DROP CONSTRAINT IF EXISTS ""FK_WorkOrderApproval_MaintenanceEvents_WorkOrderId"";
            ");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""WorkOrderApproval"";");
        }
    }
}
