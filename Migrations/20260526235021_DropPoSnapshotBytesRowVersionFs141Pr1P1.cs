using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class DropPoSnapshotBytesRowVersionFs141Pr1P1 : Migration
    {
        // PR-14.1-1.1 HOTFIX (2026-05-26).
        //
        // PR #364 (Sprint 14.1 PR-1) created a real `bytea RowVersion NOT NULL`
        // column on ProductionMaterialStructures with EF's `IsRowVersion()`
        // concurrency annotation. EF interpreted this as "value-generated
        // on add" and EXCLUDED the column from INSERT statements, expecting
        // the DB to populate it. Postgres does NOT auto-populate `bytea`
        // columns, so every INSERT threw `23502 NOT NULL violation in
        // RowVersion`. The probe page hit this on the first Capture click
        // during Lock 16 E2E.
        //
        // Fix: drop the bogus `bytea` column. The C# property now maps to
        // Postgres' built-in `xmin` system column via the project convention
        // `MapXminRowVersion` (see Data/XminRowVersionExtensions.cs). The
        // `xmin` column exists on every Postgres row by default — no DDL
        // needed, and it auto-populates on every INSERT/UPDATE.
        //
        // The snapshot/Designer were already regenerated to reflect the
        // xmin mapping (no RowVersion property in the snapshot column list);
        // this Up() handles the DDL gap so the actual DB matches.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductionMaterialStructures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductionMaterialStructures",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
