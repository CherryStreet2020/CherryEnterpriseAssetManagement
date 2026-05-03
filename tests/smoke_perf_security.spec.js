// Regression coverage for Phase 3 performance & security features.
// Locks in three production guarantees:
//   1) Server-Timing header is emitted on every response (browser dev-tools
//      visibility + APM ingestion).
//   2) Brotli AND Gzip response compression engage when the client asks for
//      them on text content (HTML).
//   3) The fixed-window rate limiter on POST /Account/Login rejects with
//      HTTP 429 once the per-IP budget is exhausted (anti-credential-stuffing).
//
// All tests use anonymous request contexts and target /Account/Login or
// /healthz so they don't interfere with cookied tests in other specs.
const { test, expect, request: pwRequest } = require('@playwright/test');
const { BASE } = require('./_helpers');

test.describe('SMOKE — Phase 3 perf + security', () => {

  test('Server-Timing header present on /healthz', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    const res = await req.get('/healthz');
    expect(res.status()).toBe(200);
    const st = res.headers()['server-timing'];
    expect(st, 'Server-Timing must be present').toBeTruthy();
    // Format: "total;dur=<float>"
    expect(st).toMatch(/\btotal;dur=\d+(?:\.\d+)?/);
    await req.dispose();
  });

  test('Brotli compression engages when client requests it', async () => {
    // Disable automatic decompression so we can inspect Content-Encoding.
    const req = await pwRequest.newContext({
      baseURL: BASE,
      extraHTTPHeaders: { 'Accept-Encoding': 'br' },
    });
    const res = await req.get('/Account/Login');
    expect(res.status()).toBe(200);
    const enc = (res.headers()['content-encoding'] || '').toLowerCase();
    expect(enc, `expected br, got "${enc}"`).toBe('br');
    await req.dispose();
  });

  test('Gzip compression engages when only gzip is offered', async () => {
    const req = await pwRequest.newContext({
      baseURL: BASE,
      extraHTTPHeaders: { 'Accept-Encoding': 'gzip' },
    });
    const res = await req.get('/Account/Login');
    expect(res.status()).toBe(200);
    const enc = (res.headers()['content-encoding'] || '').toLowerCase();
    expect(enc, `expected gzip, got "${enc}"`).toBe('gzip');
    await req.dispose();
  });

  test('Login endpoint rate-limits per (IP, Username) at 100/min (returns 429)', async () => {
    test.setTimeout(90_000);
    // Use a UNIQUE probe username per test run so we never share a counter
    // with the "admin" partition that all other test specs use to log in.
    // Burst 110 POSTs against the same (IP, probeUser) partition: the
    // limiter is 100/min, so we expect ~100 OK + ~10 rejected (429). We
    // assert >=1 success AND >=5 rejections — generous bounds.
    const probeUser = `rate-limit-probe-${Date.now()}`;
    const req = await pwRequest.newContext({ baseURL: BASE });
    let ok = 0;
    let limited = 0;
    for (let i = 0; i < 110; i++) {
      const res = await req.post('/Account/Login', {
        form: { Username: probeUser, Password: 'wrong' },
        failOnStatusCode: false,
      });
      if (res.status() === 429) limited++;
      else ok++;
    }
    expect(ok, 'some POSTs must succeed before the limit').toBeGreaterThan(0);
    expect(limited, 'limiter must reject excess POSTs with 429').toBeGreaterThanOrEqual(5);
    await req.dispose();
  });
});
