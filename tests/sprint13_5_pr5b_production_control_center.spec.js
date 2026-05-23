// Sprint 13.5 PR #5b — E2E smoke tests for the Production Control Center.
//
// Validates Dean's "true Control Center" quality bar (memory:
// feedback_control_center_quality_bar.md):
//
//   1. Sidebar label is CONTROL CENTERS (not COCKPITS — terminology lock)
//   2. Production sidebar link points to /Production/ControlCenter
//   3. /Production/ControlCenter loads (200)
//   4. KPI band renders 6 tiles
//   5. AI summary strip renders
//   6. Three tabs present (Queue / Exceptions / Activity)
//   7. Tab switching via query string works
//   8. Filter chips present on Queue tab + status filter wires through
//   9. Free-text search form present
//  10. Bulk toolbar exists (hidden until selection)
//  11. Selecting a row reveals the bulk toolbar (visual state)
//  12. Verb tray buttons rendered for at least one queue row (when data present)
//  13. Exceptions tab loads its lane
//  14. Activity tab loads the feed

const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('Sprint 13.5 PR #5b — Production Control Center', () => {

  test('sidebar uses CONTROL CENTERS label (not COCKPITS)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    const label = page.locator('.nav-section-label', { hasText: /CONTROL CENTERS/i });
    await expect(label).toBeVisible();

    // Old terminology must NOT appear as the section label.
    const oldLabel = page.locator('.nav-section-label', { hasText: /^COCKPITS$/ });
    await expect(oldLabel).toHaveCount(0);
  });

  test('Production sidebar link points to /Production/ControlCenter', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    const prodLink = page.locator('a.nav-cc-item[href="/Production/ControlCenter"]');
    await expect(prodLink).toBeVisible();
    await expect(prodLink).toContainText(/Production/);
  });

  test('/Production/ControlCenter loads (200) with title + breadcrumb', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Production/ControlCenter`);
    expect(resp.status()).toBe(200);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('h1.cc-title')).toContainText(/Production Control Center/);
    await expect(page.locator('.breadcrumb-trail')).toContainText(/Production Control Center/);
  });

  test('KPI band renders 6 tiles', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const tiles = page.locator('.cc-kpi-tile');
    await expect(tiles).toHaveCount(6);
  });

  test('AI summary strip renders', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.cc-ai-strip')).toBeVisible();
    await expect(page.locator('.cc-ai-label')).toContainText(/AI Snapshot/i);
  });

  test('three tabs render: Queue / Exceptions / Activity', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const tabs = page.locator('.cc-tab');
    await expect(tabs).toHaveCount(3);
    await expect(tabs.nth(0)).toContainText(/Queue/i);
    await expect(tabs.nth(1)).toContainText(/Exceptions/i);
    await expect(tabs.nth(2)).toContainText(/Activity/i);
  });

  test('Queue tab is active by default', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const activeTab = page.locator('.cc-tab.cc-tab--active');
    await expect(activeTab).toContainText(/Queue/i);
  });

  test('Exceptions tab switches via ?tab=exceptions', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=exceptions`);
    await page.waitForLoadState('domcontentloaded');

    const activeTab = page.locator('.cc-tab.cc-tab--active');
    await expect(activeTab).toContainText(/Exceptions/i);
  });

  test('Activity tab switches via ?tab=activity', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=activity`);
    await page.waitForLoadState('domcontentloaded');

    const activeTab = page.locator('.cc-tab.cc-tab--active');
    await expect(activeTab).toContainText(/Activity/i);
  });

  test('Filter chips render on Queue tab (All Active + 4 statuses)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const chips = page.locator('.cc-filter-row a.cc-chip');
    // 1 "All Active" + 4 status chips = 5 minimum
    await expect(chips).toHaveCount(5);
    await expect(chips.first()).toContainText(/All Active/i);
  });

  test('Status filter chip wires through query string', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=queue&status=Released`);
    await page.waitForLoadState('domcontentloaded');

    const activeChip = page.locator('.cc-chip.cc-chip--active');
    await expect(activeChip).toBeVisible();
    await expect(activeChip).toContainText(/Released/i);
  });

  test('Free-text search form is present', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('input.cc-search-input[name="q"]')).toBeVisible();
  });

  test('Search query echoes into the input + persists tab', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=queue&q=PRO`);
    await page.waitForLoadState('domcontentloaded');

    const input = page.locator('input.cc-search-input[name="q"]');
    await expect(input).toHaveValue('PRO');
  });

  test('Bulk toolbar exists (hidden until selection)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    // Page may render an empty state if no rows exist; toolbar only renders
    // alongside the queue list. Skip if no rows present.
    const list = page.locator('ul.cc-queue-list');
    if ((await list.count()) === 0) { test.skip(); return; }

    const toolbar = page.locator('.cc-bulk-toolbar');
    await expect(toolbar).toHaveCount(1);
    // Must NOT carry the --active modifier before selection.
    await expect(toolbar).not.toHaveClass(/cc-bulk-toolbar--active/);
  });

  test('Selecting a row reveals the bulk toolbar', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const firstCheck = page.locator('input.cc-queue-check').first();
    if ((await firstCheck.count()) === 0) { test.skip(); return; }

    await firstCheck.check();
    const toolbar = page.locator('.cc-bulk-toolbar');
    await expect(toolbar).toHaveClass(/cc-bulk-toolbar--active/);
    await expect(page.locator('#cc-bulk-count')).toContainText(/1 selected/i);
  });

  test('Verb tray renders for at least one queue row (when data present)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter`);
    await page.waitForLoadState('domcontentloaded');

    const rows = page.locator('li.cc-queue-row');
    if ((await rows.count()) === 0) { test.skip(); return; }

    // At least one row should expose a verb (non-Completed/Cancelled rows always have ≥1 legal transition).
    const verbs = page.locator('button.cc-verb');
    expect(await verbs.count()).toBeGreaterThanOrEqual(1);
  });

  test('Exceptions tab renders lane or empty state', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=exceptions`);
    await page.waitForLoadState('domcontentloaded');

    const lane = page.locator('.cc-exception-lane');
    const empty = page.locator('.empty-state');
    const total = (await lane.count()) + (await empty.count());
    expect(total).toBeGreaterThanOrEqual(1);
  });

  test('Activity tab renders feed or empty state', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Production/ControlCenter?tab=activity`);
    await page.waitForLoadState('domcontentloaded');

    const feed = page.locator('ul.cc-activity-feed');
    const empty = page.locator('.empty-state');
    const total = (await feed.count()) + (await empty.count());
    expect(total).toBeGreaterThanOrEqual(1);
  });

});
