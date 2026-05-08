using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // S1-8 / S2-8: maps RowVersion (byte[]) to PostgreSQL's built-in `xmin`
    // system column on five state-machine entities:
    //   - PurchaseOrder
    //   - MaintenanceEvent
    //   - GoodsReceipt
    //   - VendorInvoice
    //   - CipProject
    //
    // Same pattern as Asset (PR #2026-05-02). xmin is a system column on
    // every PG row — no DDL is required, this migration is a no-op marker
    // for snapshot alignment only.
    //
    // EF wiring lives in Data/AppDbContext.cs via the MapXminRowVersion<T>()
    // extension method (Data/XminRowVersionExtensions.cs). Two concurrent
    // updates to any of these rows now fail with DbUpdateConcurrencyException
    // — the read-and-then-write pattern that was previously vulnerable to
    // silent overwrite is now protected at the EF layer.
    public partial class AddXminRowVersionToStateMachineEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
