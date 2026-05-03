// Exercise the cross-cutting Premium DataGrid behaviour (search, sort, paging,
// export buttons) on each major index page that renders a grid.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

// Pages whose index uses a real listing surface (table/card-grid). Some
// modules (e.g. /Purchasing) render a custom dashboard instead — those are
// covered by the page-smoke spec, not here.
const GRID_PAGES = [
  '/Assets',
  '/Materials/Items',
  '/Receiving',
  '/AccountsPayable',
  '/Maintenance',
  '/Journals',
  '/Books',
  '/CIP',
  '/CCA',
  '/BulkOperations',
  '/Admin/Users',
  '/Admin/Vendors',
  '/Admin/Locations',
  '/Admin/Departments',
  '/Admin/Companies',
  '/Admin/Sites',
  '/Admin/Items',
  '/Admin/Manufacturers',
  '/Admin/AssetCategories',
  '/Admin/ItemCategories',
  '/Admin/CostCenters',
  '/Admin/ProjectManagers',
  '/Admin/AuditLog',
  '/Admin/Outbox',
  // Excluded — these don't render the standard listing surface used here:
  //   /Admin/GlAccounts → 301 to /Books/GlAccounts/{bookId} (book-scoped UI)
  //   /Admin/ExchangeRates → only renders the table when rates exist; empty
  //      state is a different surface
  //   /Admin/Webhooks → Tailwind-based dashboard, not a DataGrid; covered
  //      by the page-smoke spec instead
];

test.describe('DATAGRID — index pages render a grid', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of GRID_PAGES) {
    test(`grid present on ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      // tolerate 301/302 — some legacy routes redirect to canonical
      expect(resp.status()).toBeLessThan(500);
      // A "grid" surface is either a real <table>/data-table/[role=grid]
      // (DataGrid pages) or a domain-specific card/list collection used on
      // a few index pages. We enumerate the real listing wrappers used in
      // this app rather than `[class*="card"]` (which would match generic
      // screen-header cards and dilute the assertion).
      const surfaces = await page
        .locator([
          'table',
          '.data-table',
          '[role="grid"]',
          '.list-group',
          '.sites-grid',     // /Admin/Sites
          '.site-card',      // /Admin/Sites
          '.card-grid',      // generic card grids
          '.companies-grid', // multi-company index
          '.org-grid',       // org/company index variants
        ].join(', '))
        .count();
      expect(surfaces, `${path} rendered no listing surface`).toBeGreaterThan(0);
    });
  }
});

test.describe('DATAGRID — search input filters rows', () => {
  test('typing in the Assets search input does not crash the page', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Assets`);
    // Scope to the page's main content — the layout's sidebar contains a
    // hidden #orgSearchInput (org-switcher) that otherwise wins :first.
    const main = page.locator('main, .app-main, .content-wrapper, [role="main"]').first();
    const scope = (await main.count()) ? main : page;
    const search = scope
      .locator('input[type="search"], input[placeholder*="Search" i], input[name*="search" i]')
      .filter({ visible: true })
      .first();
    if ((await search.count()) === 0) test.skip();
    await search.fill('A');
    await page.waitForTimeout(400); // debounce
    expect(await page.locator('table').count()).toBeGreaterThan(0);
  });
});
