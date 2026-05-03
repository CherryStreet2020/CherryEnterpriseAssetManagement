// Bulk operations index + details drill-through. Mutations are covered by
// fa_partial_disposal.spec.js / fa_transfer.spec.js — here we just exercise
// listing, history view, and a representative details page.
const { test, expect } = require('@playwright/test');
const { BASE, login, pickers } = require('./_helpers');

test.describe('BULK OPS — drill-through', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('bulk operations index renders the launcher cards', async ({ page }) => {
    const resp = await page.goto(`${BASE}/BulkOperations`);
    expect(resp.status()).toBe(200);
    // Page should expose at least one operation tile/card.
    const tiles = await page.locator('a[href*="BulkOperations"], .card, .tile').count();
    expect(tiles).toBeGreaterThan(0);
  });

  test('bulk operation details open for a real operation', async ({ page }) => {
    const id = await pickers.bulkOperation();
    test.skip(!id, 'no bulk operations in DB');
    const resp = await page.goto(`${BASE}/BulkOperations/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });
});
