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

const PROCEEDS = 25000;
const DISPOSAL_DATE = '2026-05-15';

let ASSET_ID;
let DISPOSAL_REASON_LV_SALE;
let original;
let createdJeIds = [];

test.describe('FA — Dispose', () => {
  test.beforeAll(async () => {
    original = await pickAsset({ rank: 0 });
    ASSET_ID = original.Id;
    DISPOSAL_REASON_LV_SALE =
      (await pickLookupValueId('DisposalReason', 'SALE')) ||
      (await pickAnyLookupValueId('DisposalReason'));
    expect(DISPOSAL_REASON_LV_SALE, 'a DisposalReason lookup value must exist').toBeTruthy();
  });

  test.afterEach(async () => {
    // Restore the asset's exact pre-test state.
    await dbQuery(
      'UPDATE "Assets" SET "Status"=$2,"DisposalDate"=$3,"DisposalProceeds"=$4,"GainLossOnDisposal"=$5,"Active"=$6 WHERE "Id"=$1',
      [ASSET_ID, original.Status, original.DisposalDate, original.DisposalProceeds, original.GainLossOnDisposal, original.Active]
    );
    // Delete journal entries this spec produced. We match by tracked
    // Ids first, then by stable markers (source=Disposal + Reference =
    // this spec's asset number) so cleanup still runs even if the test
    // failed after the POST but before we captured the JE Id.
    const ids = await dbQuery(
      `SELECT "Id" FROM "JournalEntries"
        WHERE LOWER("Source")='disposal' AND "Reference"=$1
           OR "Id" = ANY($2::int[])`,
      [original.AssetNumber, createdJeIds.length ? createdJeIds : [0]]
    );
    const allIds = ids.map((r) => r.Id);
    if (allIds.length) {
      await dbQuery('DELETE FROM "JournalLines" WHERE "JournalEntryId" = ANY($1::int[])', [allIds]);
      await dbQuery('DELETE FROM "JournalEntries" WHERE "Id" = ANY($1::int[])', [allIds]);
    }
    createdJeIds = [];
  });

  test('disposal records status=Disposed, journal entry, and gain/loss', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Dispose/${ASSET_ID}`);

    await expect(page.locator('input[name="DisposalDate"]')).toBeVisible();
    await page.fill('input[name="DisposalDate"]', DISPOSAL_DATE);
    await page.fill('input[name="Proceeds"]', String(PROCEEDS));

    // Real <select> interaction. The Dispose page resolves DisposalType
    // server-side from this lookup value's Code, so picking the value
    // here is sufficient — no hidden inputs are injected.
    await page.selectOption(
      'select[name="DisposalReasonLookupValueId"]',
      String(DISPOSAL_REASON_LV_SALE)
    );

    // Capture the chosen Book so we can assert it on the JE later.
    const bookOption = await page
      .locator('select[name="BookId"] option')
      .first()
      .getAttribute('value');

    // Ensure the "Create journal entry" checkbox is checked (it ships
    // checked, but check explicitly to match the user-visible state).
    const createJe = page.locator('#createJournal');
    if ((await createJe.count()) > 0 && !(await createJe.isChecked())) {
      await createJe.check();
    }

    await Promise.all([
      page.waitForURL(/\/Assets\/Asset\/\d+/),
      page.click('button[type="submit"]'),
    ]);

    const updated = await dbOne(
      'SELECT "Status","DisposalDate","DisposalProceeds","GainLossOnDisposal","Active","AcquisitionCost","AccumulatedDepreciation" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(Number(updated.Status)).toBe(2);
    expect(Number(updated.DisposalProceeds)).toBe(PROCEEDS);
    expect(updated.Active).toBe(false);

    const expectedBookValue = Number(original.AcquisitionCost) - Number(original.AccumulatedDepreciation);
    const expectedGainLoss = PROCEEDS - expectedBookValue;
    expect(Math.round(Number(updated.GainLossOnDisposal) * 100)).toBe(Math.round(expectedGainLoss * 100));

    const je = await dbOne(
      'SELECT "Id","Batch","Source","Reference","BookId" FROM "JournalEntries" WHERE LOWER("Source")=\'disposal\' AND "Reference"=$1 ORDER BY "Id" DESC LIMIT 1',
      [original.AssetNumber]
    );
    expect(je, 'disposal journal entry should exist').toBeTruthy();
    createdJeIds.push(je.Id);
    if (bookOption) expect(String(je.BookId)).toBe(bookOption);

    const lines = await dbQuery(
      'SELECT "Debit","Credit" FROM "JournalLines" WHERE "JournalEntryId"=$1',
      [je.Id]
    );
    expect(lines.length).toBeGreaterThanOrEqual(2);
    const dr = lines.reduce((s, l) => s + Number(l.Debit), 0);
    const cr = lines.reduce((s, l) => s + Number(l.Credit), 0);
    expect(Math.round(dr * 100)).toBe(Math.round(cr * 100));
  });

  test('second disposal on already-disposed asset shows error', async ({ page }) => {
    await dbQuery(
      'UPDATE "Assets" SET "Status"=2,"DisposalDate"=NOW(),"DisposalProceeds"=1,"Active"=false WHERE "Id"=$1',
      [ASSET_ID]
    );

    await login(page);
    await gotoApp(page, `/Assets/Dispose/${ASSET_ID}`);

    await expect(page.locator('.alert-danger, .alert.alert-danger')).toContainText(/already been disposed/i);
  });
});
