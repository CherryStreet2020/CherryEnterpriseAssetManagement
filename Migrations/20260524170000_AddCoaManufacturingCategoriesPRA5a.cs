// =============================================================================
// Sprint 13.5 PRA-5a — COA additive expansion (Master Files Baseline ship #2).
//
// PURE ADDITIVE — NO schema changes. The GlAccountCategory enum just got 26
// new values in Models/GlAccount.cs; this migration seeds ~30 SYSTEM-template
// `GlAccount` rows (CompanyId IS NULL, IsSystemAccount = TRUE) under those
// new categories so tenant onboarding has a reference catalog to copy from.
//
// AUTHORITY:
//   - docs/research/master-files-baseline-2026-05-24.md §4
//   - memory: reference_master_files_baseline.md
//   - memory: reference_bic_entity_checklist.md
//   - memory: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock)
//   - memory: feedback_replit_autodiff_destructive_on_populated_tables.md
//
// IDEMPOTENT — every INSERT uses `WHERE NOT EXISTS` against the same
// (CompanyId IS NULL, AccountNumber) pair, so re-running is a no-op. No
// UNIQUE constraint added in this PR (Lock 5 + minimum-surface principle).
//
// NO HARDCODED TENANT DATA — all rows are CompanyId NULL system templates.
// Tenants don't see these in their normal queries unless the onboarding
// flow materializes copies into their CompanyId.
//
// SCOPE — 30 system template accounts across the new categories:
//   8 inventory  · 1 equity  · 4 revenue  · 16 COGS/variance/production-cost
//
// NOT in scope (separate cascade PRs):
//   - PRA-5b: AccountingKey segment-refactor (Company-Site-Account-CostCenter-
//             Dept-Project-InterCoPartner-Vertical) — touches JournalLine
//   - PRA-6:  Currency / PaymentTerm / TaxCode real-table masters
//   - PRA-7:  Warehouse / Bin / Lot / SerialMaster / ItemGroup→PostingProfile
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524170000_AddCoaManufacturingCategoriesPRA5a")]
    public partial class AddCoaManufacturingCategoriesPRA5a : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ----------------------------------------------------------------
            // Seed 30 SYSTEM-template GlAccount rows.
            //
            // Column order must match the GlAccount entity:
            //   AccountNumber / Name / Description / AccountType (int from
            //   GlAccountType enum) / Category (int from GlAccountCategory
            //   enum) / SubCategory (int = 0 = None) / NormalBalance (int
            //   from NormalBalance enum) / IsActive / IsSystemAccount /
            //   AllowManualEntry / RequiresCostCenter / RequiresDepartment /
            //   RequiresAssetCategory / SortOrder / CompanyId / CreatedAt
            //
            // AccountType:    1=Asset 2=Liab 3=Eq 4=Rev 5=Exp 6=ContraAsset
            //                 7=ContraRev 8=ContraExp
            // NormalBalance:  1=Debit 2=Credit
            // ----------------------------------------------------------------

            // Helper to keep each INSERT compact + idempotent.
            //   args: account#, name, type, cat, normalBalance, sortOrder,
            //         requiresCostCenter, requiresDepartment, isAutoPostOnly
            // (We can't define a local helper inside MigrationBuilder.Sql;
            //  expand inline for each row instead.)

            mb.Sql(@"
                -- ASSETS — manufacturing inventory (111-118)

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1300', 'Raw Material Inventory',
                       'System template — raw material inventory for manufactured products.',
                       1, 111, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1300, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1300');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1310', 'WIP Inventory — Production',
                       'System template — production work-in-process (distinct from CIP=120).',
                       1, 112, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1310, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1310');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1320', 'Finished Goods Inventory',
                       'System template — finished goods awaiting sale.',
                       1, 113, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1320, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1320');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1330', 'Sub-Assembly Inventory',
                       'System template — sub-assemblies feeding higher-level production.',
                       1, 114, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1330, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1330');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1340', 'Subcontract Inventory',
                       'System template — parts at subcontractor (we own, they hold).',
                       1, 115, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1340, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1340');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1350', 'Consigned Inventory',
                       'System template — vendor-owned inventory on our floor (consignment).',
                       1, 116, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1350, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1350');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1360', 'Inventory in Transit',
                       'System template — inventory en route between locations.',
                       1, 117, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1360, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1360');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '1370', 'Scrap Inventory',
                       'System template — scrap awaiting disposition / sale.',
                       1, 118, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1370, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '1370');
            ");

            mb.Sql(@"
                -- EQUITY — period-end clearing (305)

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '3050', 'Current Year Earnings',
                       'System template — current-year net income clearing account (period-close roll-forward).',
                       3, 305, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 3050, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '3050');
            ");

            mb.Sql(@"
                -- REVENUE — product/service/contract split + intercompany (401-410)

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '4010', 'Product Revenue',
                       'System template — revenue from product sales.',
                       4, 401, 0, 2, TRUE, TRUE, TRUE, FALSE, FALSE, FALSE, 4010, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '4010');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '4020', 'Service Revenue',
                       'System template — revenue from services / labor billings.',
                       4, 402, 0, 2, TRUE, TRUE, TRUE, FALSE, FALSE, FALSE, 4020, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '4020');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '4030', 'Contract Revenue',
                       'System template — long-term contract revenue (percentage-of-completion / milestone).',
                       4, 403, 0, 2, TRUE, TRUE, TRUE, FALSE, FALSE, FALSE, 4030, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '4030');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '4100', 'Intercompany Sales',
                       'System template — sales to intercompany partners (eliminates at consolidation).',
                       4, 410, 0, 2, TRUE, TRUE, TRUE, FALSE, FALSE, FALSE, 4100, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '4100');
            ");

            mb.Sql(@"
                -- COGS + MANUFACTURING (510-596) — 16 templates

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5150', 'Intercompany COGS',
                       'System template — COGS for intercompany sales (eliminates at consolidation).',
                       5, 510, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 5150, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5150');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5200', 'Production Labor Expense',
                       'System template — direct production labor (distinct from maintenance labor 610).',
                       5, 520, 0, 1, TRUE, TRUE, FALSE, TRUE, TRUE, FALSE, 5200, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5200');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5210', 'Production Overhead',
                       'System template — manufacturing overhead applied to production.',
                       5, 530, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5210, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5210');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5220', 'Production Consumables',
                       'System template — consumables used in production (lubricants, abrasives, cutting fluids, etc.).',
                       5, 540, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5220, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5220');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5300', 'Purchase Price Variance',
                       'System template — PPV: actual purchase price vs standard cost.',
                       5, 550, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 5300, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5300');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5310', 'Material Usage Variance',
                       'System template — MUV: actual material usage vs standard BOM.',
                       5, 560, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5310, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5310');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5320', 'Labor Rate Variance',
                       'System template — LRV: actual labor rate vs standard rate.',
                       5, 570, 0, 1, TRUE, TRUE, FALSE, TRUE, TRUE, FALSE, 5320, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5320');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5330', 'Labor Efficiency Variance',
                       'System template — LEV: actual labor hours vs standard hours.',
                       5, 580, 0, 1, TRUE, TRUE, FALSE, TRUE, TRUE, FALSE, 5330, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5330');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5340', 'Overhead Applied',
                       'System template — overhead applied to production (CREDIT normal balance — subtractive against CostOfSales).',
                       5, 590, 0, 2, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5340, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5340');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5350', 'Overhead Spending Variance',
                       'System template — overhead spending: actual OH spend vs budgeted OH at standard.',
                       5, 591, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5350, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5350');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5360', 'Overhead Volume Variance',
                       'System template — overhead volume: actual production volume vs budgeted absorption volume.',
                       5, 592, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5360, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5360');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5370', 'Yield Variance',
                       'System template — yield variance for process industries (actual output vs theoretical).',
                       5, 593, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5370, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5370');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5400', 'Scrap Expense',
                       'System template — scrap loss expense (material destroyed during production).',
                       5, 594, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5400, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5400');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5410', 'Rework Expense',
                       'System template — labor + material expense for rework loops.',
                       5, 595, 0, 1, TRUE, TRUE, FALSE, TRUE, FALSE, FALSE, 5410, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5410');

                INSERT INTO ""GlAccounts""
                    (""AccountNumber"", ""Name"", ""Description"", ""AccountType"", ""Category"", ""SubCategory"", ""NormalBalance"",
                     ""IsActive"", ""IsSystemAccount"", ""AllowManualEntry"", ""RequiresCostCenter"", ""RequiresDepartment"",
                     ""RequiresAssetCategory"", ""SortOrder"", ""CompanyId"", ""CreatedAt"")
                SELECT '5500', 'WIP-to-FG Clearing',
                       'System template — clearing account for WIP → Finished Goods transfer.',
                       5, 596, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 5500, NULL, NOW()
                WHERE NOT EXISTS (SELECT 1 FROM ""GlAccounts"" WHERE ""CompanyId"" IS NULL AND ""AccountNumber"" = '5500');
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Pure rollback — remove only the system templates this migration
            // added. Tenant rows (CompanyId IS NOT NULL) are not touched.
            mb.Sql(@"
                DELETE FROM ""GlAccounts""
                WHERE ""CompanyId"" IS NULL
                  AND ""IsSystemAccount"" = TRUE
                  AND ""AccountNumber"" IN (
                    '1300','1310','1320','1330','1340','1350','1360','1370',
                    '3050',
                    '4010','4020','4030','4100',
                    '5150','5200','5210','5220',
                    '5300','5310','5320','5330','5340','5350','5360','5370','5400','5410','5500'
                  );
            ");
        }
    }
}
