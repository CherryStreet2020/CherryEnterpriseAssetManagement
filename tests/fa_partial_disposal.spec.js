const { test, expect } = require('@playwright/test');
const { BASE, login, gotoApp, dbQuery, dbOne } = require('./_helpers');

const ASSET_ID = 39;
const PERCENTAGE = 25;
const SALE_PROCEEDS = 4500;
const REASON_SALE = 0;

let original;

test.describe('FA — Partial Disposal', () => {
  test.beforeAll(async () => {
    original = await dbOne(
      'SELECT "AcquisitionCost","AccumulatedDepreciation" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "AcquisitionCost"=$2,"AccumulatedDepreciation"=$3 WHERE "Id"=$1',
      [ASSET_ID, original.AcquisitionCost, original.AccumulatedDepreciation]
    );
    await dbQuery('DELETE FROM "PartialDisposals" WHERE "AssetId"=$1 AND "Notes"=$2', [
      ASSET_ID,
      'E2E partial disposal spec',
    ]);
  });

  test('partial disposal decrements asset cost and creates PartialDisposal record', async ({ page }) => {
    await login(page);
    await gotoApp(page, '/BulkOperations');

    const html = await page.content();
    const tokenMatch = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    expect(tokenMatch, 'antiforgery token must be present on /BulkOperations').toBeTruthy();
    const token = tokenMatch[1];

    const cookies = await page.context().cookies();
    const cookieHeader = cookies.map((c) => `${c.name}=${c.value}`).join('; ');

    const form = new URLSearchParams();
    form.set('__RequestVerificationToken', token);
    form.set('assetId', String(ASSET_ID));
    form.set('percentage', String(PERCENTAGE));
    form.set('saleProceeds', String(SALE_PROCEEDS));
    form.set('reason', String(REASON_SALE));
    form.set('buyer', 'Spec Buyer');
    form.set('notes', 'E2E partial disposal spec');

    const resp = await page.request.post(
      `${BASE}/BulkOperations?handler=PartialDisposal`,
      {
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          Cookie: cookieHeader,
          RequestVerificationToken: token,
        },
        data: form.toString(),
        maxRedirects: 0,
      }
    );
    expect([200, 302, 303]).toContain(resp.status());

    const updated = await dbOne(
      'SELECT "AcquisitionCost","AccumulatedDepreciation" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    const expectedCost = Number(original.AcquisitionCost) * (1 - PERCENTAGE / 100);
    expect(Math.round(Number(updated.AcquisitionCost) * 100)).toBe(Math.round(expectedCost * 100));

    const disposal = await dbOne(
      'SELECT "Id","PercentageDisposed","SaleProceeds","Notes","Buyer" FROM "PartialDisposals" WHERE "AssetId"=$1 AND "Notes"=$2 ORDER BY "Id" DESC LIMIT 1',
      [ASSET_ID, 'E2E partial disposal spec']
    );
    expect(disposal, 'PartialDisposal record should exist').toBeTruthy();
    expect(Number(disposal.SaleProceeds)).toBe(SALE_PROCEEDS);
    expect(Math.round(Number(disposal.PercentageDisposed) * 10000)).toBe(PERCENTAGE * 100);
    expect((disposal.Buyer || '').toLowerCase()).toBe('spec buyer');

    const listResp = await gotoApp(page, '/Assets');
    expect(listResp.status()).toBeLessThan(400);
  });
});
