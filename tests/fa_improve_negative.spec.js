const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne, pickAsset } = require('./_helpers');

let ASSET_ID;
let original;
let originalImprovementCount;
let periodId;
let originalPeriodStatus;

async function snapshotImprovementCount(assetId) {
  const row = await dbOne(
    'SELECT COUNT(*)::int AS n FROM "CapitalImprovements" WHERE "AssetId"=$1',
    [assetId]
  );
  return Number(row.n);
}

async function bypassClientValidation(page) {
  await page.evaluate(() => {
    const form = document.querySelector('form');
    if (form) form.setAttribute('novalidate', 'novalidate');
    document.querySelectorAll('input, textarea, select').forEach((el) => {
      el.removeAttribute('required');
      el.removeAttribute('min');
    });
  });
}

async function assertNoMutation() {
  const after = await dbOne(
    'SELECT "AcquisitionCost","UsefulLifeMonths" FROM "Assets" WHERE "Id"=$1',
    [ASSET_ID]
  );
  expect(Number(after.AcquisitionCost)).toBe(Number(original.AcquisitionCost));
  expect(Number(after.UsefulLifeMonths)).toBe(Number(original.UsefulLifeMonths));
  const count = await snapshotImprovementCount(ASSET_ID);
  expect(count).toBe(originalImprovementCount);
}

async function assertRejected(page) {
  expect(page.url()).toContain(`/Assets/Improve/${ASSET_ID}`);
  expect(page.url()).not.toMatch(/\/Assets\/Asset\/\d+/);
  await expect(page.locator('.alert-danger, .text-danger').first()).toBeVisible();
  await assertNoMutation();
}

test.describe('FA — Improve (negative paths)', () => {
  test.beforeAll(async () => {
    const a = await pickAsset({ rank: 3 });
    ASSET_ID = a.Id;
    original = await dbOne(
      'SELECT "CompanyId","AcquisitionCost","UsefulLifeMonths" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();
    originalImprovementCount = await snapshotImprovementCount(ASSET_ID);

    const period = await dbOne(
      'SELECT "Id","Status" FROM "FiscalPeriods" WHERE "CompanyId"=$1 AND "StartDate" <= $2::timestamptz AND "EndDate" >= $2::timestamptz LIMIT 1',
      [original.CompanyId, '2026-05-15']
    );
    expect(period, 'fiscal period covering improvement date must exist').toBeTruthy();
    periodId = period.Id;
    originalPeriodStatus = period.Status;
  });

  test.afterEach(async () => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=$2 WHERE "Id"=$1', [periodId, originalPeriodStatus]);
    await dbQuery(
      'UPDATE "Assets" SET "AcquisitionCost"=$2,"UsefulLifeMonths"=$3 WHERE "Id"=$1',
      [ASSET_ID, original.AcquisitionCost, original.UsefulLifeMonths]
    );
    await dbQuery(
      'DELETE FROM "CapitalImprovements" WHERE "AssetId"=$1 AND "InvoiceNumber" LIKE $2',
      [ASSET_ID, 'TEST-FA-IMPROVE-NEG-%']
    );
  });

  test('zero cost is rejected with "greater than 0" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'NEG zero cost');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-ZERO');
    await bypassClientValidation(page);
    await page.fill('input[name="Cost"]', '0');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/greater than 0/i);
    await assertRejected(page);
  });

  test('negative cost is rejected with "greater than 0" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'NEG negative cost');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-NEG');
    await bypassClientValidation(page);
    await page.fill('input[name="Cost"]', '-500');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/greater than 0/i);
    await assertRejected(page);
  });

  test('missing description is rejected with "required" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await bypassClientValidation(page);
    await page.fill('input[name="Description"]', '');
    await page.fill('input[name="Cost"]', '1000');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-NODESC');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/Description.*required/i);
    await assertRejected(page);
  });

  test('zero useful-life extension is rejected with "at least 1 month" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'NEG zero life ext');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-LIFE0');
    await page.fill('input[name="Cost"]', '1000');
    await bypassClientValidation(page);
    await page.fill('input[name="UsefulLifeExtension"]', '0');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/at least 1 month/i);
    await assertRejected(page);
  });

  test('negative useful-life extension is rejected with "at least 1 month" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'NEG negative life ext');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-LIFENEG');
    await page.fill('input[name="Cost"]', '1000');
    await bypassClientValidation(page);
    await page.fill('input[name="UsefulLifeExtension"]', '-5');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/at least 1 month/i);
    await assertRejected(page);
  });

  test('locked fiscal period blocks improvement and shows period guard error', async ({ page }) => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=2 WHERE "Id"=$1', [periodId]);

    await login(page);
    await gotoApp(page, `/Assets/Improve/${ASSET_ID}`);
    await page.fill('input[name="ImprovementDate"]', '2026-05-15');
    await page.fill('input[name="Description"]', 'NEG locked period');
    await page.fill('input[name="InvoiceNumber"]', 'TEST-FA-IMPROVE-NEG-LOCKED');
    await page.fill('input[name="Cost"]', '1000');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/Locked|Closed|period/i);
    await assertRejected(page);
  });
});
