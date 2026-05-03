// UI-level coverage for the Barcode Labels admin page. Drives the page +
// controller + SkiaSharp native lib end-to-end through an authenticated
// browser session and asserts the rendered PNGs are real bitmap images
// (magic bytes, content-type, body size, and decoded width/height).
const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { BASE, login, dbOne } = require('./_helpers');

const SS_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');
const PNG_MAGIC = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

function parsePng(buf) {
  if (!buf || buf.length < 24) throw new Error('buffer too small to be a PNG');
  if (!buf.slice(0, 8).equals(PNG_MAGIC)) throw new Error('missing PNG magic bytes');
  // IHDR chunk starts at byte 8: 4-byte length, 4-byte "IHDR" type,
  // then width/height as big-endian uint32.
  const ihdr = buf.slice(12, 16).toString('ascii');
  if (ihdr !== 'IHDR') throw new Error(`expected IHDR chunk, got ${ihdr}`);
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

async function pickItemId(page) {
  const cb = page.locator('input.item-select').first();
  if (await cb.count() > 0) {
    const v = parseInt(await cb.getAttribute('value'), 10);
    if (Number.isFinite(v)) return v;
  }
  const row = await dbOne('SELECT "Id" FROM "Items" ORDER BY "Id" LIMIT 1');
  return row ? row.Id : null;
}

test.describe('UI — Barcode Labels admin page', () => {
  test.beforeAll(() => { fs.mkdirSync(SS_DIR, { recursive: true }); });

  test('admin barcode page loads and lists items', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Admin/Barcodes`);
    expect(resp.status()).toBeLessThan(400);
    await page.waitForLoadState('domcontentloaded');

    const hasTable = await page.locator('table#itemsTable').count() > 0;
    const hasEmpty = await page.locator('text=No Items Found').count() > 0;
    expect(hasTable || hasEmpty).toBeTruthy();
    await page.screenshot({ path: path.join(SS_DIR, 'ui_barcode_admin.png') });
  });

  test('GET /api/barcode/generate/{id} returns a valid PNG with sane dimensions', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Admin/Barcodes`);
    await page.waitForLoadState('domcontentloaded');

    const itemId = await pickItemId(page);
    test.skip(!itemId, 'no Items rows to test against');

    const r = await page.request.get(`${BASE}/api/barcode/generate/${itemId}`);
    expect(r.status(), 'barcode endpoint must succeed (no 503 backstop)').toBe(200);
    expect(r.headers()['content-type']).toContain('image/png');

    const body = await r.body();
    expect(body.length).toBeGreaterThan(200);
    const { width, height } = parsePng(body);
    expect(width).toBeGreaterThan(40);
    expect(height).toBeGreaterThan(20);
    expect(width).toBeLessThan(4096);
    expect(height).toBeLessThan(4096);
  });

  test('GET /api/barcode/label/{id} returns a valid PNG label with sane dimensions', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Admin/Barcodes`);
    await page.waitForLoadState('domcontentloaded');

    const itemId = await pickItemId(page);
    test.skip(!itemId, 'no Items rows to test against');

    const r = await page.request.get(`${BASE}/api/barcode/label/${itemId}`);
    expect(r.status(), 'label endpoint must succeed (no 503 backstop)').toBe(200);
    expect(r.headers()['content-type']).toContain('image/png');

    const body = await r.body();
    // Labels embed the barcode plus part number and description text, so
    // they're meaningfully larger than a bare barcode.
    expect(body.length).toBeGreaterThan(500);
    const { width, height } = parsePng(body);
    expect(width).toBeGreaterThan(80);
    expect(height).toBeGreaterThan(40);
    expect(width).toBeLessThan(4096);
    expect(height).toBeLessThan(4096);
  });

  test('clicking Generate fetches a PNG and renders it as <img> in the preview', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Admin/Barcodes`);
    await page.waitForLoadState('domcontentloaded');

    const genBtn = page.locator('button[onclick^="generateBarcode("]').first();
    if (await genBtn.count() === 0) test.skip(true, 'no items rendered');

    // Capture the network response triggered by the button click so we can
    // assert the JS path actually hit /api/barcode/generate/{id} and got PNG.
    const respPromise = page.waitForResponse(r =>
      /\/api\/barcode\/generate\/\d+$/.test(r.url())
    );
    await genBtn.click();
    const resp = await respPromise;
    expect(resp.status()).toBe(200);
    expect(resp.headers()['content-type']).toContain('image/png');
    const { width, height } = parsePng(await resp.body());
    expect(width).toBeGreaterThan(40);
    expect(height).toBeGreaterThan(20);

    // And confirm the page actually rendered the result, not an error message.
    const img = page.locator('#generatedBarcodeImg');
    await expect(img).toBeVisible({ timeout: 3000 });
    const src = await img.getAttribute('src');
    expect(src).toMatch(/^blob:/);
  });

  test('clicking Print embeds a label iframe whose src returns a valid PNG', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Admin/Barcodes`);
    await page.waitForLoadState('domcontentloaded');

    const printBtn = page.locator('button[onclick^="printLabel("]').first();
    if (await printBtn.count() === 0) test.skip(true, 'no items rendered');

    await printBtn.click();
    const iframe = page.locator('#barcodePreviewArea iframe');
    await expect(iframe).toBeVisible({ timeout: 2000 });
    const src = await iframe.getAttribute('src');
    expect(src).toMatch(/^\/api\/barcode\/label\/\d+$/);

    // Follow the iframe src through the same authenticated context and
    // assert the response is a real PNG, not a redirect to a login page.
    const r = await page.request.get(`${BASE}${src}`);
    expect(r.status()).toBe(200);
    expect(r.headers()['content-type']).toContain('image/png');
    const { width, height } = parsePng(await r.body());
    expect(width).toBeGreaterThan(80);
    expect(height).toBeGreaterThan(40);
  });
});
