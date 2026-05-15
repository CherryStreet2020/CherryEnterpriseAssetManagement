using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // 2026-05-15: DEF-012 — schema drift on CipCapitalizations + CipCapitalizationCosts.
    //
    // Root cause: neither table was ever created by a migration. The live PG
    // schema was produced by an early Program.cs:396 EnsureCreated() call
    // against a since-evolved model snapshot. When the CipCapitalization
    // entity was later extended (CapitalizedByUserId added, TotalAmount
    // renamed to TotalCapitalized, Notes + CreatedAt removed, AssetId /
    // JournalEntryId indexes + FKs added) and CipCapitalizationCost was
    // tightened (Amount column dropped, CipCostId FK behavior switched to
    // Restrict), no migration was authored — Database.MigrateAsync had
    // nothing to apply, and EnsureCreated does not patch existing tables.
    //
    // Result on the live DB:
    //   CipCapitalizations had  CapitalizedBy (varchar 200), TotalAmount,
    //                          Notes, CreatedAt; missing CapitalizedByUserId,
    //                          TotalCapitalized, FK→Assets, FK→JournalEntries,
    //                          IX_AssetId, IX_JournalEntryId.
    //   CipCapitalizationCosts had an extra Amount column and an erroneous
    //                          CipCostId FK ON DELETE CASCADE (model = Restrict).
    //
    // Discovered 2026-05-15 mid-Step-5 of the marquee smoke when CIP
    // capitalization threw PostgresException 42703: column
    // "CapitalizedByUserId" of relation "CipCapitalizations" does not exist.
    //
    // The body of this migration is idempotent: it brings the live DB into
    // exact alignment with the model snapshot whether the table is in the
    // legacy "EnsureCreated" shape, the target shape, or partway through. Safe
    // to apply on any environment.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260515160000_AlignCipCapitalizationsSchema")]
    public partial class AlignCipCapitalizationsSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ============================================================================
-- CipCapitalizations
-- ============================================================================

-- 1. CapitalizedBy (varchar 200) → CapitalizedByUserId (varchar 100).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizations'
                 AND column_name = 'CapitalizedBy')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public'
                         AND table_name = 'CipCapitalizations'
                         AND column_name = 'CapitalizedByUserId')
    THEN
        ALTER TABLE ""CipCapitalizations""
            RENAME COLUMN ""CapitalizedBy"" TO ""CapitalizedByUserId"";
        UPDATE ""CipCapitalizations""
            SET ""CapitalizedByUserId"" = LEFT(""CapitalizedByUserId"", 100)
            WHERE LENGTH(""CapitalizedByUserId"") > 100;
        ALTER TABLE ""CipCapitalizations""
            ALTER COLUMN ""CapitalizedByUserId"" TYPE character varying(100);
    END IF;
END$$;

ALTER TABLE ""CipCapitalizations""
    ADD COLUMN IF NOT EXISTS ""CapitalizedByUserId"" character varying(100);

-- 2. TotalAmount (numeric) → TotalCapitalized (numeric(18,2)).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizations'
                 AND column_name = 'TotalAmount')
       AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public'
                         AND table_name = 'CipCapitalizations'
                         AND column_name = 'TotalCapitalized')
    THEN
        ALTER TABLE ""CipCapitalizations""
            RENAME COLUMN ""TotalAmount"" TO ""TotalCapitalized"";
        ALTER TABLE ""CipCapitalizations""
            ALTER COLUMN ""TotalCapitalized"" TYPE numeric(18,2);
        ALTER TABLE ""CipCapitalizations""
            ALTER COLUMN ""TotalCapitalized"" DROP DEFAULT;
        ALTER TABLE ""CipCapitalizations""
            ALTER COLUMN ""TotalCapitalized"" SET NOT NULL;
    END IF;
END$$;

ALTER TABLE ""CipCapitalizations""
    ADD COLUMN IF NOT EXISTS ""TotalCapitalized"" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE ""CipCapitalizations""
    ALTER COLUMN ""TotalCapitalized"" DROP DEFAULT;

-- 3. Drop columns the model doesn't know about. EF would never write to them;
--    leaving them around invites future seed-from-prod fixtures to break.
ALTER TABLE ""CipCapitalizations"" DROP COLUMN IF EXISTS ""Notes"";
ALTER TABLE ""CipCapitalizations"" DROP COLUMN IF EXISTS ""CreatedAt"";

-- 4. CapitalizedAt: drop now() default. The model has no [DefaultValueSql];
--    CipCapitalizationService initializes the value in code.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizations'
                 AND column_name = 'CapitalizedAt'
                 AND column_default IS NOT NULL) THEN
        ALTER TABLE ""CipCapitalizations"" ALTER COLUMN ""CapitalizedAt"" DROP DEFAULT;
    END IF;
