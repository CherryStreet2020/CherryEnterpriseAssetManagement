const { test, expect } = require('@playwright/test');
const {
  login,
  gotoApp,
  dbQuery,
  dbOne,
  pickAsset,
  pickLookupValueId,
  pickAnyLookupValueId,
} = require('./_helpers');

// NOTE: There is no Admin Periods UI for locking a fiscal period in the
// current build, so this test sets `FiscalPeriods.Status = 2` (Locked)
// directly via SQL, exercises the disposal page (which is guarded by
// IPeriodGuard), and asserts the user-visible guard error. Original
// period status is restored in afterEach. Because the guard rejects the
// post before any JE is created, no JournalEntry/JournalLine rows need
// cleanup.

let ASSET_ID;
let originalAsset;
let periodId;
let originalStatus;
let DISPOSAL_REASON_LV;

test.describe('FA — Period Lock', () => {
  test.beforeAll(async () => {
    const a = await pickAsset({ rank: 4 });
    ASSET_ID = a.Id;
    originalAsset = await dbOne(
      'SELECT "CompanyId","Status","DisposalDate","DisposalProceeds","GainLossOnDisposal","Active" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(originalAsset).toBeTruthy();
    const period = await dbOne(
      'SELECT "Id","Status" FROM "FiscalPeriods" WHERE "CompanyId"=$1 AND "StartDate" <= $2::timestamptz AND "EndDate" >= $2::timestamptz LIMIT 1',
      [originalAsset.CompanyId, '2026-05-15']
    );
    expect(period, 'fiscal period covering disposal date must exist').toBeTruthy();
    periodId = period.Id;
    originalStatus = period.Status;

    DISPOSAL_REASON_LV =
      (await pickLookupValueId('DisposalReason', 'SALE')) ||
      (await pickAnyLookupValueId('DisposalReason'));
    expect(DISPOSAL_REASON_LV, 'a DisposalReason lookup value must exist').toBeTruthy();
  });

  test.afterEach(async () => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=$2 WHERE "Id"=$1', [periodId, originalStatus]);
    await dbQuery(
      'UPDATE "Assets" SET "Status"=$2,"DisposalDate"=$3,"DisposalProceeds"=$4,"GainLossOnDisposal"=$5,"Active"=$6 WHERE "Id"=$1',
      [ASSET_ID, originalAsset.Status, originalAsset.DisposalDate, originalAsset.DisposalProceeds, originalAsset.GainLossOnDisposal, originalAsset.Active]
    );
  });

  test('locked period blocks disposal posting and shows the period guard error', async ({ page }) => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=2 WHERE "Id"=$1', [periodId]);

    await login(page);
    await gotoApp(page, `/Assets/Dispose/${ASSET_ID}`);

    await page.fill('input[name="DisposalDate"]', '2026-05-15');
    await page.fill('input[name="Proceeds"]', '1000');
    await page.selectOption(
      'select[name="DisposalReasonLookupValueId"]',
      String(DISPOSAL_REASON_LV)
    );

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    expect(page.url()).toContain(`/Assets/Dispose/${ASSET_ID}`);
    const body = await page.locator('body').innerText();
    expect(body).toMatch(/Locked|Closed|period/i);
    expect(body).not.toMatch(/Server Error|HTTP 500/i);

    const after = await dbOne('SELECT "Status" FROM "Assets" WHERE "Id"=$1', [ASSET_ID]);
    expect(Number(after.Status)).toBe(Number(originalAsset.Status));
  });
});
