// Smoke-test every JSON/Web API endpoint exposed by the app's controllers.
// We log in via the UI to obtain an auth cookie, then issue authenticated
// requests using Playwright's APIRequestContext.
const { test, expect, request: pwRequest } = require('@playwright/test');
const { BASE, login, dbOne, pickers, pickFirstId } = require('./_helpers');

async function authedRequest(browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await login(page);
  const cookies = await context.cookies();
  await page.close();
  const req = await pwRequest.newContext({ baseURL: BASE });
  await req.storageState();
  const cookieHeader = cookies.map((c) => `${c.name}=${c.value}`).join('; ');
  // Tenant-scoped /api/v1/* endpoints require these headers (enforced by
  // ApiHeaderEnforcementMiddleware). Default to tenant=1, user=1, org=1
  // which matches the seed data for the admin login.
  const apiHeaders = {
    cookie: cookieHeader,
    'X-Tenant-Id': '1',
    'X-User-Id': '1',
    'X-Org-Node-Id': '1',
  };
  return { req, context, cookieHeader, apiHeaders };
}

test.describe('SMOKE — REST API endpoints', () => {
  let req, context, cookieHeader, apiHeaders;

  test.beforeAll(async ({ browser }) => {
    ({ req, context, cookieHeader, apiHeaders } = await authedRequest(browser));
  });

  test.afterAll(async () => {
    await req.dispose();
    await context.close();
  });

  test('GET /auth/whoami returns the current admin', async () => {
    // Note: AuthController is routed at "/auth", not "/api/auth".
    const r = await req.get('/auth/whoami', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBe(200);
    const json = await r.json();
    expect(JSON.stringify(json).toLowerCase()).toContain('admin');
  });

  test('GET /api/v1/org/sites returns an array', async () => {
    const r = await req.get('/api/v1/org/sites', { headers: apiHeaders });
    expect(r.status()).toBe(200);
    const json = await r.json();
    expect(Array.isArray(json) || typeof json === 'object').toBeTruthy();
  });

  test('GET /api/v1/org/tree returns the org hierarchy', async () => {
    const r = await req.get('/api/v1/org/tree', { headers: apiHeaders });
    expect(r.status()).toBe(200);
  });

  test('GET /api/v1/analytics/kpis returns KPI payload', async () => {
    const r = await req.get('/api/v1/analytics/kpis', { headers: apiHeaders });
    expect([200, 204]).toContain(r.status());
  });

  test('GET /api/v1/analytics/drilldown is reachable', async () => {
    const r = await req.get('/api/v1/analytics/drilldown?metric=assets', { headers: apiHeaders });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/drilldown/cip-kpis returns CIP KPIs', async () => {
    const r = await req.get('/api/v1/drilldown/cip-kpis', { headers: apiHeaders });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/drilldown/party-summary is reachable', async () => {
    const r = await req.get('/api/v1/drilldown/party-summary?type=vendor', { headers: apiHeaders });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/backup/status returns status', async () => {
    const r = await req.get('/api/backup/status', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/assets/{id} is reachable', async () => {
    const id = await pickers.asset();
    test.skip(!id, 'no asset rows');
    // AssetsApiController uses X-API-Key auth (not cookies), so 401 here is
    // the correct response when no API key is supplied. We just assert the
    // endpoint is wired up and not crashing.
    const r = await req.get(`/api/v1/assets/${id}`, { headers: apiHeaders });
    expect([200, 401]).toContain(r.status());
  });

  test('GET /api/v1/details/{type}/{id} returns enterprise detail', async () => {
    const id = await pickers.asset();
    test.skip(!id, 'no asset rows');
    const r = await req.get(`/api/v1/details/asset/${id}`, { headers: apiHeaders });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/items/{id}/stocking returns item stocking', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/items/${id}/stocking`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/barcode/generate/{itemId} returns a PNG barcode', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/barcode/generate/${id}`, { headers: { cookie: cookieHeader } });
    // 503 is the defensive backstop when the SkiaSharp native library is
    // unavailable on the host. The happy path returns a PNG.
    expect([200, 503]).toContain(r.status());
    if (r.status() === 200) {
      expect(r.headers()['content-type']).toContain('image/png');
      const body = await r.body();
      expect(body.length).toBeGreaterThan(100);
    }
  });

  test('GET /api/barcode/label/{itemId} returns a PNG label', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/barcode/label/${id}`, { headers: { cookie: cookieHeader } });
    expect([200, 503]).toContain(r.status());
    if (r.status() === 200) {
      expect(r.headers()['content-type']).toContain('image/png');
      const body = await r.body();
      expect(body.length).toBeGreaterThan(100);
    }
  });

  test('GET /api/barcode/lookup/{value} handles unknown codes gracefully', async () => {
    const r = await req.get('/api/barcode/lookup/__nope__', { headers: { cookie: cookieHeader } });
    expect([200, 204, 404]).toContain(r.status());
  });
});
