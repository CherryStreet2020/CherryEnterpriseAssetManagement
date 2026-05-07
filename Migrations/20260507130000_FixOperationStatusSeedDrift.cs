using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Fixes drift between the OperationStatus C# enum
    // (Models/WorkOrderOperation.cs) and the seed-driven LookupValue rows
    // for LookupType.Key = 'OperationStatus'.
    //
    // The C# enum is the source of truth. The drift was introduced when
    // OperationStatus.Ready was added to the enum without updating the
    // seed JSON, which shifted the meaning of every code from 1 onward.
    //
    // Pre-migration (broken):
    //     0=Pending, 1="In Progress", 2=Completed, 3="On Hold", 4=Cancelled, (no 5)
    //
    // Post-migration (correct, matches enum):
    //     0=Pending, 1=Ready, 2="In Progress", 3="On Hold", 4=Completed, 5=Cancelled
    //
    // The LookupValue row IDs do not change. Existing FKs (rare today —
    // ScheduleBoard never wrote StatusLookupValueId — but possible) remain
    // valid; only the Name/SortOrder are corrected. Code "5" is inserted
    // for the previously-missing Cancelled value.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260507130000_FixOperationStatusSeedDrift")]
    public partial class FixOperationStatusSeedDrift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename mis-labelled rows. Idempotent — safe to re-run.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Ready',
                    ""SortOrder"" = 2,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '1'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'In Progress',
                    ""SortOrder"" = 3,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '2'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Completed',
                    ""SortOrder"" = 5,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '4'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            // Sanity: keep Pending/On Hold sortOrder consistent with the
            // new ordering (1 / 4 respectively). Idempotent.
            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""SortOrder"" = 1,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '0'
                  AND ""SortOrder"" <> 1
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""SortOrder"" = 4,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '3'
                  AND ""SortOrder"" <> 4
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            // Insert the previously-missing Cancelled row at code '5' for
            // every OperationStatus LookupType (one per tenant/company
            // scope where the type exists). Skip if already present.
            migrationBuilder.Sql(@"
                INSERT INTO ""LookupValues""
                    (""LookupTypeId"", ""Code"", ""Name"", ""SortOrder"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT lt.""Id"", '5', 'Cancelled', 6, true,
                       (now() AT TIME ZONE 'UTC'), (now() AT TIME ZONE 'UTC')
                FROM ""LookupTypes"" lt
                WHERE lt.""Key"" = 'OperationStatus'
                  AND NOT EXISTS (
                      SELECT 1 FROM ""LookupValues"" lv
                      WHERE lv.""LookupTypeId"" = lt.""Id"" AND lv.""Code"" = '5'
                  );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: drop the inserted Cancelled row, restore old
            // (drifted) names so this migration can be safely rolled back.
            migrationBuilder.Sql(@"
                DELETE FROM ""LookupValues""
                WHERE ""Code"" = '5'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Cancelled',
                    ""SortOrder"" = 5,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '4'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'Completed',
                    ""SortOrder"" = 3,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '2'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");

            migrationBuilder.Sql(@"
                UPDATE ""LookupValues""
                SET ""Name"" = 'In Progress',
                    ""SortOrder"" = 2,
                    ""UpdatedAt"" = (now() AT TIME ZONE 'UTC')
                WHERE ""Code"" = '1'
                  AND ""LookupTypeId"" IN (
                      SELECT ""Id"" FROM ""LookupTypes"" WHERE ""Key"" = 'OperationStatus'
                  );
            ");
        }
    }
}
