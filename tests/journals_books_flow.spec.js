// Books, GL accounts, journal entries and the journal-generation surface.
const { test, expect } = require('@playwright/test');
const { BASE, login, pickers } = require('./_helpers');

test.describe('JOURNALS / BOOKS — drill-through', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('books index lists books', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Books`);
    expect(resp.status()).toBe(200);
    expect(await page.locator('table').count()).toBeGreaterThan(0);
  });

  test('book details/edit/gl-accounts open for a real book', async ({ page }) => {
    const id = await pickers.book();
    test.skip(!id, 'no books in DB');
    for (const path of [`/Books/Details/${id}`, `/Books/Edit/${id}`, `/Books/GlAccounts/${id}`]) {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp.status(), `${path} returned ${resp.status()}`).toBeLessThan(500);
    }
  });

  test('journals index renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Journals`);
    expect(resp.status()).toBe(200);
  });

  test('journal entry details open', async ({ page }) => {
    const id = await pickers.journalEntry();
    test.skip(!id, 'no journal entries in DB');
    const resp = await page.goto(`${BASE}/Journals/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('journals generate page renders the form', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Journals/Generate`);
    expect(resp.status()).toBe(200);
    expect(await page.locator('form').count()).toBeGreaterThan(0);
  });

  test('chart of accounts report renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Reports/ChartOfAccounts`);
    expect(resp.status()).toBe(200);
  });
});
