using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // S2-4: explicit FK linkage from Asset back to its financial origin.
    // The existing PurchaseOrderNumber/InvoiceNumber denormalized strings
    // can't be reliably walked back to source rows; partner integrations
    // and audit reports need real FKs. Adds three nullable columns +
    // indexes + FK constraints. See docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.
    //
    // Schema-only and additive: nullable columns, no backfill. Existing
    // assets keep NULL; future Receiving/AP/CIP-capitalize paths will
    // stamp the FKs as part of S1-1, S1-4, and S1-5 respectively.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508130000_AddAssetOriginatingFks")]
    public partial class AddAssetOriginatingFks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Assets""
                ADD COLUMN IF NOT EXISTS ""OriginatingPurchaseOrderId"" integer NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Assets""
                ADD COLUMN IF NOT EXISTS ""OriginatingVendorInvoiceId"" integer NULL;
            ");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Assets""
                ADD COLUMN IF NOT EXISTS ""OriginatingCipProjectId"" integer NULL;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Assets_OriginatingPurchaseOrderId""
                ON ""Assets"" (""OriginatingPurchaseOrderId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Assets_OriginatingVendorInvoiceId""
                ON ""Assets"" (""OriginatingVendorInvoiceId"");
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Assets_OriginatingCipProjectId""
                ON ""Assets"" (""OriginatingCipProjectId"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Assets_PurchaseOrders_OriginatingPurchaseOrderId') THEN
                        ALTER TABLE ""Assets""
                        ADD CONSTRAINT ""FK_Assets_PurchaseOrders_OriginatingPurchaseOrderId""
                        FOREIGN KEY (""OriginatingPurchaseOrderId"")
                        REFERENCES ""PurchaseOrders"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Assets_VendorInvoices_OriginatingVendorInvoiceId') THEN
                        ALTER TABLE ""Assets""
                        ADD CONSTRAINT ""FK_Assets_VendorInvoices_OriginatingVendorInvoiceId""
                        FOREIGN KEY (""OriginatingVendorInvoiceId"")
                        REFERENCES ""VendorInvoices"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Assets_CipProjects_OriginatingCipProjectId') THEN
                        ALTER TABLE ""Assets""
                        ADD CONSTRAINT ""FK_Assets_CipProjects_OriginatingCipProjectId""
                        FOREIGN KEY (""OriginatingCipProjectId"")
                        REFERENCES ""CipProjects"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP CONSTRAINT IF EXISTS ""FK_Assets_CipProjects_OriginatingCipProjectId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP CONSTRAINT IF EXISTS ""FK_Assets_VendorInvoices_OriginatingVendorInvoiceId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP CONSTRAINT IF EXISTS ""FK_Assets_PurchaseOrders_OriginatingPurchaseOrderId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Assets_OriginatingCipProjectId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Assets_OriginatingVendorInvoiceId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Assets_OriginatingPurchaseOrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP COLUMN IF EXISTS ""OriginatingCipProjectId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP COLUMN IF EXISTS ""OriginatingVendorInvoiceId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Assets"" DROP COLUMN IF EXISTS ""OriginatingPurchaseOrderId"";");
        }
    }
}
