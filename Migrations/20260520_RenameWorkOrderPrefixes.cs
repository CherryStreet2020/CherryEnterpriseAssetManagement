using Microsoft.EntityFrameworkCore.Migrations;

namespace Abs.FixedAssets.Migrations;

// ADR-025 sibling rename (2026-05-20, Dean's call). Customer-aligned
// record-number prefixes that line up with the OrderTypeLabels.ShortCode
// (and the user-visible labels from PR #266):
//
//   Classification=Maintenance (0) → "MO-"  + "Maintenance Order"
//   Classification=Quality     (2) → "QO-"  + "Quality Order"
//   Classification=Engineering (3) → "EO-"  + "Engineering Order"
//   Classification=HSE         (4) → "HSE-" + "HSE Order"
//   Classification=CIP         (5) → "CIP-" + "CIP Order"
//
// Two parts to the migration:
//   1. UPDATE the NumberSequence config rows so future records emit the
//      new prefix. The lookup hits any tenant override first, then the
//      global (NULL-tenant) row. We update both if they exist.
//   2. Backfill existing WorkOrders.WorkOrderNumber via REGEXP_REPLACE,
//      swapping the "WO-" head while preserving the year+counter portion
//      (so "WO-202601-0240" → "MO-202601-0240" — easy to cross-reference
//      against any old screenshots / notes if needed).
//
// Records with non-WO prefixes already (e.g., "FIX-S1-004-ELEC-OTH" seed
// data) are guarded by the LIKE 'WO-%' clause and left untouched.
//
// Per feedback_ef_migration_attribute_required: apply via psql on Replit
// + INSERT INTO __EFMigrationsHistory until the AppDbContextModelSnapshot
// drift fix (Priority 1.6075) lands. Path C from the gotcha memory.
[Migration("20260520_RenameWorkOrderPrefixes")]
public partial class RenameWorkOrderPrefixes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. NumberSequence config rows. We accept either "WO" (the
        //    current production override) or the prior SAP-style default
        //    (PM/NCR/ECO/INC/AFE) as the "from" value — defensive against
        //    any tenant that seeded a custom row matching either pattern.
        //    Customers with explicit per-tenant overrides (anything OTHER
        //    than these defaults) are left untouched.
        migrationBuilder.Sql(@"
            UPDATE ""NumberSequence""
               SET ""Prefix"" = 'MO',  ""UpdatedAt"" = now()
             WHERE ""Classification"" = 0
               AND ""Prefix"" IN ('WO', 'PM');

            UPDATE ""NumberSequence""
               SET ""Prefix"" = 'QO',  ""UpdatedAt"" = now()
             WHERE ""Classification"" = 2
               AND ""Prefix"" IN ('WO', 'NCR');

            UPDATE ""NumberSequence""
               SET ""Prefix"" = 'EO',  ""UpdatedAt"" = now()
             WHERE ""Classification"" = 3
               AND ""Prefix"" IN ('WO', 'ECO');

            UPDATE ""NumberSequence""
               SET ""Prefix"" = 'HSE', ""UpdatedAt"" = now()
             WHERE ""Classification"" = 4
               AND ""Prefix"" IN ('WO', 'INC');

            UPDATE ""NumberSequence""
               SET ""Prefix"" = 'CIP', ""UpdatedAt"" = now()
             WHERE ""Classification"" = 5
               AND ""Prefix"" IN ('WO', 'AFE');
        ");

        // 2. Backfill existing WorkOrder records — preserve the year +
        //    counter portion, swap only the prefix segment. The LIKE
        //    'WO-%' guard prevents touching records with explicit
        //    non-WO numbers (seed data, hand-assigned overrides).
        migrationBuilder.Sql(@"
            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^WO-', 'MO-')
             WHERE ""Classification"" = 0
               AND ""WorkOrderNumber"" LIKE 'WO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^WO-', 'QO-')
             WHERE ""Classification"" = 2
               AND ""WorkOrderNumber"" LIKE 'WO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^WO-', 'EO-')
             WHERE ""Classification"" = 3
               AND ""WorkOrderNumber"" LIKE 'WO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^WO-', 'HSE-')
             WHERE ""Classification"" = 4
               AND ""WorkOrderNumber"" LIKE 'WO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^WO-', 'CIP-')
             WHERE ""Classification"" = 5
               AND ""WorkOrderNumber"" LIKE 'WO-%';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Inverse: swap the new per-classification prefix back to "WO-"
        // on both the NumberSequence config and the existing WorkOrder
        // records. Same LIKE guard pattern.
        migrationBuilder.Sql(@"
            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^MO-',  'WO-')
             WHERE ""Classification"" = 0 AND ""WorkOrderNumber"" LIKE 'MO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^QO-',  'WO-')
             WHERE ""Classification"" = 2 AND ""WorkOrderNumber"" LIKE 'QO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^EO-',  'WO-')
             WHERE ""Classification"" = 3 AND ""WorkOrderNumber"" LIKE 'EO-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^HSE-', 'WO-')
             WHERE ""Classification"" = 4 AND ""WorkOrderNumber"" LIKE 'HSE-%';

            UPDATE ""WorkOrders""
               SET ""WorkOrderNumber"" = REGEXP_REPLACE(""WorkOrderNumber"", '^CIP-', 'WO-')
             WHERE ""Classification"" = 5 AND ""WorkOrderNumber"" LIKE 'CIP-%';

            UPDATE ""NumberSequence"" SET ""Prefix"" = 'WO', ""UpdatedAt"" = now()
             WHERE ""Classification"" IN (0, 2, 3, 4, 5)
               AND ""Prefix"" IN ('MO', 'QO', 'EO', 'HSE', 'CIP');
        ");
    }
}
