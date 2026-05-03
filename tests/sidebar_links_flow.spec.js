// Verify that every link rendered in the main sidebar resolves to a 2xx/3xx
// response (no 4xx/5xx). This catches dead links left over after refactors.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('NAV — every sidebar link resolves', () => {
  test('crawl sidebar', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    const hrefs = await page
      .locator('.sidebar-nav a[href], aside a[href], nav a[href]')
      .evaluateAll((els) =>
        els
          .map((el) => el.getAttribute('href'))
          .filter(
            (h) =>
              h &&
              h.startsWith('/') &&
              !h.startsWith('/Account/Logout') &&
              !h.includes('mailto:') &&
              !h.startsWith('javascript:')
          )
      );

    const unique = Array.from(new Set(hrefs));
    expect(unique.length, 'sidebar produced no links').toBeGreaterThan(0);

    const failures = [];
    for (const href of unique) {
      const resp = await page.request.get(`${BASE}${href}`, { maxRedirects: 5 });
      if (resp.status() >= 400) failures.push(`${href} → ${resp.status()}`);
    }
    expect(failures, `dead sidebar links:\n${failures.join('\n')}`).toEqual([]);
  });
});
