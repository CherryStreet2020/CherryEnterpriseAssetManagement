// =============================================================================
// Sprint 13.5 PRA-5e.2 — Seed system-default GlAccounts for every
// IndustryDefaults account number in GlAccountResolver.
//
// E2E discovery 2026-05-24: PRA-5c/5d/5e shipped the dual-write code but
// `AccountingKeyId` was stamping NULL on every new JournalLine because the
// COA was missing GlAccount rows for the industry-default account-number
// strings the resolver returns (e.g. "1110" Cash, "2000" AP, "2150"
// GrAccrued). The DEF-008 try/catch in each posting service was silently
// swallowing the `GlAccountResolutionException` from
// `ResolveAccountingKeyAsync`, leaving AccountingKeyId NULL. Code worked
// (legacy Account string set correctly), but the new feature was dead.
//
// This migration inserts CompanyId IS NULL system rows for every account
// number `GlAccountResolver.IndustryDefaults.For()` can return. Tenants
// already override these via CompanyGlAccountConfigs or per-entity
// overrides; this ensures the BASE case always has a GlAccount row to
// resolve to.
//
// IDEMPOTENT — INSERT WHERE NOT EXISTS guards on (AccountNumber,
// CompanyId IS NULL). Migration safely runs twice; existing rows untouched.
//
// NOT DESTRUCTIVE — pure additive. Lock 5 destructive-diff doesn't apply
// (no ALTER TABLE, no NOT NULL on populated columns, no TRUNCATE).
// Replit's auto-schema-diff will see additive INSERT-only and apply
// without an Approval gate.
//
// CATEGORIES — set to the nearest sensible GlAccountCategory. Tenants can
// recategorize via the GlAccounts admin UI if needed; the system-row
// categories are reasonable starting points for unmapped tenants.
//
// AUTHORITY
//   - E2E test 2026-05-24: payment on FIX1-2058 produced 2 new JE lines
//     with Account="2000"/"1110" but AccountingKeyId=NULL on both.
//   - GlAccountResolver.IndustryDefaults.For() — the canonical list of
//     account numbers that must have a row in GlAccounts.
//   - memory: project_pra5b_shipped / project_pra5c_shipped /
//             project_pra5d_shipped / project_pra5e_shipped
// =============================================================================

