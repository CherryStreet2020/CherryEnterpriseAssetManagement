// Exercise every report page: render the page, run the report, and (where
// supported) trigger the export endpoints (xlsx/pdf/csv) so we catch
// ClosedXML / QuestPDF / data-shape failures in CI.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

const REPORT_PAGES = [
  '/Reports',
  '/Reports/ReportHub',
  '/Reports/Builder',
  '/Reports/ChartOfAccounts',
  '/Reports/Compliance',
  '/Reports/DepreciationPreview',
  '/Reports/DepreciationSchedule',
  '/Reports/Form4562',
  '/Reports/T2Schedule8',
];

test.describe('REPORTS — page render', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of REPORT_PAGES) {
    test(`renders ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp.status()).toBe(200);
      // Reports must render at least one heading or table.
      const hasContent = await page.locator('h1, h2, .page-title, table').first().count();
      expect(hasContent).toBeGreaterThan(0);
    });
  }
});

test.describe('REPORTS — exports', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  // Reports/Export is the unified export endpoint. Hit it with each format
  // for the most common report keys; we just need a non-500 response.
  const formats = ['xlsx', 'pdf', 'csv'];
  const reportKeys = [
    'depreciation-schedule',
    'depreciation-preview',
    'chart-of-accounts',
    'form4562',
    't2-schedule8',
  ];

  for (const key of reportKeys) {
    for (const fmt of formats) {
      test(`export ${key} as ${fmt}`, async ({ page }) => {
        const url = `${BASE}/Reports/Export?report=${encodeURIComponent(key)}&format=${fmt}`;
        const resp = await page.request.get(url);
        expect(resp.status(), `${url} returned ${resp.status()}`).toBeLessThan(500);
      });
    }
  }
});
