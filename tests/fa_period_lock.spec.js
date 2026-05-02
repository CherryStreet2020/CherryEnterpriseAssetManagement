const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne } = require('./_helpers');

const ASSET_ID = 49;

let originalStatus;
let periodId;
let originalAsset;
let maxJeIdBefore;

test.describe('FA — Period Lock', () => {
  test.beforeAll(async () => {
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
    const max = await dbOne('SELECT COALESCE(MAX("Id"),0) AS m FROM "JournalEntries"');
    maxJeIdBefore = Number(max.m);
  });

  test.afterEach(async () => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=$2 WHERE "Id"=$1', [periodId, originalStatus]);
    await dbQuery(
      'UPDATE "Assets" SET "Status"=$2,"DisposalDate"=$3,"DisposalProceeds"=$4,"GainLossOnDisposal"=$5,"Active"=$6 WHERE "Id"=$1',
      [ASSET_ID, originalAsset.Status, originalAsset.DisposalDate, originalAsset.DisposalProceeds, originalAsset.GainLossOnDisposal, originalAsset.Active]
    );
    await dbQuery('DELETE FROM "JournalLines" WHERE "JournalEntryId">$1', [maxJeIdBefore]);
    await dbQuery('DELETE FROM "JournalEntries" WHERE "Id">$1', [maxJeIdBefore]);
  });

  test('locked period blocks disposal posting and shows the period guard error', async ({ page }) => {
    await dbQuery('UPDATE "FiscalPeriods" SET "Status"=2 WHERE "Id"=$1', [periodId]);

    await login(page);
    await gotoApp(page, `/Assets/Dispose/${ASSET_ID}`);

    await page.fill('input[name="DisposalDate"]', '2026-05-15');
    await page.fill('input[name="Proceeds"]', '1000');

    await page.evaluate(() => {
      const sel = document.querySelector('select[name="DisposalReasonLookupValueId"]');
      const opt = sel.querySelector('option[value]:not([value=""])');
      if (opt) {
        sel.value = opt.value;
        sel.dispatchEvent(new Event('change', { bubbles: true }));
      }
      const form = document.querySelector('form[method="post"]');
      const ensure = (name, value) => {
        let el = form.querySelector(`[name="${name}"]`);
        if (!el) {
          el = document.createElement('input');
          el.type = 'hidden';
          el.name = name;
          form.appendChild(el);
        }
        el.value = value;
      };
      ensure('DisposalType', 'Sale');
    });

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    expect(page.url()).toContain(`/Assets/Dispose/${ASSET_ID}`);
    const body = await page.locator('body').innerText();
    expect(body).toMatch(/Locked|Closed|period/i);
    expect(body).not.toMatch(/Server Error|HTTP 500/i);

    const after = await dbOne('SELECT "Status" FROM "Assets" WHERE "Id"=$1', [ASSET_ID]);
    expect(Number(after.Status)).toBe(0);
  });
});