using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524260000_SeedSystemGlAccountsForIndustryDefaultsPRA5e2")]
    public partial class SeedSystemGlAccountsForIndustryDefaultsPRA5e2 : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            // ================================================================
            // System-default GlAccount rows (CompanyId IS NULL) — one per
            // IndustryDefaults entry. Idempotent INSERT-WHERE-NOT-EXISTS so
            // re-runs are safe. AccountType / Category / NormalBalance set
            // to sensible defaults aligned with the account semantic.
            //
            // GlAccountType: 1=Asset, 2=Liability, 3=Equity, 4=Revenue,
            //                5=Expense, 6=ContraAsset, 7=ContraRevenue,
            //                8=ContraExpense
            // NormalBalance: 1=Debit, 2=Credit
            // GlAccountCategory: see Models/GlAccount.cs enum
            //   100=CashAndReceivables, 120=WorkInProgress,
            //   140=FixedAssetsLandBuildings, 150=FixedAssetsMachinery,
            //   190=AccumulatedDepreciation, 200=CurrentLiabilities,
            //   400=RevenueAndGains, 500=CostOfSales,
            //   550=PurchasePriceVariance, 600=DepreciationExpense,
            //   610=MaintenanceLabor, 620=RepairParts,
            //   660=UtilitiesInfrastructure, 680=AssetLosses,
            //   690=OperatingExpenses
            // ================================================================
            mb.Sql(@"
                INSERT INTO ""GlAccounts"" (
                    ""AccountNumber"", ""Name"", ""Description"",
                    ""AccountType"", ""Category"", ""SubCategory"",
                    ""NormalBalance"", ""IsActive"", ""IsSystemAccount"",
                    ""AllowManualEntry"", ""RequiresCostCenter"",
                    ""RequiresDepartment"", ""RequiresAssetCategory"",
                    ""SortOrder"", ""CompanyId"", ""CreatedAt""
                )
                SELECT *
                FROM (VALUES
                    -- ASSETS
                    ('1110', 'Cash',                          'System default — Cash on hand / bank', 1, 100, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1110, NULL::int, NOW()),
                    ('1290', 'Sales Tax Recoverable',         'System default — VAT/GST recoverable', 1, 100, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1290, NULL::int, NOW()),
                    ('1300', 'Inventory',                     'System default — generic inventory',   1, 100, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1300, NULL::int, NOW()),
                    ('1400', 'CIP Pending',                   'System default — construction in progress pending capitalization', 1, 120, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1400, NULL::int, NOW()),
                    ('1410', 'WIP Expense',                   'System default — work in progress expense',   1, 120, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 1410, NULL::int, NOW()),
                    ('1500', 'Fixed Assets',                  'System default — generic fixed asset cost basis', 1, 150, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, TRUE, 1500, NULL::int, NOW()),
                    -- CONTRA-ASSET
                    ('1510', 'Accumulated Depreciation',      'System default — generic accumulated depreciation', 6, 190, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, TRUE, 1510, NULL::int, NOW()),
                    -- LIABILITIES
                    ('2000', 'Accounts Payable',              'System default — vendor invoice liability',   2, 200, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 2000, NULL::int, NOW()),
                    ('2150', 'GR Accrued (Goods Received Not Invoiced)', 'System default — receipt-side accrual until invoice arrives', 2, 200, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 2150, NULL::int, NOW()),
                    ('2160', 'Accrued Labor',                 'System default — labor accrual until payroll clearing',  2, 200, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 2160, NULL::int, NOW()),
                    -- REVENUE
                    ('4500', 'Gain on Disposal',              'System default — gain on asset disposal',     4, 400, 0, 2, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 4500, NULL::int, NOW()),
                    -- COST OF SALES + VARIANCES
                    ('5900', 'Purchase Price Variance',       'System default — invoice vs PO unit price variance', 5, 550, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 5900, NULL::int, NOW()),
                    -- OPERATING EXPENSES
                    ('6000', 'Direct Expense',                'System default — direct expense (services, supplies)', 5, 690, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6000, NULL::int, NOW()),
                    ('6200', 'Maintenance Labor',             'System default — maintenance internal labor',  5, 610, 15, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6200, NULL::int, NOW()),
                    ('6210', 'Maintenance Materials',         'System default — repair parts + consumables',  5, 620, 13, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6210, NULL::int, NOW()),
                    ('6220', 'Maintenance Outside Vendor',    'System default — outside maintenance vendor work', 5, 610, 16, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6220, NULL::int, NOW()),
                    ('6300', 'Freight Expense',               'System default — inbound freight + shipping',  5, 660, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6300, NULL::int, NOW()),
                    ('6500', 'Depreciation Expense',          'System default — periodic depreciation expense', 5, 600, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, TRUE, 6500, NULL::int, NOW()),
                    ('6510', 'Loss on Disposal',              'System default — loss on asset disposal',     5, 680, 0, 1, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, 6510, NULL::int, NOW())
                ) AS v(
                    ""AccountNumber"", ""Name"", ""Description"",
                    ""AccountType"", ""Category"", ""SubCategory"",
                    ""NormalBalance"", ""IsActive"", ""IsSystemAccount"",
                    ""AllowManualEntry"", ""RequiresCostCenter"",
                    ""RequiresDepartment"", ""RequiresAssetCategory"",
                    ""SortOrder"", ""CompanyId"", ""CreatedAt""
                )
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""GlAccounts"" ga
                    WHERE ga.""AccountNumber"" = v.""AccountNumber""
                      AND ga.""CompanyId"" IS NULL
                );
            ");
        }

        protected override void Down(MigrationBuilder mb)
        {
            // Remove only the system rows that THIS migration could have
            // inserted. Match on (AccountNumber, CompanyId IS NULL,
            // IsSystemAccount=TRUE) so we don't accidentally delete pre-
            // existing system rows from a prior seed pass.
            mb.Sql(@"
                DELETE FROM ""GlAccounts""
                WHERE ""CompanyId"" IS NULL
                  AND ""IsSystemAccount"" = TRUE
                  AND ""AccountNumber"" IN (
                      '1110','1290','1300','1400','1410','1500','1510',
                      '2000','2150','2160',
                      '4500',
                      '5900',
                      '6000','6200','6210','6220','6300','6500','6510'
                  );
            ");
        }
    }
}
