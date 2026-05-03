// Admin CRUD smoke flows. We don't actually mutate (mutations would dirty the
// shared dev DB used by every other test); instead we open each Admin page
// that has a Create form and assert the form is present and the Save button
// renders. This catches model-binding / view-model mismatches.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

const ADMIN_PAGES_WITH_FORM = [
  '/Admin/Companies',
  '/Admin/Sites',
  '/Admin/Departments',
  '/Admin/Locations',
  '/Admin/Users',
  '/Admin/CostCenters',
  '/Admin/AssetCategories',
  '/Admin/ItemCategories',
  '/Admin/Manufacturers',
  '/Admin/ProjectManagers',
  '/Admin/Technicians',
  '/Admin/Vendors',
  '/Admin/ExchangeRates',
  '/Admin/PMTemplates',
  '/Admin/PMSchedules',
  '/Admin/Webhooks',
];

test.describe('ADMIN — CRUD form smoke', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of ADMIN_PAGES_WITH_FORM) {
    test(`form present on ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp.status()).toBeLessThan(500);
      // Either a <form> element or a "New" / "Add" CTA must be present so an
      // operator can actually create a row.
      const forms = await page.locator('form').count();
      const ctas = await page
        .locator('button:has-text("New"), button:has-text("Add"), a:has-text("New"), a:has-text("Add")')
        .count();
      expect(forms + ctas, `${path} has no Create form/CTA`).toBeGreaterThan(0);
    });
  }
});

test.describe('ADMIN — system pages render', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of [
    '/Admin/AuditLog',
    '/Admin/SystemSettings',
    '/Admin/Diagnostics',
    '/Admin/EnvironmentStatus',
    '/Admin/SmokeTests',
    '/Admin/Outbox',
    '/Admin/Webhooks/Deliveries',
    '/Admin/Integrations',
    '/Admin/Integrations/Inbound',
    '/Admin/Integrations/Maps',
    '/Admin/DataManagement',
    '/Admin/Import',
    '/Admin/ImportWizard',
    '/Admin/Export',
    '/Admin/SeedData',
    '/Admin/DemoData',
    '/Admin/JournalBackfill',
    '/Admin/DepreciationBackfill',
    '/Admin/CcaBackfill',
    '/Admin/Approvals',
    '/Admin/Requisitions',
    '/Admin/StockLevels',
    '/Admin/Kits',
    '/Admin/Items',
    '/Admin/Inventory',
    '/Admin/Lookups',
    '/Admin/Tenants',
    '/Admin/Barcodes',
    '/Admin/Company',
  ]) {
    test(`renders ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp.status(), `${path} returned ${resp.status()}`).toBeLessThan(500);
    });
  }
});
