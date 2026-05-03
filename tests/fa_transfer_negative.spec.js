const { test, expect } = require('@playwright/test');
const { login, gotoApp, dbQuery, dbOne, pickAsset } = require('./_helpers');

let ASSET_ID;
let original;
let hasTransferHistory;
let originalTransferCount;
let outOfTenantLocation;

async function snapshotTransferCount(assetId) {
  if (!hasTransferHistory) return 0;
  const row = await dbOne(
    'SELECT COUNT(*)::int AS n FROM "AssetTransfers" WHERE "AssetId"=$1',
    [assetId]
  );
  return Number(row.n);
}

async function bypassClientValidation(page) {
  await page.evaluate(() => {
    const form = document.querySelector('form');
    if (form) form.setAttribute('novalidate', 'novalidate');
    document.querySelectorAll('input, textarea, select').forEach((el) => {
      el.removeAttribute('required');
    });
  });
}

async function injectLocationOption(page, value) {
  await page.evaluate((val) => {
    const sel = document.querySelector('select[name="NewLocationId"]');
    if (!sel) return;
    const opt = document.createElement('option');
    opt.value = String(val);
    opt.textContent = `INJECTED-${val}`;
    sel.appendChild(opt);
  }, value);
  await page.selectOption('select[name="NewLocationId"]', String(value));
}

async function assertNoMutation() {
  const after = await dbOne(
    'SELECT "LocationId","DepartmentId","Bay" FROM "Assets" WHERE "Id"=$1',
    [ASSET_ID]
  );
  expect(Number(after.LocationId)).toBe(Number(original.LocationId));
  expect(after.DepartmentId == null ? null : Number(after.DepartmentId)).toBe(
    original.DepartmentId == null ? null : Number(original.DepartmentId)
  );
  expect(after.Bay).toBe(original.Bay);
  const count = await snapshotTransferCount(ASSET_ID);
  expect(count).toBe(originalTransferCount);
}

async function assertRejected(page) {
  expect(page.url()).toContain(`/Assets/Transfer/${ASSET_ID}`);
  expect(page.url()).not.toMatch(/\/Assets\/Asset\/\d+/);
  await expect(page.locator('.alert-danger, .text-danger').first()).toBeVisible();
  await assertNoMutation();
}

