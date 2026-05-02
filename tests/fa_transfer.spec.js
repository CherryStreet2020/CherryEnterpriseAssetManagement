const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne } = require('./_helpers');

const ASSET_ID = 29;
const TARGET_LOCATION_ID = 13;
const TRANSFER_REASON_LV_RELOCATION = 13;

let original;

test.describe('FA — Transfer', () => {
  test.beforeAll(async () => {
    original = await dbOne(
      'SELECT "Id","LocationId","DepartmentId","Bay","CompanyId","SiteId" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();
    expect(original.LocationId).not.toBe(TARGET_LOCATION_ID);

    const loc = await dbOne(
      'SELECT "Id" FROM "Locations" WHERE "Id"=$1 AND "IsActive"=true',
      [TARGET_LOCATION_ID]
    );
    expect(loc, `target location ${TARGET_LOCATION_ID} must exist and be active`).toBeTruthy();
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "LocationId"=$2,"DepartmentId"=$3,"Bay"=$4 WHERE "Id"=$1',
      [ASSET_ID, original.LocationId, original.DepartmentId, original.Bay]
    );
    await dbQuery(
      'DELETE FROM "AssetTransfers" WHERE "AssetId"=$1 AND "Notes"=$2',
      [ASSET_ID, 'E2E spec transfer']
    );
  });

  test('transfer updates LocationId and creates AssetTransfer history row', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Transfer/${ASSET_ID}`);

    await page.evaluate((id) => {
      const sel = document.querySelector('select[name="NewLocationId"]');
      sel.value = String(id);
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    }, TARGET_LOCATION_ID);

    await page.fill('input[name="NewBay"]', 'BAY-E2E');
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    await page.evaluate((id) => {
      const sel = document.querySelector('select[name="TransferReasonLookupValueId"]');
      if (sel) {
        sel.value = String(id);
        sel.dispatchEvent(new Event('change', { bubbles: true }));
      }
    }, TRANSFER_REASON_LV_RELOCATION);
    await page.fill('textarea[name="Notes"]', 'E2E spec transfer');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    const errBanner = page.locator('.alert-danger, .text-danger').filter({ hasText: /\S/ });
    if ((await errBanner.count()) > 0) {
      const txt = (await errBanner.allInnerTexts()).join(' | ');
      throw new Error(`Transfer form returned error(s): ${txt}\nURL=${page.url()}`);
    }
    expect(page.url()).toMatch(/\/Assets\/Asset\/\d+/);

    const updated = await dbOne(
      'SELECT "LocationId","Bay" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(Number(updated.LocationId)).toBe(TARGET_LOCATION_ID);
    expect(updated.Bay).toBe('BAY-E2E');

    const history = await dbOne(
      'SELECT "Id","FromLocation","ToLocation","Notes" FROM "AssetTransfers" WHERE "AssetId"=$1 AND "Notes"=$2 ORDER BY "Id" DESC LIMIT 1',
      [ASSET_ID, 'E2E spec transfer']
    );
    expect(history, 'transfer history row should exist').toBeTruthy();
    expect(history.ToLocation).toBeTruthy();
  });
});
