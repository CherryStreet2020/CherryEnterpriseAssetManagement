const { test, expect } = require('@playwright/test');
const {
  login,
  gotoApp,
  dbQuery,
  dbOne,
  pickAsset,
  pickActiveLocationOtherThan,
  pickActiveDepartmentOtherThan,
  pickLookupValueId,
  pickAnyLookupValueId,
} = require('./_helpers');

const NOTES = 'E2E spec transfer';

let ASSET_ID;
let TARGET_LOCATION_ID;
let TARGET_DEPARTMENT_ID;
let TRANSFER_REASON_LV;
let original;
let hasTransferHistory;

test.describe('FA — Transfer', () => {
  test.beforeAll(async () => {
    const a = await pickAsset({ rank: 2, requireLocation: true });
    ASSET_ID = a.Id;
    original = await dbOne(
      'SELECT "Id","LocationId","DepartmentId","Bay","CompanyId","SiteId" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();

    TARGET_LOCATION_ID = await pickActiveLocationOtherThan(original.LocationId, original.CompanyId);
    expect(TARGET_LOCATION_ID, 'a different active location must exist').toBeTruthy();

    // department is optional; resolved from the actual <select> options at runtime
    TARGET_DEPARTMENT_ID = null;

    TRANSFER_REASON_LV =
      (await pickLookupValueId('TransferReason', 'RELOCATION')) ||
      (await pickAnyLookupValueId('TransferReason'));

    const tbl = await dbOne(
      `SELECT to_regclass('public."AssetTransfers"') AS t`
    );
    hasTransferHistory = !!(tbl && tbl.t);
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "LocationId"=$2,"DepartmentId"=$3,"Bay"=$4 WHERE "Id"=$1',
      [ASSET_ID, original.LocationId, original.DepartmentId, original.Bay]
    );
    if (hasTransferHistory) {
      await dbQuery(
        'DELETE FROM "AssetTransfers" WHERE "AssetId"=$1 AND "Notes"=$2',
        [ASSET_ID, NOTES]
      );
    }
  });

  test('transfer updates LocationId, DepartmentId, and creates AssetTransfer history row', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Transfer/${ASSET_ID}`);

    await page.evaluate((id) => {
      const sel = document.querySelector('select[name="NewLocationId"]');
      sel.value = String(id);
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    }, TARGET_LOCATION_ID);

    // Pick a department from the form's actual select options so we never
    // post an Id that isn't authorized for the current tenant scope.
    TARGET_DEPARTMENT_ID = await page.evaluate((excludeId) => {
      const sel = document.querySelector('select[name="NewDepartmentId"]');
      if (!sel) return null;
      const opt = Array.from(sel.options).find(
        (o) => o.value && o.value !== '' && Number(o.value) !== Number(excludeId)
      );
      if (!opt) return null;
      sel.value = opt.value;
      sel.dispatchEvent(new Event('change', { bubbles: true }));
      return Number(opt.value);
    }, original.DepartmentId);

    await page.fill('input[name="NewBay"]', 'BAY-E2E');
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    if (TRANSFER_REASON_LV) {
      await page.evaluate((id) => {
        const sel = document.querySelector('select[name="TransferReasonLookupValueId"]');
        if (sel) {
          sel.value = String(id);
          sel.dispatchEvent(new Event('change', { bubbles: true }));
        }
      }, TRANSFER_REASON_LV);
    }
    await page.fill('textarea[name="Notes"]', NOTES);

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    const errBanner = page.locator('.alert-danger, .text-danger').filter({ hasText: /\S/ });
    if ((await errBanner.count()) > 0) {
      const txt = (await errBanner.allInnerTexts()).join(' | ');
      throw new Error(`Transfer form returned error(s): ${txt}\nURL=${page.url()}`);
    }
    expect(page.url()).toMatch(/\/Assets\/Asset\/\d+/);

    const updated = await dbOne(
      'SELECT "LocationId","DepartmentId","Bay" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(Number(updated.LocationId)).toBe(Number(TARGET_LOCATION_ID));
    if (TARGET_DEPARTMENT_ID) {
      expect(Number(updated.DepartmentId)).toBe(Number(TARGET_DEPARTMENT_ID));
    }
    expect(updated.Bay).toBe('BAY-E2E');

    if (hasTransferHistory) {
      const history = await dbOne(
        'SELECT "Id","FromLocation","ToLocation","Notes" FROM "AssetTransfers" WHERE "AssetId"=$1 AND "Notes"=$2 ORDER BY "Id" DESC LIMIT 1',
        [ASSET_ID, NOTES]
      );
      expect(history, 'transfer history row should exist').toBeTruthy();
      expect(history.ToLocation).toBeTruthy();
    }
  });
});