END$$;

-- 5. Indexes the model expects (the legacy table only had pk + CipProjectId).
CREATE INDEX IF NOT EXISTS ""IX_CipCapitalizations_AssetId""
    ON ""CipCapitalizations"" (""AssetId"");
CREATE INDEX IF NOT EXISTS ""IX_CipCapitalizations_JournalEntryId""
    ON ""CipCapitalizations"" (""JournalEntryId"");

-- 6. FKs the model expects: Assets (Restrict), JournalEntries (SetNull).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
                   WHERE table_schema = 'public'
                     AND table_name = 'CipCapitalizations'
                     AND constraint_name = 'FK_CipCapitalizations_Assets_AssetId') THEN
        ALTER TABLE ""CipCapitalizations""
            ADD CONSTRAINT ""FK_CipCapitalizations_Assets_AssetId""
            FOREIGN KEY (""AssetId"") REFERENCES ""Assets"" (""Id"") ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
                   WHERE table_schema = 'public'
                     AND table_name = 'CipCapitalizations'
                     AND constraint_name = 'FK_CipCapitalizations_JournalEntries_JournalEntryId') THEN
        ALTER TABLE ""CipCapitalizations""
            ADD CONSTRAINT ""FK_CipCapitalizations_JournalEntries_JournalEntryId""
            FOREIGN KEY (""JournalEntryId"") REFERENCES ""JournalEntries"" (""Id"") ON DELETE SET NULL;
    END IF;
END$$;

-- 7. Rename the legacy CipProjectId FK to the EF naming convention so future
--    snapshot diffs don't churn.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizations'
                 AND constraint_name = 'CipCapitalizations_CipProjectId_fkey')
       AND NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
                       WHERE table_schema = 'public'
                         AND table_name = 'CipCapitalizations'
                         AND constraint_name = 'FK_CipCapitalizations_CipProjects_CipProjectId')
    THEN
        ALTER TABLE ""CipCapitalizations""
            RENAME CONSTRAINT ""CipCapitalizations_CipProjectId_fkey""
            TO ""FK_CipCapitalizations_CipProjects_CipProjectId"";
    END IF;
END$$;

-- ============================================================================
-- CipCapitalizationCosts
-- ============================================================================

-- 1. Drop the unmodelled Amount column. EF never writes it; preserving it
--    risks silent staleness when capitalize re-runs.
ALTER TABLE ""CipCapitalizationCosts"" DROP COLUMN IF EXISTS ""Amount"";

-- 2. CipCostId FK: legacy was ON DELETE CASCADE; model is Restrict. We do not
--    want a CipCost delete to cascade-drop the cost-mapping rows that prove a
--    historical capitalization touched that cost — those rows are part of the
--    audit trail.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizationCosts'
                 AND constraint_name = 'CipCapitalizationCosts_CipCostId_fkey') THEN
        ALTER TABLE ""CipCapitalizationCosts""
            DROP CONSTRAINT ""CipCapitalizationCosts_CipCostId_fkey"";
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
                   WHERE table_schema = 'public'
                     AND table_name = 'CipCapitalizationCosts'
                     AND constraint_name = 'FK_CipCapitalizationCosts_CipCosts_CipCostId') THEN
        ALTER TABLE ""CipCapitalizationCosts""
            ADD CONSTRAINT ""FK_CipCapitalizationCosts_CipCosts_CipCostId""
            FOREIGN KEY (""CipCostId"") REFERENCES ""CipCosts"" (""Id"") ON DELETE RESTRICT;
    END IF;
END$$;

-- 3. Rename the legacy CipCapitalizationId FK to the EF naming convention.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.table_constraints
               WHERE table_schema = 'public'
                 AND table_name = 'CipCapitalizationCosts'
                 AND constraint_name = 'CipCapitalizationCosts_CipCapitalizationId_fkey')
       AND NOT EXISTS (SELECT 1 FROM information_schema.table_constraints
                       WHERE table_schema = 'public'
                         AND table_name = 'CipCapitalizationCosts'
                         AND constraint_name = 'FK_CipCapitalizationCosts_CipCapitalizations_CipCapitalizationId')
    THEN
        ALTER TABLE ""CipCapitalizationCosts""
            RENAME CONSTRAINT ""CipCapitalizationCosts_CipCapitalizationId_fkey""
            TO ""FK_CipCapitalizationCosts_CipCapitalizations_CipCapitalizationId"";
    END IF;
END$$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally not reversible. Reverting would re-create a
            // schema EF Core can no longer read (CapitalizedBy + TotalAmount
            // columns the model has no properties for). If you genuinely
            // need to roll back, drop the tables and restore from backup.
        }
    }
}
