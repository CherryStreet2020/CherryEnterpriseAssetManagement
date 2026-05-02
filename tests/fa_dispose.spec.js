const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne, pickAsset, pickLookupValueId, pickAnyLookupValueId } = require('./_helpers');

const PROCEEDS = 25000;
const DISPOSAL_DATE = '2026-05-15';

let ASSET_ID;
let DISPOSAL_REASON_LV_SALE;
let original;
let maxJeIdBefore;

test.describe('FA — Dispose', () => {
  test.beforeAll(async () => {
    original = await pickAsset({ rank: 0 });
    ASSET_ID = original.Id;
    DISPOSAL_REASON_LV_SALE =
      (await pickLookupValueId('DisposalReason', 'SALE')) ||
      (await pickAnyLookupValueId('DisposalReason'));
    expect(DISPOSAL_REASON_LV_SALE, 'a DisposalReason lookup value must exist').toBeTruthy();
    const max = await dbOne('SELECT COALESCE(MAX("Id"),0) AS m FROM "JournalEntries"');
    maxJeIdBefore = Number(max.m);
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "Status"=$2,"DisposalDate"=$3,"DisposalProceeds"=$4,"GainLossOnDisposal"=$5,"Active"=$6 WHERE "Id"=$1',
      [ASSET_ID, original.Status, original.DisposalDate, original.DisposalProceeds, original.GainLossOnDisposal, original.Active]
    );
    await dbQuery(
      'DELETE FROM "JournalLines" WHERE "JournalEntryId" IN (SELECT "Id" FROM "JournalEntries" WHERE "Id">$1 AND "Reference"=$2 AND LOWER("Source")=\'disposal\')',
      [maxJeIdBefore, original.AssetNumber]
    );
    await dbQuery(
      'DELETE FROM "JournalEntries" WHERE "Id">$1 AND "Reference"=$2 AND LOWER("Source")=\'disposal\'',
      [maxJeIdBefore, original.AssetNumber]
    );
  });

  test('disposal records status=Disposed, journal entry, and gain/loss', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Dispose/${ASSET_ID}`);

    await expect(page.locator('input[name="DisposalDate"]')).toBeVisible();
    await page.fill('input[name="DisposalDate"]', DISPOSAL_DATE);
    await page.fill('input[name="Proceeds"]', String(PROCEEDS));

    await page.evaluate((id) => {
      const sel = document.querySelector('select[name="DisposalReasonLookupValueId"]');
      sel.value = String(id);
      sel.dispatchEvent(new Event('change', { bubbles: true }));
    }, DISPOSAL_REASON_LV_SALE);

    const bookOption = await page
      .locator('select[name="BookId"] option')
      .filter({ hasNot: page.locator('option[value=""]') })
      .first()
      .getAttribute('value');

    await page.evaluate(() => {
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
      const cje = form.querySelector('input[type="checkbox"][name="CreateJournalEntry"]');
      if (cje) cje.checked = true;
    });

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    if (!/\/Assets\/Asset\/\d+/.test(page.url())) {
      const formDump = await page.evaluate(() => {
        const out = {};
        document.querySelectorAll('form input, form select, form textarea').forEach((el) => {
          if (!el.name) return;
          out[el.name] = el.value;
        });
        const errs = [];
        document.querySelectorAll('.text-danger, .alert-danger, .field-validation-error, .validation-summary-errors').forEach((el) => {
          const t = (el.textContent || '').trim();
          if (t) errs.push(t);
        });
        return { values: out, errs };
      });
      const bodySnippet = (await page.locator('body').innerText()).slice(0, 2000);
      throw new Error(
        `Dispose form did not redirect.\nURL=${page.url()}\n${JSON.stringify(formDump, null, 2)}\nBODY:\n${bodySnippet}`
      );
    }

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