test.describe('FA — Transfer (negative paths)', () => {
  test.beforeAll(async () => {
    const a = await pickAsset({ rank: 5, requireLocation: true });
    ASSET_ID = a.Id;
    original = await dbOne(
      'SELECT "Id","LocationId","DepartmentId","Bay","CompanyId","SiteId" FROM "Assets" WHERE "Id"=$1',
      [ASSET_ID]
    );
    expect(original).toBeTruthy();

    const tbl = await dbOne(`SELECT to_regclass('public."AssetTransfers"') AS t`);
    hasTransferHistory = !!(tbl && tbl.t);
    originalTransferCount = await snapshotTransferCount(ASSET_ID);

    // The cross-company guard checks against TenantContext.VisibleCompanyIds,
    // which for an admin user with no assigned company resolves to ALL
    // active companies in the SAME tenant. To exercise the guard
    // deterministically, seed a Company in a DIFFERENT tenant and a
    // Location inside it; that Location's CompanyId can never be in
    // admin's visible set.
    let otherTenant = await dbOne(
      `SELECT "Id" FROM "Tenants" WHERE "Code"='TEST-XT' LIMIT 1`
    );
    if (!otherTenant) {
      const ins = await dbQuery(
        `INSERT INTO "Tenants"("Name","Code","IsActive","CreatedAt")
           VALUES('FA Negative Cross-Tenant','TEST-XT',true,NOW()) RETURNING "Id"`
      );
      otherTenant = ins[0];
    }
    let otherCo = await dbOne(
      `SELECT "Id" FROM "Companies" WHERE "TenantId"=$1 AND "CompanyCode"='TEST-XT-CO' LIMIT 1`,
      [otherTenant.Id]
    );
    if (!otherCo) {
      // Companies has many NOT NULL columns and an identity Id; build a
      // schema-agnostic clone by listing all columns except Id from
      // information_schema, then patch the identifying fields.
      const cols = await dbQuery(
        `SELECT column_name FROM information_schema.columns
           WHERE table_schema='public' AND table_name='Companies' AND column_name <> 'Id'
           ORDER BY ordinal_position`
      );
      const colList = cols.map((c) => `"${c.column_name}"`).join(',');
      const ins = await dbQuery(
        `INSERT INTO "Companies" (${colList})
           SELECT ${colList} FROM "Companies" WHERE "Id"=$1
           RETURNING "Id"`,
        [original.CompanyId]
      );
      await dbQuery(
        `UPDATE "Companies"
            SET "TenantId"=$1,"CompanyCode"='TEST-XT-CO',
                "Name"='FA Negative Cross-Tenant Co',"ParentCompanyId"=NULL
          WHERE "Id"=$2`,
        [otherTenant.Id, ins[0].Id]
      );
      otherCo = ins[0];
    }
    let xc = await dbOne(
      `SELECT "Id" FROM "Locations" WHERE "CompanyId"=$1 AND "Code"='TEST-XT-LOC' LIMIT 1`,
      [otherCo.Id]
    );
    if (!xc) {
      const ins = await dbQuery(
        `INSERT INTO "Locations"("Code","Name","IsActive","CompanyId","SortOrder","Type","CreatedAt")
           VALUES('TEST-XT-LOC','FA Negative Cross-Tenant Loc',true,$1,9999,0,NOW()) RETURNING "Id"`,
        [otherCo.Id]
      );
      xc = ins[0];
    }
    outOfTenantLocation = xc.Id;
  });

  test.afterEach(async () => {
    await dbQuery(
      'UPDATE "Assets" SET "LocationId"=$2,"DepartmentId"=$3,"Bay"=$4 WHERE "Id"=$1',
      [ASSET_ID, original.LocationId, original.DepartmentId, original.Bay]
    );
    if (hasTransferHistory) {
      await dbQuery(
        `DELETE FROM "AssetTransfers" WHERE "AssetId"=$1
           AND ("Notes" LIKE 'NEG-TRANSFER-%' OR "ToLocation" LIKE 'INJECTED-%' OR "ToLocation"='Test Cross-Company Loc')`,
        [ASSET_ID]
      );
    }
  });

  test('non-existent location id is rejected with "valid location" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Transfer/${ASSET_ID}`);
    await bypassClientValidation(page);
    await injectLocationOption(page, 999999999);
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    await page.fill('textarea[name="Notes"]', 'NEG-TRANSFER-NONEXISTENT');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/select a valid location/i);
    await assertRejected(page);
  });

  test('NewLocationId=0 is rejected with "valid location" message', async ({ page }) => {
    await login(page);
    await gotoApp(page, `/Assets/Transfer/${ASSET_ID}`);
    await bypassClientValidation(page);
    await injectLocationOption(page, 0);
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    await page.fill('textarea[name="Notes"]', 'NEG-TRANSFER-ZERO');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/select a valid location/i);
    await assertRejected(page);
  });

  test('cross-company location id is rejected and creates no AssetTransfer row', async ({ page }) => {
    expect(outOfTenantLocation, 'cross-company location must exist (seeded if missing)').toBeTruthy();

    await login(page);
    await gotoApp(page, `/Assets/Transfer/${ASSET_ID}`);
    await bypassClientValidation(page);
    await injectLocationOption(page, outOfTenantLocation);
    await page.fill('input[name="TransferDate"]', '2026-05-15');
    await page.fill('textarea[name="Notes"]', 'NEG-TRANSFER-XCOMPANY');

    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('.alert-danger')).toContainText(/select a valid location/i);
    await assertRejected(page);
  });
});
