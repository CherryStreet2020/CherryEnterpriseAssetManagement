// Phase 4 regression coverage:
//   1) Distributed login rate limiter (Postgres-backed) — burst against a
//      unique probe username trips 429 even though we're hitting a single
//      dev instance (in dev that's "the cluster"). Cleans up its counter
//      rows after each run so subsequent test runs in the same minute
//      don't inherit a primed bucket.
//   2) Security headers — CSP / X-Content-Type-Options / Referrer-Policy /
//      Permissions-Policy present on /_live AND /Account/Login, and CSP
//      frame-ancestors allows the Replit edge so preview iframe still works.
//   3) OpenTelemetry — the app boots cleanly when OTEL_EXPORTER_OTLP_ENDPOINT
//      is unset (no exporter, zero network). Verified by the fact that
//      /_live still responds 200; an OTel registration crash would have
//      taken the whole process down at startup.
const { test, expect, request: pwRequest } = require('@playwright/test');
const { BASE, dbQuery } = require('./_helpers');

test.describe('SMOKE — Phase 4 distributed limiter + security headers + OTel', () => {

  test('Distributed login limiter rejects burst with HTTP 429', async () => {
    test.setTimeout(120_000);
    const probeUser = `phase4-probe-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    const req = await pwRequest.newContext({ baseURL: BASE });
    let ok = 0;
    let limited = 0;
    try {
      for (let i = 0; i < 130; i++) {
        const res = await req.post('/Account/Login', {
          form: { Username: probeUser, Password: 'wrong' },
          failOnStatusCode: false,
        });
        if (res.status() === 429) limited++;
        else ok++;
      }
      expect(ok, 'some POSTs must succeed before the limit').toBeGreaterThan(0);
      expect(limited, 'distributed limiter must reject excess POSTs with 429')
        .toBeGreaterThanOrEqual(25);
    } finally {
      await req.dispose();
      // Clean up only this probe's counter rows so we don't pollute the
      // table for the next run inside the same 1-minute window.
      try {
        await dbQuery(
          `DELETE FROM "RateLimitCounters" WHERE "PartitionKey" LIKE $1`,
          [`%:${probeUser}`]
        );
      } catch (_e) { /* best-effort */ }
    }
  });

  test('Security headers — CSP/XCTO/Referrer/Permissions on /_live', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const res = await req.get('/_live');
      expect(res.status()).toBe(200);
      const h = res.headers();
      expect(h['content-security-policy'], 'CSP must be set').toBeTruthy();
      expect(h['content-security-policy']).toMatch(/frame-ancestors[^;]*'self'/);
      expect(h['content-security-policy']).toMatch(/replit\.app/);
      expect(h['x-content-type-options']).toBe('nosniff');
      expect(h['referrer-policy']).toBe('strict-origin-when-cross-origin');
      expect(h['permissions-policy']).toBeTruthy();
      expect(h['permissions-policy']).toMatch(/camera=\(\)/);
    } finally {
      await req.dispose();
    }
  });

  test('Security headers — present on /Account/Login (HTML response)', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const res = await req.get('/Account/Login');
      expect(res.status()).toBe(200);
      const h = res.headers();
      expect(h['content-security-policy'], 'CSP must be set on HTML responses').toBeTruthy();
      expect(h['x-content-type-options']).toBe('nosniff');
      expect(h['referrer-policy']).toBe('strict-origin-when-cross-origin');
      expect(h['permissions-policy']).toBeTruthy();
    } finally {
      await req.dispose();
    }
  });

  test('OTel TracerProvider + MeterProvider registered with expected instrumentation', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const res = await req.get('/_otel/diag');
      expect(res.status()).toBe(200);
      const body = await res.json();
      expect(body.tracerProvider, 'TracerProvider must be registered in DI').toBe(true);
      expect(body.meterProvider, 'MeterProvider must be registered in DI').toBe(true);
      expect(body.serviceName).toBe('cherryai-eam');
      expect(body.instrumentation).toEqual(
        expect.arrayContaining(['AspNetCore', 'HttpClient', 'EFCore', 'Runtime', 'Process'])
      );
      expect(body.meterSources).toEqual(
        expect.arrayContaining(['Microsoft.EntityFrameworkCore'])
      );
      expect(body.otlpExporter).toBe('disabled');
    } finally {
      await req.dispose();
    }
  });
});
