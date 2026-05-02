const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne } = require('./_helpers');

const ASSET_ID = 59;

let originalDescription;

async function openEditPage(page) {
  await login(page);
  await gotoApp(page, `/Assets/Asset/${ASSET_ID}?mode=edit`);
  await expect(page.locator('input[name="Asset.Description"]')).toBeVisible();
}

test.describe('FA — Asset Concurrency', () => {
  test.beforeAll(async () => {
    const a = await dbOne('SELECT "Description" FROM "Assets" WHERE "Id"=$1', [ASSET_ID]);
    expect(a).toBeTruthy();
    originalDescription = a.Description;
  });

  test.afterEach(async () => {
    await dbQuery('UPDATE "Assets" SET "Description"=$2 WHERE "Id"=$1', [ASSET_ID, originalDescription]);
  });

  test('second save sees conflict banner; first edit is preserved in DB', async ({ browser }) => {
    const ctxA = await browser.newContext();
    const ctxB = await browser.newContext();
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();

    try {
      await openEditPage(pageA);
      await openEditPage(pageB);

      const winningDescription = `${originalDescription} — A wins ${Date.now()}`;
      await pageA.fill('input[name="Asset.Description"]', winningDescription);
      await Promise.all([
        pageA.waitForLoadState('domcontentloaded'),
        pageA.click('button[type="submit"]'),
      ]);

      const losingDescription = `${originalDescription} — B should fail`;
      await pageB.fill('input[name="Asset.Description"]', losingDescription);
      await pageB.click('button[type="submit"]');
      await pageB.waitForLoadState('domcontentloaded');

      await expect(pageB.locator('[data-testid="asset-concurrency-banner"]')).toBeVisible();

      const after = await dbOne('SELECT "Description" FROM "Assets" WHERE "Id"=$1', [ASSET_ID]);
      expect(after.Description).toBe(winningDescription);
    } finally {
      await ctxA.close();
      await ctxB.close();
    }
  });
});
