// Sprint 13.5 PR #5c — Routing + WorkCenter + ProductionOperation
//
// What this migration does:
//   1) WorkCenters table (dispatch unit master) + indexes + CHECKs +
//      seed 8 demo work centers for ABS Machining (CompanyId=1)
//   2) WorkCenterAssetLinks join (N:N WorkCenter ↔ Asset, with primary flag
//      and effective-range history)
//   3) Routings header (manufacturing method master, versioned)
//   4) RoutingOperations child rows (5-time decomp, ordered, parallel-capable)
//   5) ProductionOperations execution-time rows (snapshot from RoutingOperation
//      at release; 8-state status machine; the universal entity that the
//      MES event layer #5e/#5f/#5g all FK to)
//
// MULTI-VERTICAL POLYMORPHISM (locked):
//   ProductionType.JobShop / RepetitiveDiscrete / CapitalETO -> use Routing
//   ProductionType.ProcessBatch -> continue using Recipe + RecipePhase (PR #1.5)
//   Recipe/RecipePhase NOT touched.
//
// CHECK CONSTRAINTS (14):
//   - WorkCenters Status / Type / CapacityModel enum bounds
//   - EfficiencyPct / UtilizationPct 0-200
//   - Routings Type / Status enum bounds
//   - LotBaseSize > 0
//   - RoutingOperations SequenceNumber > 0
//   - Setup/Run/Queue/Move/Wait times >= 0
//   - YieldPct 0-100
//   - ProductionOperations SequenceNumber > 0
//   - Status enum bounds (0..7)
//   - PlannedQty / CompletedQty / ScrappedQty / ReworkQty >= 0
//
// Idempotent: every CREATE uses IF NOT EXISTS; seeds use ON CONFLICT DO NOTHING.
//
// Cross-refs:
//   - docs/research/luxury-cockpit-ux.md — locked design language
//   - memory project_mes_gap_analysis.md — 6-PR cascade where this is #5c
//   - Migrations/20260524_AddMastersPRA2.cs — template I'm mirroring

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524_AddRoutingWorkCenterProductionOperation")]
    public partial class AddRoutingWorkCenterProductionOperation : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ============================================================
            // 1) WorkCenters
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkCenters"" (
                    ""Id""                          serial       PRIMARY KEY,
                    ""CompanyId""                   integer      NOT NULL,
                    ""Code""                        varchar(50)  NOT NULL,
                    ""Name""                        varchar(200) NOT NULL,
                    ""Description""                 varchar(2000) NULL,
                    ""Type""                        integer      NOT NULL DEFAULT 0,
                    ""Status""                      integer      NOT NULL DEFAULT 0,
                    ""CapacityModel""               integer      NOT NULL DEFAULT 0,
                    ""CalendarId""                  integer      NULL,
                    ""EfficiencyPct""               numeric(6,2) NOT NULL DEFAULT 100,
                    ""UtilizationPct""              numeric(6,2) NOT NULL DEFAULT 100,
                    ""SimultaneousOperationsMax""   integer      NULL,
                    ""DefaultQueueTimeMins""        integer      NOT NULL DEFAULT 0,
                    ""DefaultMoveTimeMins""         integer      NOT NULL DEFAULT 0,
                    ""DefaultWaitTimeMins""         integer      NOT NULL DEFAULT 0,
                    ""StandardCostRatePerHour""     numeric(12,2) NOT NULL DEFAULT 0,
                    ""OverheadRatePerHour""         numeric(12,2) NOT NULL DEFAULT 0,
                    ""CurrencyCode""                varchar(3)   NOT NULL DEFAULT 'USD',
                    ""LocationId""                  integer      NULL,
                    ""OwningDepartmentId""          integer      NULL,
                    ""PreferredVendorId""           integer      NULL,
                    ""DefaultLeadTimeDays""         integer      NULL,
                    ""CreatedAt""                   timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""CreatedBy""                   varchar(100) NULL,
                    ""ModifiedAt""                  timestamp    NULL,
                    ""ModifiedBy""                  varchar(100) NULL,
                    ""IsActive""                    boolean      NOT NULL DEFAULT TRUE,
                    CONSTRAINT ""CK_WorkCenters_Type""          CHECK (""Type""          BETWEEN 0 AND 3),
                    CONSTRAINT ""CK_WorkCenters_Status""        CHECK (""Status""        BETWEEN 0 AND 3),
                    CONSTRAINT ""CK_WorkCenters_CapacityModel"" CHECK (""CapacityModel"" BETWEEN 0 AND 2),
                    CONSTRAINT ""CK_WorkCenters_Efficiency""    CHECK (""EfficiencyPct""  BETWEEN 0 AND 200),
                    CONSTRAINT ""CK_WorkCenters_Utilization""   CHECK (""UtilizationPct"" BETWEEN 0 AND 200)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkCenters_Company_Code"" ON ""WorkCenters""(""CompanyId"", ""Code"");
                CREATE INDEX IF NOT EXISTS ""IX_WorkCenters_Status_Type"" ON ""WorkCenters""(""Status"", ""Type"");
                CREATE INDEX IF NOT EXISTS ""IX_WorkCenters_Location"" ON ""WorkCenters""(""LocationId"") WHERE ""LocationId"" IS NOT NULL;
            ");

            // ============================================================
            // 2) WorkCenterAssetLinks (N:N WC <-> Asset, with effective range)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkCenterAssetLinks"" (
                    ""Id""             serial       PRIMARY KEY,
                    ""WorkCenterId""   integer      NOT NULL,
                    ""AssetId""        integer      NOT NULL,
                    ""IsPrimary""      boolean      NOT NULL DEFAULT FALSE,
                    ""EffectiveFrom""  timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""EffectiveTo""    timestamp    NULL,
                    ""CreatedAt""      timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""CreatedBy""      varchar(100) NULL,
                    CONSTRAINT ""FK_WorkCenterAssetLinks_WC"" FOREIGN KEY (""WorkCenterId"")
                        REFERENCES ""WorkCenters""(""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_WorkCenterAssetLinks_WC"" ON ""WorkCenterAssetLinks""(""WorkCenterId"");
                CREATE INDEX IF NOT EXISTS ""IX_WorkCenterAssetLinks_Asset"" ON ""WorkCenterAssetLinks""(""AssetId"");
                -- Partial unique: only one active primary link per WC
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_WorkCenterAssetLinks_Primary"" ON ""WorkCenterAssetLinks""(""WorkCenterId"") WHERE ""IsPrimary"" = TRUE AND ""EffectiveTo"" IS NULL;
            ");

            // ============================================================
            // 3) Routings (header, versioned)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Routings"" (
                    ""Id""                serial        PRIMARY KEY,
                    ""CompanyId""         integer       NOT NULL,
                    ""Code""              varchar(50)   NOT NULL,
                    ""RevisionNumber""    varchar(10)   NOT NULL DEFAULT 'A',
                    ""Name""              varchar(200)  NOT NULL,
                    ""Description""       varchar(2000) NULL,
                    ""ItemId""            integer       NOT NULL,
                    ""Type""              integer       NOT NULL DEFAULT 0,
                    ""Status""            integer       NOT NULL DEFAULT 0,
                    ""EffectiveFrom""     timestamp     NULL,
                    ""EffectiveTo""       timestamp     NULL,
                    ""ApprovedBy""        varchar(100)  NULL,
                    ""ApprovedAt""        timestamp     NULL,
                    ""LotBaseSize""       numeric(18,4) NOT NULL DEFAULT 1,
                    ""UnitOfMeasure""     varchar(10)   NULL,
                    ""IsDefault""         boolean       NOT NULL DEFAULT FALSE,
                    ""SourceRoutingId""   integer       NULL,
                    ""Notes""             varchar(4000) NULL,
                    ""CreatedAt""         timestamp     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""CreatedBy""         varchar(100)  NULL,
                    ""ModifiedAt""        timestamp     NULL,
                    ""ModifiedBy""        varchar(100)  NULL,
                    ""IsActive""          boolean       NOT NULL DEFAULT TRUE,
                    CONSTRAINT ""CK_Routings_Type""        CHECK (""Type""   BETWEEN 0 AND 3),
                    CONSTRAINT ""CK_Routings_Status""      CHECK (""Status"" BETWEEN 0 AND 4),
                    CONSTRAINT ""CK_Routings_LotBaseSize"" CHECK (""LotBaseSize"" > 0)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Routings_Company_Code_Rev"" ON ""Routings""(""CompanyId"", ""Code"", ""RevisionNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_Routings_Item_Status"" ON ""Routings""(""ItemId"", ""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_Routings_Default"" ON ""Routings""(""ItemId"") WHERE ""IsDefault"" = TRUE;
            ");

            // ============================================================
            // 4) RoutingOperations (child rows, ordered, 5-time decomp)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""RoutingOperations"" (
                    ""Id""                       serial         PRIMARY KEY,
                    ""RoutingId""                integer        NOT NULL,
                    ""SequenceNumber""           integer        NOT NULL DEFAULT 10,
                    ""WorkCenterId""             integer        NOT NULL,
                    ""OperationType""            integer        NOT NULL DEFAULT 1,
                    ""Description""              varchar(200)   NOT NULL,
                    ""SetupTimeMins""            numeric(10,2)  NOT NULL DEFAULT 0,
                    ""RunTimePerUnitMins""       numeric(10,4)  NOT NULL DEFAULT 0,
                    ""QueueTimeMins""            numeric(10,2)  NOT NULL DEFAULT 0,
                    ""MoveTimeMins""             numeric(10,2)  NOT NULL DEFAULT 0,
                    ""WaitTimeMins""             numeric(10,2)  NOT NULL DEFAULT 0,
                    ""YieldPct""                 numeric(5,2)   NOT NULL DEFAULT 100,
                    ""PredecessorOperationId""   integer        NULL,
                    ""IsParallel""               boolean        NOT NULL DEFAULT FALSE,
                    ""IsOptional""               boolean        NOT NULL DEFAULT FALSE,
                    ""Instructions""             varchar(8000)  NULL,
                    ""RequiredSkillCodes""       varchar(500)   NULL,
                    ""RequiredToolingIds""       varchar(500)   NULL,
                    ""CostRateOverridePerHour""  numeric(12,2)  NULL,
                    ""CreatedAt""                timestamp      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""CreatedBy""                varchar(100)   NULL,
                    ""ModifiedAt""               timestamp      NULL,
                    ""ModifiedBy""               varchar(100)   NULL,
                    CONSTRAINT ""FK_RoutingOps_Routing"" FOREIGN KEY (""RoutingId"")
                        REFERENCES ""Routings""(""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_RoutingOps_WC"" FOREIGN KEY (""WorkCenterId"")
                        REFERENCES ""WorkCenters""(""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""CK_RoutingOps_OpType""   CHECK (""OperationType""    BETWEEN 0 AND 7),
                    CONSTRAINT ""CK_RoutingOps_Sequence"" CHECK (""SequenceNumber""   > 0),
                    CONSTRAINT ""CK_RoutingOps_Setup""    CHECK (""SetupTimeMins""    >= 0),
                    CONSTRAINT ""CK_RoutingOps_Run""      CHECK (""RunTimePerUnitMins"" >= 0),
                    CONSTRAINT ""CK_RoutingOps_Queue""    CHECK (""QueueTimeMins""    >= 0),
                    CONSTRAINT ""CK_RoutingOps_Move""     CHECK (""MoveTimeMins""     >= 0),
                    CONSTRAINT ""CK_RoutingOps_Wait""     CHECK (""WaitTimeMins""     >= 0),
                    CONSTRAINT ""CK_RoutingOps_Yield""    CHECK (""YieldPct""         BETWEEN 0 AND 100)
                );
                CREATE INDEX IF NOT EXISTS ""IX_RoutingOps_Routing_Seq"" ON ""RoutingOperations""(""RoutingId"", ""SequenceNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_RoutingOps_WC"" ON ""RoutingOperations""(""WorkCenterId"");
            ");

            // ============================================================
            // 5) ProductionOperations (execution-time, the universal MES entity)
            // ============================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""ProductionOperations"" (
                    ""Id""                         serial         PRIMARY KEY,
                    ""ProductionOrderId""          integer        NOT NULL,
                    ""RoutingOperationId""         integer        NULL,
                    ""RoutingRevisionSnapshot""    varchar(10)    NULL,
                    ""SequenceNumber""             integer        NOT NULL DEFAULT 10,
                    ""WorkCenterId""               integer        NOT NULL,
                    ""AssetId""                    integer        NULL,
                    ""OperationType""              integer        NOT NULL DEFAULT 1,
                    ""Status""                     integer        NOT NULL DEFAULT 0,
                    ""Description""                varchar(200)   NOT NULL,
                    ""PlannedSetupMins""           numeric(10,2)  NOT NULL DEFAULT 0,
                    ""PlannedRunMins""             numeric(10,2)  NOT NULL DEFAULT 0,
                    ""PlannedQueueMins""           numeric(10,2)  NOT NULL DEFAULT 0,
                    ""PlannedMoveMins""            numeric(10,2)  NOT NULL DEFAULT 0,
                    ""PlannedWaitMins""            numeric(10,2)  NOT NULL DEFAULT 0,
                    ""ActualSetupMins""            numeric(10,2)  NOT NULL DEFAULT 0,
                    ""ActualRunMins""              numeric(10,2)  NOT NULL DEFAULT 0,
                    ""ActualDownMins""             numeric(10,2)  NOT NULL DEFAULT 0,
                    ""PlannedStart""               timestamp      NULL,
                    ""PlannedEnd""                 timestamp      NULL,
                    ""ActualStart""                timestamp      NULL,
                    ""ActualEnd""                  timestamp      NULL,
                    ""PlannedQty""                 numeric(18,4)  NOT NULL DEFAULT 0,
                    ""CompletedQty""               numeric(18,4)  NOT NULL DEFAULT 0,
                    ""ScrappedQty""                numeric(18,4)  NOT NULL DEFAULT 0,
                    ""ReworkQty""                  numeric(18,4)  NOT NULL DEFAULT 0,
                    ""OperatorUserIdsCsv""         varchar(500)   NULL,
                    ""Instructions""               varchar(8000)  NULL,
                    ""Notes""                      varchar(2000)  NULL,
                    ""SkipReason""                 varchar(200)   NULL,
                    ""CreatedAt""                  timestamp      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""CreatedBy""                  varchar(100)   NULL,
                    ""ModifiedAt""                 timestamp      NULL,
                    ""ModifiedBy""                 varchar(100)   NULL,
                    CONSTRAINT ""FK_ProdOps_Order""  FOREIGN KEY (""ProductionOrderId"")
                        REFERENCES ""ProductionOrders""(""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_ProdOps_RoutOp"" FOREIGN KEY (""RoutingOperationId"")
                        REFERENCES ""RoutingOperations""(""Id"") ON DELETE SET NULL,
                    CONSTRAINT ""FK_ProdOps_WC""     FOREIGN KEY (""WorkCenterId"")
                        REFERENCES ""WorkCenters""(""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""CK_ProdOps_OpType""   CHECK (""OperationType""  BETWEEN 0 AND 7),
                    CONSTRAINT ""CK_ProdOps_Status""   CHECK (""Status""         BETWEEN 0 AND 7),
                    CONSTRAINT ""CK_ProdOps_Sequence"" CHECK (""SequenceNumber"" > 0),
                    CONSTRAINT ""CK_ProdOps_PlannedQty""    CHECK (""PlannedQty""    >= 0),
                    CONSTRAINT ""CK_ProdOps_CompletedQty""  CHECK (""CompletedQty""  >= 0),
                    CONSTRAINT ""CK_ProdOps_ScrappedQty""   CHECK (""ScrappedQty""   >= 0),
                    CONSTRAINT ""CK_ProdOps_ReworkQty""     CHECK (""ReworkQty""     >= 0)
                );
                CREATE INDEX IF NOT EXISTS ""IX_ProdOps_Order_Seq"" ON ""ProductionOperations""(""ProductionOrderId"", ""SequenceNumber"");
                CREATE INDEX IF NOT EXISTS ""IX_ProdOps_WC_Status"" ON ""ProductionOperations""(""WorkCenterId"", ""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_ProdOps_Asset"" ON ""ProductionOperations""(""AssetId"") WHERE ""AssetId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ""IX_ProdOps_Status_PlannedStart"" ON ""ProductionOperations""(""Status"", ""PlannedStart"");
            ");

            // ============================================================
            // 6) Seed 8 demo WorkCenters for ABS Machining (CompanyId = 1)
            //    Gives the ABS Thursday demo authentic shop-floor texture.
            // ============================================================
            mb.Sql(@"
                INSERT INTO ""WorkCenters""
                    (""CompanyId"", ""Code"", ""Name"", ""Description"", ""Type"", ""Status"", ""CapacityModel"", ""StandardCostRatePerHour"", ""OverheadRatePerHour"", ""CurrencyCode"")
                VALUES
                    (1, 'CNC-1',     'Haas VF-2 CNC Mill #1',    '3-axis vertical machining center, 30in x 16in table',     0, 0, 0,  95.00, 28.00, 'USD'),
                    (1, 'CNC-2',     'Haas VF-2 CNC Mill #2',    '3-axis vertical machining center, twin to CNC-1',         0, 0, 0,  95.00, 28.00, 'USD'),
                    (1, 'LATHE-1',   'Mazak QT-200MY Turning',   'Multi-axis turning + live tool milling',                   0, 0, 0, 110.00, 30.00, 'USD'),
                    (1, 'MILL-MAN',  'Bridgeport Manual Mill',   'Manual knee mill for one-offs and prototypes',             0, 0, 0,  65.00, 18.00, 'USD'),
                    (1, 'DEBURR-1',  'Deburr / Edge-Break Bench','Manual hand finishing station, 2 operators',               2, 0, 1,  48.00, 12.00, 'USD'),
                    (1, 'WELD-1',    'TIG / MIG Weld Booth',     'Lincoln TIG 225 + Miller 252, shared booth',               0, 0, 0,  78.00, 22.00, 'USD'),
                    (1, 'QC-1',      'Inspection / CMM',         'Mitutoyo CMM + manual gage rack, ISO-9001 sample plans',   2, 0, 0,  72.00, 20.00, 'USD'),
                    (1, 'FINAL-1',   'Final Assy / Packout',     'Final QC + tag + pack, single shift',                      2, 0, 0,  52.00, 14.00, 'USD')
                ON CONFLICT (""CompanyId"", ""Code"") DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            mb.Sql(@"DROP TABLE IF EXISTS ""ProductionOperations"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""RoutingOperations"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""Routings"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""WorkCenterAssetLinks"" CASCADE;");
            mb.Sql(@"DROP TABLE IF EXISTS ""WorkCenters"" CASCADE;");
        }
    }
}
