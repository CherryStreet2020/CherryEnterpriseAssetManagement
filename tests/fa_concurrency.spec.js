const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne, pickAsset } = require('./_helpers');

let ASSET_ID;
let originalDescription;

async function openEditPage(page) {
  await login(page);
  await gotoApp(page, `/Assets/Asset/${ASSET_ID}?mode=edit`);
  await expect(page.locator('input[name="Asset.Description"]')).toBeVisible();
}

test.describe('FA — Asset Concurrency', () => {
  test.beforeAll(async () => {
    const a = await pickAsset({ rank: 5 });
    ASSET_ID = a.Id;
    const row = await dbOne('SELECT "Description" FROM "Assets" WHERE "Id"=$1', [ASSET_ID]);
    expect(row).toBeTruthy();
    originalDescription = row.Description;
  });

  test.afterEach(async () => {
    await dbQuery('UPDATE "Assets" SET "Description"=$2 WHERE "Id"=$1', [ASSET_ID, originalDescription]);
  });

  test('second save sees conflict banner; first edit is preserved in DB', async ({ browser }) => {
    test.setTimeout(120000);
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
      // Both contexts share the default Playwright artifacts directory
      // (.playwright-artifacts/.../*.network, *.trace). When ctxA.close()
      // and ctxB.close() run concurrently, Playwright occasionally hits an
      // ENOENT race tearing down trace/network files — every in-test
      // assertion has already passed, but teardown throws and the test
      // is reported as flaky.
      //
      // Close both contexts via Promise.allSettled so neither blocks the
      // other, then swallow ONLY ENOENT errors whose path matches the
      // known artifact patterns. Anything else is rethrown.
      const results = await Promise.allSettled([ctxA.close(), ctxB.close()]);
      const TEARDOWN_RACE_PATH = /\.playwright-artifacts\b.*\.(network|trace)$/;
      for (const r of results) {
        if (r.status !== 'rejected') continue;
        const err = r.reason;
        const isEnoent = err && (err.code === 'ENOENT' || /ENOENT/.test(err.message || ''));
        const isArtifactPath = err && TEARDOWN_RACE_PATH.test(err.path || err.message || '');
        if (isEnoent && isArtifactPath) {
          console.warn(`[fa_concurrency] swallowed Playwright artifact-cleanup race: ${err.message}`);
          continue;
        }
        throw err;
      }
    }
  });
});
