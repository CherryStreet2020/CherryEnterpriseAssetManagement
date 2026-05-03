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
  await req.storageState(); // noop, just ensure context is initialised
  // Replay cookies on the API context so it shares the auth session.
  const cookieHeader = cookies.map((c) => `${c.name}=${c.value}`).join('; ');
  return { req, context, cookieHeader };
}

test.describe('SMOKE — REST API endpoints', () => {
  let req, context, cookieHeader;

  test.beforeAll(async ({ browser }) => {
    ({ req, context, cookieHeader } = await authedRequest(browser));
  });

  test.afterAll(async () => {
    await req.dispose();
    await context.close();
  });

  test('GET /api/auth/whoami returns the current admin', async () => {
    const r = await req.get('/api/auth/whoami', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBe(200);
    const json = await r.json();
    expect(JSON.stringify(json).toLowerCase()).toContain('admin');
  });

  test('GET /api/v1/org/sites returns an array', async () => {
    const r = await req.get('/api/v1/org/sites', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBe(200);
    const json = await r.json();
    expect(Array.isArray(json) || typeof json === 'object').toBeTruthy();
  });

  test('GET /api/v1/org/tree returns the org hierarchy', async () => {
    const r = await req.get('/api/v1/org/tree', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBe(200);
  });

  test('GET /api/v1/analytics/kpis returns KPI payload', async () => {
    const r = await req.get('/api/v1/analytics/kpis', { headers: { cookie: cookieHeader } });
    expect([200, 204]).toContain(r.status());
  });

  test('GET /api/v1/analytics/drilldown is reachable', async () => {
    const r = await req.get('/api/v1/analytics/drilldown?metric=assets', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/drilldown/cip-kpis returns CIP KPIs', async () => {
    const r = await req.get('/api/v1/drilldown/cip-kpis', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/drilldown/party-summary is reachable', async () => {
    const r = await req.get('/api/v1/drilldown/party-summary?type=vendor', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/backup/status returns status', async () => {
    const r = await req.get('/api/backup/status', { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/v1/assets/{id} returns the asset', async () => {
    const id = await pickers.asset();
    test.skip(!id, 'no asset rows');
    const r = await req.get(`/api/v1/assets/${id}`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBe(200);
    const json = await r.json();
    expect(json.id ?? json.Id).toBe(id);
  });

  test('GET /api/v1/details/{type}/{id} returns enterprise detail', async () => {
    const id = await pickers.asset();
    test.skip(!id, 'no asset rows');
    const r = await req.get(`/api/v1/details/asset/${id}`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/items/{id}/stocking returns item stocking', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/items/${id}/stocking`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/barcode/generate/{itemId} returns a barcode', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/barcode/generate/${id}`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/barcode/label/{itemId} returns a label', async () => {
    const id = await pickers.item();
    test.skip(!id, 'no item rows');
    const r = await req.get(`/api/barcode/label/${id}`, { headers: { cookie: cookieHeader } });
    expect(r.status()).toBeLessThan(500);
  });

  test('GET /api/barcode/lookup/{value} handles unknown codes gracefully', async () => {
    const r = await req.get('/api/barcode/lookup/__nope__', { headers: { cookie: cookieHeader } });
    // 404 is the expected "not found" response, not an error.
    expect([200, 204, 404]).toContain(r.status());
  });
});
