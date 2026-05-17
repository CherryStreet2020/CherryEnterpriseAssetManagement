using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // ADR-012 v0.2 / PR #119.3 — Per-classification state machine config.
    //
    // Three tables:
    //   - WorkOrderStatusProfile (one row per classification — header)
    //   - WorkOrderStatusLabel   (Classification × StatusCode → label/color)
    //   - WorkOrderStatusTransition (allowed FROM → TO edges per classification)
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517_AddWorkOrderStatusEngine")]
    public partial class AddWorkOrderStatusEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WorkOrderStatusProfile — one row per classification, PK is
            // the Classification column itself (no surrogate Id).
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkOrderStatusProfile"" (
                    ""Classification""         smallint    NOT NULL PRIMARY KEY,
                    ""Name""                   varchar(80) NOT NULL,
                    ""StartStatusCode""        smallint    NOT NULL DEFAULT 0,
                    ""CanReopenFromTerminal""  boolean     NOT NULL DEFAULT false,
                    ""Description""            varchar(500) NULL,
                    ""CreatedAt""              timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""              timestamptz NOT NULL DEFAULT now()
                );
            ");

            // WorkOrderStatusLabel — one row per (Classification, StatusCode).
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkOrderStatusLabel"" (
                    ""Id""              SERIAL PRIMARY KEY,
                    ""Classification""  smallint    NOT NULL,
                    ""StatusCode""      smallint    NOT NULL,
                    ""StatusKey""       varchar(40) NOT NULL,
                    ""DisplayLabel""    varchar(80) NOT NULL,
                    ""DisplayColor""    varchar(20) NOT NULL DEFAULT 'gray',
                    ""IsTerminal""      boolean     NOT NULL DEFAULT false,
                    ""IsHolding""       boolean     NOT NULL DEFAULT false,
                    ""DisplayOrder""    integer     NOT NULL DEFAULT 100,
                    ""CreatedAt""       timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""       timestamptz NOT NULL DEFAULT now()
                );
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS
                    ""IX_WorkOrderStatusLabel_Classification_StatusCode""
                ON ""WorkOrderStatusLabel"" (""Classification"", ""StatusCode"");
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS
                    ""IX_WorkOrderStatusLabel_Classification_StatusKey""
                ON ""WorkOrderStatusLabel"" (""Classification"", ""StatusKey"");
            ");

            // WorkOrderStatusTransition — allowed FROM → TO edges.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""WorkOrderStatusTransition"" (
                    ""Id""                      SERIAL PRIMARY KEY,
                    ""Classification""          smallint    NOT NULL,
                    ""FromStatusCode""          smallint    NOT NULL,
                    ""ToStatusCode""            smallint    NOT NULL,
                    ""RequiredApprovalStage""   varchar(40) NULL,
                    ""GuardServiceName""        varchar(80) NULL,
                    ""IsBackTransition""        boolean     NOT NULL DEFAULT false,
                    ""ActionLabel""             varchar(80) NULL,
                    ""DisplayOrder""            integer     NOT NULL DEFAULT 100,
                    ""CreatedAt""               timestamptz NOT NULL DEFAULT now(),
                    ""UpdatedAt""               timestamptz NOT NULL DEFAULT now()
                );
            ");
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS
                    ""IX_WorkOrderStatusTransition_Cls_From_To""
                ON ""WorkOrderStatusTransition"" (""Classification"", ""FromStatusCode"", ""ToStatusCode"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS
                    ""IX_WorkOrderStatusTransition_Cls_From""
                ON ""WorkOrderStatusTransition"" (""Classification"", ""FromStatusCode"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderStatusTransition_Cls_From"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderStatusTransition_Cls_From_To"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""WorkOrderStatusTransition"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderStatusLabel_Classification_StatusKey"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_WorkOrderStatusLabel_Classification_StatusCode"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""WorkOrderStatusLabel"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""WorkOrderStatusProfile"";");
        }
    }
}
