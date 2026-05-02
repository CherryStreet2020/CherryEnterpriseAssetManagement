using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // No-op: maps Asset.RowVersion to PG's built-in `xmin` system column. No DDL
    // is required (and `AddColumn xmin` would fail — name conflicts with a system column).
    public partial class AddAssetRowVersionXmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
