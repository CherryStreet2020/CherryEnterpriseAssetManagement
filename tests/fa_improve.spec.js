const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne } = require('./_helpers');

const ASSET_ID = 19;
const COST = 7500;
const LIFE_EXTENSION = 12;

let original;

test.describe('FA — Improve', () => {
  test.beforeAll(async () => {
    original = await dbOne(
      'SELECT "AcquisitionCost","UsefulLifeMonths" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "AcquisitionCost"=$2,"UsefulLifeMonths"=$3 WHERE "Id"=$1',
      [ASSET_ID, original.AcquisitionCost, original.UsefulLifeMonths]
    );
    await dbQuery(
      'DELETE FROM "CapitalImprovements" WHERE "AssetId"=$1 AND "Cost"=$2 AND "InvoiceNumber"=$3',
      [ASSET_ID, COST, 'TEST-FA-IMPROVE']
    );
  });

  test('capital improvement increases cost, extends life, creates audit row', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);

    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'E2E spec — spindle motor upgrade');
    await page.fill('input[name="Cost"]', String(COST));
    await page.fill('input[name="Vendor"]', 'Spec Vendor');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE');
    await page.fill('input[name="UsefulLifeExtension"]', String(LIFE_EXTENSION));

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    const errBanner = page.locator('.alert-danger, .text-danger').filter({ hasText: /\S/ });
    if ((await errBanner.count()) > 0) {
      const txt = (await errBanner.allInnerTexts()).join(' | ');
      throw new Error(`Improve form returned error(s): ${txt}\nURL=${page.url()}`);
    }
    expect(page.url()).toMatch(/\/Assets\/Asset\/\d+/);

    const updated = await dbOne(
      'SELECT "AcquisitionCost","UsefulLifeMonths" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(Math.round(Number(updated.AcquisitionCost) * 100)).toBe(
      Math.round((Number(original.AcquisitionCost) + COST) * 100)
    );
    expect(Number(updated.UsefulLifeMonths)).toBe(Number(original.UsefulLifeMonths) + LIFE_EXTENSION);

    const audit = await dbOne(
      'SELECT "Id","Cost","Description","UsefulLifeExtensionMonths","Capitalized" FROM "CapitalImprovements" WHERE "AssetId"=$1 AND "InvoiceNumber"=$2',
      [ASSET_ID, 'TEST-FA-IMPROVE']
    );
    expect(audit, 'capital improvement audit row should exist').toBeTruthy();
    expect(Number(audit.Cost)).toBe(COST);
    expect(Number(audit.UsefulLifeExtensionMonths)).toBe(LIFE_EXTENSION);
    expect(audit.Capitalized).toBe(true);
  });
});
