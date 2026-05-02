const { test, expect } = require('@playwright/test');
const {
  login,
  gotoApp,
  dbQuery,
  dbOne,
  pickAsset,
  pickActiveLocationOtherThan,
  pickLookupValueId,
  pickAnyLookupValueId,
} = require('./_helpers');

const NOTES = 'E2E spec transfer';

let ASSET_ID;
let TARGET_LOCATION_ID;
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

    TRANSFER_REASON_LV =
      (await pickLookupValueId('TransferReason', 'RELOCATION')) ||
      (await pickAnyLookupValueId('TransferReason'));

    const tbl = await dbOne(`SELECT to_regclass('public."AssetTransfers"') AS t`);
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

    // Real <select> interaction (the cherry-select widget is layered on
    // top of these native selects; selectOption posts the underlying
    // value).
    await page.selectOption('select[name="NewLocationId"]', String(TARGET_LOCATION_ID));

    // Department is optional; pick the first non-original option that the
    // form actually offers (so we never post an Id outside the user's
    // tenant scope).
    const deptValues = await page.$$eval(
      'select[name="NewDepartmentId"] option',
      (opts) => opts.map((o) => o.value).filter((v) => v && v !== '')
    );
    const targetDeptValue = deptValues.find(
      (v) => Number(v) !== Number(original.DepartmentId)
    );
    let TARGET_DEPARTMENT_ID = null;
    if (targetDeptValue) {
      await page.selectOption('select[name="NewDepartmentId"]', targetDeptValue);
      TARGET_DEPARTMENT_ID = Number(targetDeptValue);
    }

    await page.fill('input[name="NewBay"]', 'BAY-E2E');
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    if (TRANSFER_REASON_LV) {
      await page.selectOption(
        'select[name="TransferReasonLookupValueId"]',
        String(TRANSFER_REASON_LV)
      );
    }
    await page.fill('textarea[name="Notes"]', NOTES);

    await Promise.all([
      page.waitForURL(/\/Assets\/Asset\/\d+/),
      page.click('button[type="submit"]'),
    ]);

    const updated = await dbOne(
      'SELECT "LocationId","DepartmentId","Bay" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(Number(updated.LocationId)).toBe(Number(TARGET_LOCATION_ID));
    if (TARGET_DEPARTMENT_ID) {
      expect(Number(updated.DepartmentId)).toBe(TARGET_DEPARTMENT_ID);
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
