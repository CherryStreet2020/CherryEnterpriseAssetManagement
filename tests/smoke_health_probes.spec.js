// Regression coverage for Phase 1 production-observability endpoints.
// Locks in three guarantees flagged by the architect review:
//   1) GET /healthz is reachable ANONYMOUSLY (no cookie, no tenant headers)
//      and returns 200 "Healthy" — proves no middleware (auth, tenant guard,
//      org-scope) blocks the liveness probe.
//   2) GET /readyz is reachable ANONYMOUSLY and returns a JSON envelope with
//      per-check status for db + skia. In a healthy dev environment both
//      checks pass, so the top-level status is "Healthy" and HTTP 200.
//   3) The RequestIdMiddleware echoes any inbound X-Request-Id header back
//      on the response — required for log correlation.
//
// If a future tenant-middleware change drops /healthz or /readyz from
// IsExemptPath, this spec will fail with HTTP 400 on the very first GET.
const { test, expect, request: pwRequest } = require('@playwright/test');
const { BASE } = require('./_helpers');

test.describe('SMOKE — Phase 1 health & readiness probes', () => {
  let req;

  test.beforeAll(async () => {
    // Brand-new request context: no cookies, no extra headers, fully anonymous.
    req = await pwRequest.newContext({ baseURL: BASE });
  });

  test.afterAll(async () => {
    if (req) await req.dispose();
  });

  test('/healthz returns 200 "Healthy" anonymously', async () => {
    const res = await req.get('/healthz');
    expect(res.status(), 'liveness must be 200 with no auth/tenant headers').toBe(200);
    const body = (await res.text()).trim();
    expect(body).toBe('Healthy');
  });

  test('/readyz returns 200 JSON envelope with db + skia anonymously', async () => {
    const res = await req.get('/readyz');
    expect(res.status(), 'readiness must be 200 in a healthy dev environment').toBe(200);
    expect(res.headers()['content-type'] || '').toMatch(/application\/json/i);

    const json = await res.json();
    expect(json).toHaveProperty('status', 'Healthy');
    expect(typeof json.totalDurationMs).toBe('number');
    expect(Array.isArray(json.checks)).toBe(true);

    const names = json.checks.map((c) => c.name).sort();
    expect(names).toEqual(['db', 'skia']);

    for (const check of json.checks) {
      expect(check.status, `${check.name} must be Healthy in dev`).toBe('Healthy');
      expect(typeof check.durationMs).toBe('number');
      expect(check.exception ?? null).toBeNull();
    }
  });

  test('X-Request-Id header is echoed back on response', async () => {
    const sentId = `phase1-regression-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
    const res = await req.get('/healthz', { headers: { 'X-Request-Id': sentId } });
    expect(res.status()).toBe(200);
    expect(res.headers()['x-request-id']).toBe(sentId);
  });

  test('responses include an X-Request-Id even when client does not send one', async () => {
    const res = await req.get('/healthz');
    expect(res.status()).toBe(200);
    // Middleware falls back to HttpContext.TraceIdentifier; we just assert it's non-empty.
    const echoed = res.headers()['x-request-id'];
    expect(echoed, 'middleware must always emit X-Request-Id').toBeTruthy();
    expect(echoed.length).toBeGreaterThan(0);
  });
});
