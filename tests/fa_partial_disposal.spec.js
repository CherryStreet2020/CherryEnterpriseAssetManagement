const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne, pickAsset } = require('./_helpers');

const PERCENTAGE = 25;
const SALE_PROCEEDS = 4500;
const NOTES = 'E2E partial disposal spec';

let ASSET_ID;
let original;
let createdChildIds = [];
let createdJournalEntryIds = [];

test.describe('FA — Partial Disposal', () => {
  // ASSET_ID/original are picked at the start of the test from the
  // dropdown options the page actually renders for the logged-in user
  // (so we never select an asset that isn't in the user's tenant scope).

  test.afterEach(async () => {
    if (!ASSET_ID || !original) return;
    // Restore parent asset to its pre-test column values.
    await dbQuery(
      'UPDATE "Assets" SET "AcquisitionCost"=$2,"AccumulatedDepreciation"=$3 WHERE "Id"=$1',
      [ASSET_ID, original.AcquisitionCost, original.AccumulatedDepreciation]
    );
    // Remove rows this spec produced. Match by stable markers (Notes
    // tag for PartialDisposals, ParentAssetId + the `-PD` AssetNumber
    // suffix for child Assets) so cleanup still runs even if the test
    // failed before we captured the child Id.
    // Match by ParentAssetId only — the service truncates the parent
    // prefix when building child asset numbers, so a LIKE on the
    // original AssetNumber would miss truncated children.
    await dbQuery(
      'DELETE FROM "Assets" WHERE "ParentAssetId"=$1',
      [ASSET_ID]
    );
    if (createdChildIds.length) {
      await dbQuery('DELETE FROM "Assets" WHERE "Id" = ANY($1::int[])', [createdChildIds]);
      createdChildIds = [];
    }
    await dbQuery('DELETE FROM "PartialDisposals" WHERE "AssetId"=$1 AND "Notes"=$2', [
      ASSET_ID,
      NOTES,
    ]);
    if (createdJournalEntryIds.length) {
      await dbQuery('DELETE FROM "JournalLines" WHERE "JournalEntryId" = ANY($1::int[])', [createdJournalEntryIds]);
      await dbQuery('DELETE FROM "JournalEntries" WHERE "Id" = ANY($1::int[])', [createdJournalEntryIds]);
      createdJournalEntryIds = [];
    }
  });

  test('partial disposal decrements parent cost, creates child asset, both visible in /Assets', async ({ page }) => {
    await login(page);
    await gotoApp(page, '/BulkOperations');

    // Drive the actual UI: click the "Partial Disposal" action card to
    // open its inline form panel.
    await page.locator('.action-card', { hasText: 'Partial Disposal' }).first().click();
    const panel = page.locator('div#partialDisposalForm.inline-form-panel');
    await expect(panel).toBeVisible();

    // Open the searchable asset dropdown. The page's outside-click
    // handler (document-level) toggles dropdowns closed when a click
    // bubbles outside .searchable-select; we wait for the `show` class
    // to be present so we know the dropdown actually opened.
    const dropdown = page.locator('#disposalDropdown');
    const trigger = panel.locator('.searchable-select-trigger');
    await trigger.click();
    if (!(await dropdown.evaluate((el) => el.classList.contains('show')))) {
      await trigger.click();
    }
    await expect(dropdown).toHaveClass(/show/);
    const searchInput = page.locator('#disposalSearchInput');
    await expect(searchInput).toBeVisible();

    // Pick a candidate from the dropdown options the page actually
    // rendered for this user. We walk the rendered Asset Ids in order
    // and pick the first one whose DB row is suitable for partial
    // disposal (Active, undisposed, with cost > 0).
    const optionIds = await page.$$eval(
      '#disposalOptions .dropdown-option',
      (els) => els.map((e) => Number(e.getAttribute('data-value'))).filter(Boolean)
    );
    expect(optionIds.length, 'partial disposal dropdown should list assets').toBeGreaterThan(0);

    for (const candidateId of optionIds) {
      const row = await dbOne(
        'SELECT "Id","AssetNumber","AcquisitionCost","AccumulatedDepreciation","Status","Active" FROM "Assets" WHERE "Id"=$1',
        [candidateId]
      );
      if (
        row &&
        Number(row.Status) === 0 &&
        row.Active === true &&
        Number(row.AcquisitionCost) > 0
      ) {
        ASSET_ID = row.Id;
        original = row;
        break;
      }
    }
    expect(ASSET_ID, 'a usable asset must exist in the dropdown').toBeTruthy();

    await searchInput.fill(original.AssetNumber);
    const option = page.locator(
      `#disposalOptions .dropdown-option[data-value="${ASSET_ID}"]`
    );
    await expect(option).toBeVisible();
    await option.click();

    // Submit button becomes enabled by the page's own JS once an asset is
    // selected — assert that and then fill the rest of the form.
    await expect(page.locator('#disposalSubmitBtn')).toBeEnabled();

    await panel.locator('input[name="percentage"]').fill(String(PERCENTAGE));
    await panel.locator('input[name="saleProceeds"]').fill(String(SALE_PROCEEDS));
    await panel.locator('input[name="buyer"]').fill('Spec Buyer');
    await panel.locator('textarea[name="notes"]').fill(NOTES);

    // Pick the first real reason option from the visible <select>.
    const reasonValue = await panel
      .locator('select[name="reason"] option')
      .first()
      .getAttribute('value');
    if (reasonValue) {
      await panel.locator('select[name="reason"]').selectOption(reasonValue);
    }

    await Promise.all([
      page.waitForURL(/\/BulkOperations/),
      page.click('#disposalSubmitBtn'),
    ]);

    // Parent asset cost decremented.
    const updatedParent = await dbOne(
      'SELECT "AcquisitionCost","AccumulatedDepreciation","Status","Active" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    const expectedCost = Number(original.AcquisitionCost) * (1 - PERCENTAGE / 100);
    expect(Math.round(Number(updatedParent.AcquisitionCost) * 100)).toBe(
      Math.round(expectedCost * 100)
    );
    // Parent stays Active (it is only partially disposed).
    expect(Number(updatedParent.Status)).toBe(0);
    expect(updatedParent.Active).toBe(true);

    // PartialDisposal row exists with our unique notes tag.
    const disposal = await dbOne(
      'SELECT "Id","PercentageDisposed","SaleProceeds","Notes","Buyer","JournalEntryId" FROM "PartialDisposals" WHERE "AssetId"=$1 AND "Notes"=$2 ORDER BY "Id" DESC LIMIT 1',
      [ASSET_ID, NOTES]
    );
    expect(disposal, 'PartialDisposal record should exist').toBeTruthy();
    expect(Number(disposal.SaleProceeds)).toBe(SALE_PROCEEDS);
    expect(Math.round(Number(disposal.PercentageDisposed) * 10000)).toBe(PERCENTAGE * 100);
    expect((disposal.Buyer || '').toLowerCase()).toBe('spec buyer');

    // If the parent asset's company has a Book + GL account mapping
    // configured, the service must post a balanced gain/loss journal
    // entry for the disposed portion and link it on PartialDisposal.
    const parentRow = await dbOne(
      'SELECT "CompanyId" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    // Resolve the same Book + GL account fallbacks the service uses
    // (BookGlAccount overrides Book.GlAccount* defaults). Then determine
    // whether the configuration is sufficient for the proceeds + gain/loss
    // path this test exercises.
    const bookRow = parentRow && parentRow.CompanyId != null
      ? await dbOne(
          'SELECT "Id","GlAccountAccumDep","GlAccountAssetClearing","GlAccountGainOnDisposal","GlAccountLossOnDisposal" FROM "Books" WHERE "CompanyId"=$1 ORDER BY "IsPrimaryBook" DESC, "Id" ASC LIMIT 1',
          [parentRow.CompanyId]
        )
      : null;
    const bookGl = bookRow
      ? await dbOne(
          'SELECT "AccumulatedDepreciation","Asset","Clearing","GainOnDisposal","LossOnDisposal" FROM "BookGlAccounts" WHERE "BookId"=$1 ORDER BY "Id" ASC LIMIT 1',
          [bookRow.Id]
        )
      : null;
    const firstNonEmpty = (...vs) => vs.find((v) => v && String(v).trim().length > 0) || null;
    const accumDepAcct = bookRow ? firstNonEmpty(bookGl?.AccumulatedDepreciation, bookRow.GlAccountAccumDep) : null;
    const assetAcct    = bookRow ? firstNonEmpty(bookGl?.Asset, bookRow.GlAccountAssetClearing) : null;
    const clearingAcct = bookRow ? firstNonEmpty(bookGl?.Clearing, bookRow.GlAccountAssetClearing) : null;
    const gainAcct     = bookRow ? firstNonEmpty(bookGl?.GainOnDisposal, bookRow.GlAccountGainOnDisposal) : null;
    const lossAcct     = bookRow ? firstNonEmpty(bookGl?.LossOnDisposal, bookRow.GlAccountLossOnDisposal) : null;

    const costDisposed = Number(original.AcquisitionCost) * (PERCENTAGE / 100);
    const accumDepDisposed = Number(original.AccumulatedDepreciation) * (PERCENTAGE / 100);
    const expectedGainLoss = SALE_PROCEEDS - (costDisposed - accumDepDisposed);

    const hasGlConfig =
      !!accumDepAcct &&
      !!assetAcct &&
      (SALE_PROCEEDS <= 0 || !!clearingAcct) &&
      (expectedGainLoss <= 0 || !!gainAcct) &&
      (expectedGainLoss >= 0 || !!lossAcct);

    if (hasGlConfig) {
      expect(disposal.JournalEntryId, 'partial disposal should link a journal entry when GL is configured').toBeTruthy();
      createdJournalEntryIds.push(disposal.JournalEntryId);
      const journal = await dbOne(
        'SELECT "Id","Source","BookId" FROM "JournalEntries" WHERE "Id"=$1',
        [disposal.JournalEntryId]
      );
      expect(journal, 'journal entry row should exist').toBeTruthy();
      expect(String(journal.Source).toLowerCase()).toBe('partialdisposal');
      const lineRows = await dbQuery(
        'SELECT "Account","Debit","Credit" FROM "JournalLines" WHERE "JournalEntryId"=$1',
        [disposal.JournalEntryId]
      );
      expect(lineRows.length, 'journal entry should have at least 3 lines').toBeGreaterThanOrEqual(3);
      const totalDebit = lineRows.reduce((s, r) => s + Number(r.Debit || 0), 0);
      const totalCredit = lineRows.reduce((s, r) => s + Number(r.Credit || 0), 0);
      expect(Math.round(totalDebit * 100)).toBe(Math.round(totalCredit * 100));
    }

    // Child asset row was created with the disposed cost portion and
    // ParentAssetId pointing at the parent.
    const child = await dbOne(
      'SELECT "Id","AssetNumber","AcquisitionCost","Status","Active","ParentAssetId" FROM "Assets" WHERE "ParentAssetId"=$1 ORDER BY "Id" DESC LIMIT 1',
      [ASSET_ID]
    );
    expect(child, 'child asset for the disposed portion should exist').toBeTruthy();
    createdChildIds.push(child.Id);
    expect(Number(child.ParentAssetId)).toBe(ASSET_ID);
    expect(Math.round(Number(child.AcquisitionCost) * 100)).toBe(
      Math.round(Number(original.AcquisitionCost) * (PERCENTAGE / 100) * 100)
    );
    expect(Number(child.Status)).toBe(2); // Disposed
    expect(child.Active).toBe(false);

    // Both parent and child are visible on the /Assets register. We hit
    // the page via Playwright's API-level request to inspect the raw,
    // server-rendered HTML (so we don't depend on the client-side grid's
    // pagination/visibility behavior).
    const reg = await page.request.get(
      'http://127.0.0.1:5000/Assets',
      { headers: { Cookie: (await page.context().cookies())
          .map((c) => `${c.name}=${c.value}`).join('; ') } }
    );
    expect(reg.status()).toBeLessThan(400);
    const html = await reg.text();
    expect(html).toContain(original.AssetNumber);
    expect(html).toContain(child.AssetNumber);
  });
});
