using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <summary>
    /// Wires the Asset.RowVersion CLR property to PostgreSQL's system column `xmin`
    /// for use as an EF concurrency token. `xmin` is a built-in system column on
    /// every PostgreSQL row, so this migration is intentionally a no-op at the
    /// schema level — the EF model snapshot is the only thing that changes.
    ///
    /// EF's default scaffold for this property emits an AddColumn("xmin", "xid")
    /// statement, which fails on PostgreSQL with: column name "xmin" conflicts
    /// with a system column name. We replace it with empty Up/Down bodies so the
    /// migration applies cleanly on fresh and existing databases.
    /// </summary>
    public partial class AddAssetRowVersionXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: `xmin` is a PostgreSQL system column, no schema change required.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: cannot drop a system column.
        }
    }
}
