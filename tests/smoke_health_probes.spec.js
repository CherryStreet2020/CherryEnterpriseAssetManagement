// Regression coverage for Phase 1 production-observability endpoints.
// Locks in four guarantees:
//   1) GET /_live (canonical liveness path, GFE-safe) is reachable
//      ANONYMOUSLY and returns 200 "Healthy" — proves no middleware
//      (auth, tenant guard, org-scope) blocks the liveness probe.
//   2) GET /healthz (legacy alias) still works in dev and returns the same
//      "Healthy" body — production traffic to /healthz is intercepted by
//      Google Front End before reaching the app, which is expected.
//   3) GET /readyz is reachable ANONYMOUSLY and returns a JSON envelope
//      with per-check status for db + skia. In a healthy dev environment
//      both checks pass, so the top-level status is "Healthy" and HTTP 200.
//   4) The RequestIdMiddleware echoes any inbound X-Request-Id header back
//      on the response — required for log correlation.
//
// If a future tenant-middleware change drops /_live, /healthz, or /readyz
// from IsExemptPath, this spec will fail with HTTP 400 on the very first GET.
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

  test('/_live returns 200 "Healthy" anonymously (canonical liveness path)', async () => {
    const res = await req.get('/_live');
    expect(res.status(), 'liveness must be 200 with no auth/tenant headers').toBe(200);
    const body = (await res.text()).trim();
    expect(body).toBe('Healthy');
  });

  test('/healthz alias still returns 200 "Healthy" in dev', async () => {
    // /healthz is intercepted by Google Front End in production but must
    // remain a working alias in dev for backward-compat.
    const res = await req.get('/healthz');
    expect(res.status()).toBe(200);
    expect((await res.text()).trim()).toBe('Healthy');
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
    const res = await req.get('/_live', { headers: { 'X-Request-Id': sentId } });
    expect(res.status()).toBe(200);
    expect(res.headers()['x-request-id']).toBe(sentId);
  });

  test('responses include an X-Request-Id even when client does not send one', async () => {
    const res = await req.get('/_live');
    expect(res.status()).toBe(200);
    // Middleware falls back to HttpContext.TraceIdentifier; we just assert it's non-empty.
    const echoed = res.headers()['x-request-id'];
    expect(echoed, 'middleware must always emit X-Request-Id').toBeTruthy();
    expect(echoed.length).toBeGreaterThan(0);
  });

  test('Timing-Allow-Origin: * is set so Server-Timing survives cross-origin proxies', async () => {
    // Without Timing-Allow-Origin, browsers and many CDN/edge proxies
    // (including Google Front End in front of Replit Autoscale) hide or
    // strip Server-Timing values from cross-origin consumers.
    const res = await req.get('/_live');
    expect(res.headers()['timing-allow-origin']).toBe('*');
  });
});
