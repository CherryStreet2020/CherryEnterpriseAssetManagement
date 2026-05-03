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
const fs = require('fs');
const path = require('path');
const { test, expect, request: pwRequest } = require('@playwright/test');
const { BASE, dbQuery, login } = require('./_helpers');

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

  // ---------------------------------------------------------------------
  // Task #17 — Zero-regression security hardening
  // ---------------------------------------------------------------------

  test('SRI — every CDN <link>/<script> on /Account/Login carries sha384 integrity', async () => {
    const req = await pwRequest.newContext({ baseURL: BASE });
    try {
      const res = await req.get('/Account/Login');
      expect(res.status()).toBe(200);
      const html = await res.text();

      // Find every CDN reference (cdnjs.cloudflare.com or cdn.jsdelivr.net)
      // in the rendered HTML, and assert each tag carries an sha384 hash and
      // crossorigin="anonymous". A naive substring search is fine here — the
      // layouts only emit a small fixed set of pinned CDN tags.
      const tagRe = /<(?:link|script)\b[^>]*\b(?:href|src)\s*=\s*"(https:\/\/(?:cdnjs\.cloudflare\.com|cdn\.jsdelivr\.net)[^"]+)"[^>]*>/gi;
      const tags = [...html.matchAll(tagRe)];
      expect(tags.length, 'login page must reference at least one pinned CDN asset').toBeGreaterThanOrEqual(2);
      for (const m of tags) {
        const tag = m[0];
        const url = m[1];
        expect(tag, `missing sha384 SRI on ${url}`).toMatch(/integrity\s*=\s*"sha384-[A-Za-z0-9+/=]+"/);
        expect(tag, `missing crossorigin on ${url}`).toMatch(/crossorigin\s*=\s*"anonymous"/);
      }
    } finally {
      await req.dispose();
    }
  });

  test('SRI — all five layout CDN refs are pinned at file level', async () => {
    // Defense against accidental future regressions where a developer adds
    // a new CDN ref to a layout but forgets the integrity attribute.
    const layouts = [
      'Pages/Shared/_Layout.cshtml',
      'Pages/Shared/_PopoutLayout.cshtml',
      'Pages/Shared/_ModernLayout.cshtml',
    ];
    let total = 0;
    for (const rel of layouts) {
      const txt = fs.readFileSync(path.resolve(__dirname, '..', rel), 'utf8');
      const tagRe = /<(?:link|script)\b[^>]*\b(?:href|src)\s*=\s*"(https:\/\/(?:cdnjs\.cloudflare\.com|cdn\.jsdelivr\.net)[^"]+)"[^>]*?\/?>/gis;
      const tags = [...txt.matchAll(tagRe)];
      for (const m of tags) {
        total++;
        expect(m[0], `missing sha384 SRI in ${rel} on ${m[1]}`).toMatch(/integrity\s*=\s*"sha384-[A-Za-z0-9+/=]+"/);
      }
    }
    expect(total, 'expected exactly the five pinned CDN refs across layouts').toBe(5);
  });

  test('Auth cookie posture — HttpOnly + SameSite=Lax on successful login', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    try {
      await login(page);
      // Land on any post-login page to confirm the cookie was issued.
      const cookies = await ctx.cookies();
      const auth = cookies.find(c =>
        c.name === '.AspNetCore.Cookies' || c.name.startsWith('.AspNetCore.Cookies'));
      expect(auth, 'auth cookie must be present after login').toBeTruthy();
      expect(auth.httpOnly, 'auth cookie must be HttpOnly').toBe(true);
      // Playwright normalizes SameSite to "Lax" / "Strict" / "None".
      expect(auth.sameSite, 'auth cookie SameSite must be Lax').toBe('Lax');
      // ExpireTimeSpan was set to 8h finite — assert it is NOT a session
      // cookie (Playwright reports session cookies as expires === -1).
      expect(auth.expires, 'auth cookie must not be a session cookie').toBeGreaterThan(0);
    } finally {
      await page.close();
      await ctx.close();
    }
  });

  test('Auth cookie posture — Secure flag set under https scheme (production simulation)', async () => {
    // Production simulation: in dev the cookie policy is SameAsRequest so
    // when the request scheme is https the auth cookie must carry Secure.
    // We force https via X-Forwarded-Proto (UseForwardedHeaders is wired in
    // Program.cs), which is exactly what an Autoscale / reverse-proxy
    // deployment looks like in production.
    const req = await pwRequest.newContext({
      baseURL: BASE,
      extraHTTPHeaders: { 'X-Forwarded-Proto': 'https' },
    });
    try {
      const get = await req.get('/Account/Login');
      expect(get.status()).toBe(200);
      const html = await get.text();
      const tokenMatch = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
      expect(tokenMatch, 'antiforgery token must be present on login form').toBeTruthy();
      const token = tokenMatch[1];

      const post = await req.post('/Account/Login', {
        form: {
          Username: 'admin',
          Password: 'admin123',
          __RequestVerificationToken: token,
        },
        maxRedirects: 0,
        failOnStatusCode: false,
      });
      // Successful login redirects (302/303); failure renders the form back (200).
      expect([302, 303]).toContain(post.status());

      const setCookies = post.headersArray()
        .filter(h => h.name.toLowerCase() === 'set-cookie')
        .map(h => h.value);
      const auth = setCookies.find(c => c.startsWith('.AspNetCore.Cookies='));
      expect(auth, 'auth Set-Cookie must be issued on success').toBeTruthy();
      // Production hardening contract:
      expect(auth, 'Secure flag must be set under https scheme').toMatch(/;\s*secure(?:;|\s|$)/i);
      expect(auth, 'HttpOnly flag must be set').toMatch(/;\s*httponly(?:;|\s|$)/i);
      expect(auth, 'SameSite=Lax must be set').toMatch(/;\s*samesite=lax/i);
      // Finite expiry (8h ExpireTimeSpan) — must include an expires/max-age.
      expect(auth, 'auth cookie must not be a session cookie').toMatch(/expires=|max-age=/i);
    } finally {
      await req.dispose();
    }
  });

  test('Rate limiter fail-open log redaction — partition key is hashed, not raw', () => {
    // Static-source assertion: PostgresLoginRateLimiter must hash the
    // partition key before logging it on the fail-open path. This guards
    // against a future refactor that re-introduces raw username/IP in
    // logs without anyone noticing in code review.
    const src = fs.readFileSync(
      path.resolve(__dirname, '..', 'Services/RateLimiting/PostgresLoginRateLimiter.cs'),
      'utf8');
    expect(src).toMatch(/SHA256\.HashData/);
    expect(src).toMatch(/keyHash=\{KeyHash\}/);
    // The old leaky pattern must be gone.
    expect(src).not.toMatch(/partitionKey\.Substring\(0,\s*24\)/);
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
