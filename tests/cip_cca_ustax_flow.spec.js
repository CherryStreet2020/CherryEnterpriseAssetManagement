// Tax/Cap modules: CIP project lifecycle, CCA Canadian classes, US tax engine.
const { test, expect } = require('@playwright/test');
const { BASE, login, dbOne, pickers } = require('./_helpers');

test.describe('CIP / CCA / US TAX — drill-through', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('CIP index lists projects', async ({ page }) => {
    const resp = await page.goto(`${BASE}/CIP`);
    expect(resp.status()).toBe(200);
  });

  test('CIP project details + costs open', async ({ page }) => {
    const id = await pickers.cipProject();
    test.skip(!id, 'no CIP projects in DB');
    const resp = await page.goto(`${BASE}/CIP/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('CIP cost details open for a real cost row', async ({ page }) => {
    const cost = await dbOne('SELECT "Id","CipProjectId" FROM "CipCosts" ORDER BY "Id" LIMIT 1');
    test.skip(!cost, 'no CIP costs in DB');
    const resp = await page.goto(`${BASE}/CIP/CostDetails/${cost.CipProjectId}/${cost.Id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('CCA index renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/CCA`);
    expect(resp.status()).toBe(200);
  });

  test('CCA class report renders for a real class', async ({ page }) => {
    const cls = await dbOne('SELECT "ClassNumber" FROM "CcaClasses" ORDER BY "Id" LIMIT 1');
    if (cls && cls.ClassNumber) {
      const resp = await page.goto(`${BASE}/CCA/ClassReport?classNumber=${cls.ClassNumber}`);
      expect(resp.status()).toBe(200);
    } else {
      const resp = await page.goto(`${BASE}/CCA/ClassReport`);
      expect(resp.status()).toBe(200);
    }
  });

  test('CCA backfill admin page renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Admin/CcaBackfill`);
    expect(resp.status()).toBe(200);
  });

  test('US tax index renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/UsTax`);
    expect(resp.status()).toBe(200);
  });

  test('Form 4562 report renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Reports/Form4562`);
    expect(resp.status()).toBe(200);
  });

  test('T2 Schedule 8 report renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Reports/T2Schedule8`);
    expect(resp.status()).toBe(200);
  });
});
