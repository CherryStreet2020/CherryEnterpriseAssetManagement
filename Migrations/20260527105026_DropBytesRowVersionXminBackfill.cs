using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class DropBytesRowVersionXminBackfill : Migration
    {
        // PR-XminBackfill (2026-05-27).
        //
        // Three entities — CostLayer (PR #360), ItemSourcingRule (PR #361),
        // CustomerItemXref (PR #362) — shipped with `IsRowVersion()` on a
        // `bytea NOT NULL` column. EF excluded the column from INSERT statements
        // (expecting the DB to auto-populate), but Postgres does NOT auto-populate
        // bytea → every INSERT would throw 23502 NOT NULL violation.
        //
        // The bug was dormant because the existing admin probes for these entities
        // were read-or-update-only (no INSERT path exercised). Surfaced on
        // PR #364 (Sprint 14.1 PR-1) which was the first post-B6 probe to run an
        // INSERT; hotfixed by PR #365 using the xmin pattern.
        //
        // Fix: drop the bogus `bytea RowVersion` column from all 3 tables.
        // The C# property now maps to Postgres' built-in `xmin` system column
        // via MapXminRowVersion (Data/XminRowVersionExtensions.cs). The `xmin`
        // column exists on every Postgres row by default — no DDL needed, and
        // it auto-populates on every INSERT/UPDATE.
        //
        // Pattern match: 20260526235021_DropPoSnapshotBytesRowVersionFs141Pr1P1.cs
        // (the PR #365 hotfix for ProductionMaterialStructures).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CostLayers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ItemSourcingRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CustomerItemXrefs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CostLayers",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ItemSourcingRules",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CustomerItemXrefs",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
