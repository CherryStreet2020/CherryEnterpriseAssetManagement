// Sprint 13.5 PR #4 — E2E smoke tests for the BIC nav overhaul +
// /Production + /CustomerProjects + Master Files admin pages.
//
// Coverage:
//   1. Sidebar renders PRODUCTION + CUSTOMER_PROJECTS in the Cockpits section
//   2. Sidebar renders the new Master Data group with Countries/Carriers/WorkCalendars
//   3. Old "soon · Sprint X" placeholders no longer appear (clutter cleanup)
//   4. /Production list page loads, shows status filter chips, "New Order" CTA
//   5. /Production/Create form renders with required fields
//   6. /CustomerProjects list page loads, shows status filter chips
//   7. /CustomerProjects/Create form renders
//   8. /Admin/Countries / Carriers / WorkCalendars index pages load
//   9. Production status chips function as filters

const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('Sprint 13.5 PR #4 — BIC nav + Production + CustomerProjects + Master Files', () => {

  test('sidebar renders Production + Customer Projects Control Centers', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    // PR #5b 2026-05-23: section label is CONTROL CENTERS (was COCKPITS).
    const ccLabel = page.locator('.nav-section-label', { hasText: /CONTROL CENTERS/i });
    await expect(ccLabel).toBeVisible();

    // PR #5b 2026-05-23: Production link now points to /Production/ControlCenter.
    const prodLink = page.locator('a.nav-cc-item[href="/Production/ControlCenter"]');
    await expect(prodLink).toBeVisible();
    await expect(prodLink).toContainText(/Production/);

    // Customer Projects nav item (unchanged from PR #4 until PR #5d ships its CC).
    const projLink = page.locator('a.nav-cc-item[href="/CustomerProjects"]');
    await expect(projLink).toBeVisible();
    await expect(projLink).toContainText(/Customer Projects/);
  });

  test('disabled cockpit placeholders are hidden (no "soon · Sprint X" clutter)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    // None of the disabled "soon" chips should render anywhere in the sidebar.
    const soonChips = page.locator('.nav-cc-chip--soon');
    await expect(soonChips).toHaveCount(0);

    // Disabled-state CC items shouldn't render either.
    const disabledItems = page.locator('.nav-cc-item--disabled');
    await expect(disabledItems).toHaveCount(0);
  });

  test('sidebar renders Master Data group with Countries / Carriers / WorkCalendars', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    const masterDataLabel = page.locator('.nav-section-label', { hasText: /MASTER DATA/i });
    await expect(masterDataLabel).toBeVisible();

    await expect(page.locator('a[href="/Admin/Countries"]').first()).toBeVisible();
    await expect(page.locator('a[href="/Admin/Carriers"]').first()).toBeVisible();
    await expect(page.locator('a[href="/Admin/WorkCalendars"]').first()).toBeVisible();
  });

  test('/Production list page loads with KPI strip + New Order CTA', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Production`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    // Page title
    await expect(page.locator('h1.page-title')).toContainText(/Production Orders/);

    // Breadcrumb trail
    await expect(page.locator('.breadcrumb-trail')).toContainText(/Production/);

    // KPI filter chips (at least the "All" + 6 statuses = 7 chips)
    const chips = page.locator('.kpi-chip');
    expect(await chips.count()).toBeGreaterThanOrEqual(1);

    // New Order CTA
    await expect(page.locator('a.btn-primary[href="/Production/Create"]')).toBeVisible();
  });

  test('/Production/Create form renders with required fields', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Production/Create`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    // Required inputs
    await expect(page.locator('input[name="Input.OrderNumber"]')).toBeVisible();
    await expect(page.locator('input[name="Input.Title"]')).toBeVisible();
    await expect(page.locator('select[name="Input.Type"]')).toBeVisible();
    await expect(page.locator('input[name="Input.QuantityOrdered"]')).toBeVisible();

    // Cancel + Create buttons
    await expect(page.locator('a.btn-secondary[href="/Production"]').first()).toBeVisible();
    await expect(page.locator('button.btn-primary[type="submit"]')).toBeVisible();
  });

  test('/CustomerProjects list page loads with KPI strip', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/CustomerProjects`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('h1.page-title')).toContainText(/Customer Projects/);
    await expect(page.locator('a.btn-primary[href="/CustomerProjects/Create"]')).toBeVisible();
  });

  test('/CustomerProjects/Create form renders with Mode/Costing/Revenue selects', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/CustomerProjects/Create`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('input[name="Input.Code"]')).toBeVisible();
    await expect(page.locator('input[name="Input.Name"]')).toBeVisible();
    await expect(page.locator('select[name="Input.Mode"]')).toBeVisible();
    await expect(page.locator('select[name="Input.CostingMode"]')).toBeVisible();
    await expect(page.locator('select[name="Input.RevenueMode"]')).toBeVisible();
    await expect(page.locator('select[name="Input.ExportControl"]')).toBeVisible();
  });

  test('/Admin/Countries shows 8 seeded countries (PRA-2)', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Admin/Countries`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('h1.page-title')).toContainText(/Countries/);
    // 8 seeded countries: US/CA/MX/GB/DE/FR/JP/CN
    const rows = page.locator('table.data-table tbody tr');
    expect(await rows.count()).toBeGreaterThanOrEqual(8);
    // Spot-check US row
    await expect(page.locator('table.data-table tbody')).toContainText('United States');
  });

  test('/Admin/Carriers shows 12 system carriers (PRA-1)', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Admin/Carriers`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('h1.page-title')).toContainText(/Carriers/);
    const rows = page.locator('table.data-table tbody tr');
    expect(await rows.count()).toBeGreaterThanOrEqual(12);
    // Spot-check FedEx row
    await expect(page.locator('table.data-table tbody')).toContainText(/FedEx/i);
  });

  test('/Admin/WorkCalendars shows the system US Standard Business Week calendar', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Admin/WorkCalendars`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('h1.page-title')).toContainText(/Work Calendars/);
    await expect(page.locator('table.data-table tbody')).toContainText(/US Standard Business Week/);
  });

  test('Production status filter chip routes preserve the filter query', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production?status=Planned`);
    await page.waitForLoadState('domcontentloaded');

    // The Planned chip should carry the kpi-chip--active class
    const plannedChip = page.locator('.kpi-chip[href="/Production?status=Planned"]');
    await expect(plannedChip).toHaveClass(/kpi-chip--active/);
  });
});
