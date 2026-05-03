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

  // Helper: pull a single CSP directive (e.g. "script-src") out of the header
  // so we can assert on its source list without false positives from other
  // directives like script-src-attr / style-src-attr.
  function cspDirective(csp, name) {
    const parts = csp.split(';').map(s => s.trim()).filter(Boolean);
    const hit = parts.find(p => p === name || p.startsWith(name + ' '));
    return hit ? hit.slice(name.length).trim() : '';
  }

  test('Security headers — CSP/XCTO/Referrer/Permissions on /_live', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const res = await req.get('/_live');
      expect(res.status()).toBe(200);
      const h = res.headers();
      const csp = h['content-security-policy'];
      expect(csp, 'CSP must be set').toBeTruthy();
      expect(csp).toMatch(/frame-ancestors[^;]*'self'/);
      expect(csp).toMatch(/replit\.app/);
      expect(h['x-content-type-options']).toBe('nosniff');
      expect(h['referrer-policy']).toBe('strict-origin-when-cross-origin');
      expect(h['permissions-policy']).toBeTruthy();
      expect(h['permissions-policy']).toMatch(/camera=\(\)/);

      // Phase 4 / Task #15 — script-src and style-src must NOT fall back to
      // 'unsafe-inline'; instead they must whitelist a per-request nonce.
      const scriptSrc = cspDirective(csp, 'script-src');
      const styleSrc = cspDirective(csp, 'style-src');
      expect(scriptSrc, 'script-src directive present').toBeTruthy();
      expect(styleSrc, 'style-src directive present').toBeTruthy();
      expect(scriptSrc).not.toMatch(/'unsafe-inline'/);
      expect(styleSrc).not.toMatch(/'unsafe-inline'/);
      expect(scriptSrc).toMatch(/'nonce-[A-Za-z0-9]+'/);
      expect(styleSrc).toMatch(/'nonce-[A-Za-z0-9]+'/);
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
      const csp = h['content-security-policy'];
      expect(csp, 'CSP must be set on HTML responses').toBeTruthy();
      expect(h['x-content-type-options']).toBe('nosniff');
      expect(h['referrer-policy']).toBe('strict-origin-when-cross-origin');
      expect(h['permissions-policy']).toBeTruthy();

      // Phase 4 / Task #15 — tightened CSP also applies on full HTML pages,
      // and each request must get a fresh nonce embedded into the policy.
      const scriptSrc = cspDirective(csp, 'script-src');
      const styleSrc = cspDirective(csp, 'style-src');
      expect(scriptSrc).not.toMatch(/'unsafe-inline'/);
      expect(styleSrc).not.toMatch(/'unsafe-inline'/);
      const nonceMatch = scriptSrc.match(/'nonce-([A-Za-z0-9]+)'/);
      expect(nonceMatch, 'script-src must carry a nonce').toBeTruthy();

      // Same nonce should appear on inline <script>/<style> tags rendered
      // by Razor via the NonceTagHelper.
      const body = await res.text();
      expect(body).toContain(`nonce="${nonceMatch[1]}"`);
    } finally {
      await req.dispose();
    }
  });

  test('Security headers — every request gets a fresh CSP nonce', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const a = await req.get('/_live');
      const b = await req.get('/_live');
      const aNonce = (a.headers()['content-security-policy'].match(/'nonce-([^']+)'/) || [])[1];
      const bNonce = (b.headers()['content-security-policy'].match(/'nonce-([^']+)'/) || [])[1];
      expect(aNonce).toBeTruthy();
      expect(bNonce).toBeTruthy();
      expect(aNonce).not.toBe(bNonce);
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
