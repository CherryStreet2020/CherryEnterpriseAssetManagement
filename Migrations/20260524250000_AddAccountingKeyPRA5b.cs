// =============================================================================
// Sprint 13.5 PRA-5b — AccountingKey (COA segment-key materialization).
// Master Files Baseline cascade ship #10 of 10 — closes the segment-key gap
// from docs/research/master-files-baseline-2026-05-24.md §4.
//
// SHAPE
// -----
// ONE new table:
//   AccountingKeys   (CompanyId NOT NULL + 7 segment FKs + sha256 hash)
//
// ONE column added to populated table (Lock 5 applies — pre-apply via psql):
//   JournalLines.AccountingKeyId  integer NULL REFERENCES AccountingKeys(Id)
//   ON DELETE RESTRICT
//
// BACKFILL
// --------
// For every existing JournalLine where (a) the JournalEntry.BookId resolves
// to a Book.CompanyId AND (b) the legacy varchar(50) Account string matches
// a GlAccount.AccountNumber in that company, an AccountingKey row is
// minted (8-segment with NULLs on segments not derivable from history) and
// the line's AccountingKeyId is set. Orphan lines (no Book / no
// Company / no matching account) stay NULL and continue to read via the
// legacy Account string — DEF-008 fallback. No data loss, no destructive
// SQL, all idempotent.
//
// NO SEEDS
// --------
// AccountingKey rows are operational data minted on-demand by
// IGlAccountResolver.ResolveAccountingKeyAsync at JE-post time. No system
// templates — different from the other PRA cascades. The migration body is
// pure schema + backfill.
//
// IDEMPOTENT
// ----------
// CREATE TABLE IF NOT EXISTS, ADD COLUMN guarded by a DO block (Postgres
// has no IF NOT EXISTS on ALTER TABLE ADD COLUMN at the migration level —
// the DO block is the portable guard). INSERT WHERE NOT EXISTS for
// backfill. UPDATE skips already-stamped rows. Migration can run twice
// without drift.
//
// AUTHORITY
//   - docs/research/master-files-baseline-2026-05-24.md §4
//   - docs/adr/ADR-003-central-gl-account-resolver.md (cascade order)
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - memory: feedback_replit_autodiff_destructive_on_populated_tables.md (Lock 5)
//   - memory: feedback_verify_migration_via_psql_not_agent.md (Lock 8)
//
// FORWARD-LOOKING (NOT in this migration)
//   - JournalLine.SourceModule / SourceDocumentId / SourceLineId columns.
//     Promised by IPostingService ADR-025 D2 but never landed. Separate ship.
//   - AccountPostingProfile wiring: matrix table from PRA-7 exists but
//     inventory-movement services don't consume it yet. Sprint 13 Wave 5.
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524250000_AddAccountingKeyPRA5b")]
    public partial class AddAccountingKeyPRA5b : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // 0) Ensure pgcrypto extension (sha256 via digest()).
            //    The backfill computes AccountingKeyHash directly in SQL so
            //    we don't need a round-trip to the C# resolver. pgcrypto is
            //    standard Postgres contrib; Replit Neon ships with it.
            // ================================================================
            mb.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // ================================================================
            // 1) AccountingKeys table — 8 segments + hash + audit.
            //
            //    Vertical CHECK enforces the 0..31 range Models/Company.cs
            //    locked at PRA-1 (17 values seeded, 14 reserved).
            //
            //    Hash is varchar(64) = sha256 hex. Canonical string form
            //    (computed by both the SQL backfill below and the C#
            //    resolver service so the two paths produce identical hashes):
            //      "{CompanyId}|{SiteId|''}|{AccountId}|{CostCenterId|''}
            //       |{DepartmentId|''}|{ProjectId|''}|{InterCoPartnerCompanyId|''}
            //       |{(short)IndustryVertical|''}"
            //    NULL segments serialize as empty string (NOT the literal
            //    "NULL" or "0") to keep the canonical form unambiguous.
            //
            //    Partial UNIQUE on (CompanyId, AccountingKeyHash) is the
            //    find-or-insert lookup the resolver uses on every JE post.
            // ================================================================
            mb.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AccountingKeys"" (
                    ""Id""                          serial          PRIMARY KEY,
                    ""CompanyId""                   integer         NOT NULL REFERENCES ""Companies""(""Id"") ON DELETE RESTRICT,
                    ""SiteId""                      integer         NULL     REFERENCES ""Locations""(""Id"")  ON DELETE SET NULL,
                    ""AccountId""                   integer         NOT NULL REFERENCES ""GlAccounts""(""Id"") ON DELETE RESTRICT,
                    ""CostCenterId""                integer         NULL     REFERENCES ""CostCenters""(""Id"") ON DELETE SET NULL,
                    ""DepartmentId""                integer         NULL     REFERENCES ""Departments""(""Id"") ON DELETE SET NULL,
                    ""ProjectId""                   integer         NULL,
                    ""InterCoPartnerCompanyId""     integer         NULL     REFERENCES ""Companies""(""Id"") ON DELETE SET NULL,
                    ""IndustryVertical""            smallint        NULL,
                    ""AccountingKeyHash""           varchar(64)     NOT NULL,
                    ""AccountingKeyString""         varchar(256)    NULL,
                    ""IsActive""                    boolean         NOT NULL DEFAULT TRUE,
                    ""CreatedAt""                   timestamptz     NOT NULL DEFAULT NOW(),
                    ""CreatedBy""                   varchar(100)    NULL,
                    ""ModifiedAt""                  timestamptz     NULL,
                    ""ModifiedBy""                  varchar(100)    NULL,
                    CONSTRAINT ck_accounting_keys_vertical_range
                        CHECK (""IndustryVertical"" IS NULL OR (""IndustryVertical"" BETWEEN 0 AND 31)),
                    CONSTRAINT ck_accounting_keys_hash_length
                        CHECK (length(""AccountingKeyHash"") = 64),
                    CONSTRAINT ck_accounting_keys_interco_not_self
                        CHECK (""InterCoPartnerCompanyId"" IS NULL OR ""InterCoPartnerCompanyId"" <> ""CompanyId"")
                );

                -- The find-or-insert lookup index. Partial on CompanyId-non-null
                -- (which is the table constraint anyway — partial form is
                -- defensive against future schema drift).
                CREATE UNIQUE INDEX IF NOT EXISTS ix_accounting_keys_company_hash
                    ON ""AccountingKeys"" (""CompanyId"", ""AccountingKeyHash"")
                    WHERE ""CompanyId"" IS NOT NULL;

                -- Browse / reporting indexes per BIC entity checklist rule 2.
                CREATE INDEX IF NOT EXISTS ix_accounting_keys_company_account
                    ON ""AccountingKeys"" (""CompanyId"", ""AccountId"");
                CREATE INDEX IF NOT EXISTS ix_accounting_keys_company_project
                    ON ""AccountingKeys"" (""CompanyId"", ""ProjectId"")
                    WHERE ""ProjectId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_accounting_keys_company_department
                    ON ""AccountingKeys"" (""CompanyId"", ""DepartmentId"")
                    WHERE ""DepartmentId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_accounting_keys_company_costcenter
                    ON ""AccountingKeys"" (""CompanyId"", ""CostCenterId"")
                    WHERE ""CostCenterId"" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS ix_accounting_keys_company_site
                    ON ""AccountingKeys"" (""CompanyId"", ""SiteId"")
                    WHERE ""SiteId"" IS NOT NULL;
            ");

            // ================================================================
            // 2) JournalLines.AccountingKeyId — nullable FK.
            //
            //    DO block guards the column add for migration idempotency
            //    (Postgres has no IF NOT EXISTS on ALTER TABLE ADD COLUMN
            //    in 9.5 and earlier; the DO/EXCEPTION pattern is the
            //    portable guard).
            //
            //    DeleteBehavior.Restrict mirrors the Fluent API config in
            //    Data/AppDbContext.cs — an AccountingKey is the posting-time
            //    snapshot of all 8 segments and must NEVER vanish while
            //    JournalLines reference it.
            // ================================================================
            mb.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'JournalLines' AND column_name = 'AccountingKeyId'
                    ) THEN
                        ALTER TABLE ""JournalLines""
                            ADD COLUMN ""AccountingKeyId"" integer NULL
                                REFERENCES ""AccountingKeys""(""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS ix_journal_lines_accounting_key
                    ON ""JournalLines"" (""AccountingKeyId"")
                    WHERE ""AccountingKeyId"" IS NOT NULL;
            ");

            // ================================================================
            // 3) Backfill — mint one AccountingKey row per resolvable
            //    (CompanyId, AccountId) pair across existing JournalLines, then
            //    stamp JournalLine.AccountingKeyId.
            //
            //    RESOLUTION CASCADE (mirrors what the C# resolver does at
            //    write time):
            //
            //      JournalLine  → JournalEntry  → Book.CompanyId   (CompanyId)
            //      JournalLine.Account (varchar(50)) ─→ GlAccount.AccountNumber
            //                                          WHERE CompanyId matches
            //                                          OR is NULL (system COA)
            //
            //    Orphan rows (no Book / no Company / no matching GlAccount)
            //    stay NULL — they read through the legacy Account string per
            //    the DEF-008 fallback (no data loss, no breaking change).
            //
            //    Backfill creates AccountingKey rows with NULL segments other
            //    than (CompanyId, AccountId, IndustryVertical). The richer
            //    segments (CostCenter / Department / Project / etc.) attach
            //    on FUTURE posts via the resolver; historical rows simply
            //    don't carry that dimensionality (it was never captured at
            //    the posting moment).
            // ================================================================
            mb.Sql(@"
                -- Codex P1 fix on PR #325 — TENANT-PREFERRED GL account
                -- precedence. The previous draft used a plain INNER JOIN with
                -- `ga.CompanyId = b.CompanyId OR ga.CompanyId IS NULL`, which
                -- matches BOTH the tenant-owned AND the system-owned
                -- GlAccount rows when they share an AccountNumber. The C#
                -- resolver explicitly resolves this via
                -- `.OrderByDescending(a => a.CompanyId.HasValue)` (tenant
                -- wins) — the SQL backfill needs the same precedence or
                -- step 3.1 mints duplicate AccountingKeys (saved by the
                -- partial UNIQUE, but emits noise) and step 3.2 picks an
                -- arbitrary match (Postgres UPDATE...FROM uses any one).
                --
                -- DISTINCT ON pattern: for each (Book.CompanyId, jl.Account)
                -- pair, return exactly ONE GlAccount row, preferring the
                -- tenant-owned one (CompanyId NOT NULL) over the system one
                -- (CompanyId IS NULL). `NULLS LAST` on the ORDER BY does
                -- exactly that.
                --
                -- The resolved_accounts CTE is then used by both step 3.1
                -- (INSERT) and step 3.2 (UPDATE) so the backfill is
                -- internally consistent.

                -- Step 3.0 — Resolve tenant-preferred GlAccount per
                -- (CompanyId, JournalLine.Account) pair.
                WITH resolved_accounts AS (
                    SELECT DISTINCT ON (b.""CompanyId"", jl.""Account"")
                        b.""CompanyId""              AS company_id,
                        jl.""Account""               AS account_number,
                        ga.""Id""                    AS account_id,
                        co.""IndustryVertical""      AS vertical
                    FROM ""JournalLines"" jl
                    INNER JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
                    INNER JOIN ""Books"" b           ON b.""Id""  = je.""BookId""
                    INNER JOIN ""Companies"" co      ON co.""Id"" = b.""CompanyId""
                    INNER JOIN ""GlAccounts"" ga
                        ON ga.""AccountNumber"" = jl.""Account""
                        AND (ga.""CompanyId"" = b.""CompanyId"" OR ga.""CompanyId"" IS NULL)
                    WHERE b.""CompanyId"" IS NOT NULL
                    ORDER BY b.""CompanyId"", jl.""Account"",
                             ga.""CompanyId"" NULLS LAST   -- tenant wins over system
                )
                -- Step 3.1 — Mint AccountingKey rows for distinct (Company,
                -- Account) pairs derivable from existing JournalLines. The
                -- canonical string carries empty placeholders for the
                -- missing segments so the hash is stable across the
                -- backfill and the runtime resolver.
                INSERT INTO ""AccountingKeys"" (
                    ""CompanyId"", ""SiteId"", ""AccountId"", ""CostCenterId"",
                    ""DepartmentId"", ""ProjectId"", ""InterCoPartnerCompanyId"",
                    ""IndustryVertical"", ""AccountingKeyHash"", ""AccountingKeyString"",
                    ""IsActive"", ""CreatedAt"", ""CreatedBy""
                )
                SELECT
                    ra.company_id,
                    NULL::int,
                    ra.account_id,
                    NULL::int,
                    NULL::int,
                    NULL::int,
                    NULL::int,
                    ra.vertical,
                    encode(
                        digest(
                            ra.company_id::text
                                || '|' || ''                                     -- SiteId
                                || '|' || ra.account_id::text
                                || '|' || ''                                     -- CostCenterId
                                || '|' || ''                                     -- DepartmentId
                                || '|' || ''                                     -- ProjectId
                                || '|' || ''                                     -- InterCoPartnerCompanyId
                                || '|' || COALESCE(ra.vertical::text, ''),
                            'sha256'
                        ),
                        'hex'
                    ),
                    'Co=' || ra.company_id::text
                        || '|Site=|Acct=' || ra.account_id::text
                        || '|CC=|Dept=|Proj=|ICP=|Vert='
                        || COALESCE(ra.vertical::text, ''),
                    TRUE,
                    NOW(),
                    'pra5b-backfill'
                FROM (
                    -- Distinct (company_id, account_id, vertical) — collapse
                    -- the per-line resolution into per-AccountingKey rows.
                    SELECT DISTINCT company_id, account_id, vertical
                    FROM resolved_accounts
                ) ra
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AccountingKeys"" ak
                    WHERE ak.""CompanyId"" = ra.company_id
                      AND ak.""AccountingKeyHash"" = encode(
                          digest(
                              ra.company_id::text
                                  || '|' || ''
                                  || '|' || ra.account_id::text
                                  || '|' || ''
                                  || '|' || ''
                                  || '|' || ''
                                  || '|' || ''
                                  || '|' || COALESCE(ra.vertical::text, ''),
                              'sha256'
                          ),
                          'hex'
                      )
                );

                -- Step 3.2 — Stamp JournalLine.AccountingKeyId for resolvable
                -- rows using the same tenant-preferred precedence. Orphan
                -- rows (no Book / no Company / no matching account) stay
                -- NULL; legacy Account string handles them.
                WITH resolved_accounts AS (
                    SELECT DISTINCT ON (b.""CompanyId"", jl.""Account"")
                        jl.""Id""                    AS journal_line_id,
                        b.""CompanyId""              AS company_id,
                        ga.""Id""                    AS account_id
                    FROM ""JournalLines"" jl
                    INNER JOIN ""JournalEntries"" je ON je.""Id"" = jl.""JournalEntryId""
                    INNER JOIN ""Books"" b           ON b.""Id""  = je.""BookId""
                    INNER JOIN ""GlAccounts"" ga
                        ON ga.""AccountNumber"" = jl.""Account""
                        AND (ga.""CompanyId"" = b.""CompanyId"" OR ga.""CompanyId"" IS NULL)
                    WHERE b.""CompanyId"" IS NOT NULL
                      AND jl.""AccountingKeyId"" IS NULL
                    ORDER BY b.""CompanyId"", jl.""Account"",
                             ga.""CompanyId"" NULLS LAST
                )
                UPDATE ""JournalLines"" jl
                SET ""AccountingKeyId"" = ak.""Id""
                FROM resolved_accounts ra
                INNER JOIN ""AccountingKeys"" ak
                    ON ak.""CompanyId"" = ra.company_id
                    AND ak.""AccountId"" = ra.account_id
                    AND ak.""SiteId"" IS NULL
                    AND ak.""CostCenterId"" IS NULL
                    AND ak.""DepartmentId"" IS NULL
                    AND ak.""ProjectId"" IS NULL
                    AND ak.""InterCoPartnerCompanyId"" IS NULL
                WHERE jl.""Id"" = ra.journal_line_id
                  AND jl.""AccountingKeyId"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Restore: drop the FK column on JournalLines first (the index
            // is dropped automatically), then drop the AccountingKeys table.
            // pgcrypto extension stays — other future migrations may use it.
            mb.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'JournalLines' AND column_name = 'AccountingKeyId'
                    ) THEN
                        ALTER TABLE ""JournalLines"" DROP COLUMN ""AccountingKeyId"";
                    END IF;
                END $$;

                DROP TABLE IF EXISTS ""AccountingKeys"";
            ");
        }
    }
}
