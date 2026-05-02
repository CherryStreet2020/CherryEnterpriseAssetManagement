const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:5000';
const SS_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');

const REDIRECT_MAP = {
  '/Admin/Locations': '/Assets/Locations',
  '/Admin/Vendors': '/Materials/Vendors',
  '/Admin/PMTemplates': '/Maintenance/PMTemplates',
  '/Admin/PMScheduleEdit': '/Maintenance/PMScheduleEdit',
  '/Admin/GlAccounts': '/Books/GlAccounts/1',
};

test.describe('04 — Canonical Route Redirects', () => {
  test.beforeAll(() => {
    fs.mkdirSync(SS_DIR, { recursive: true });
  });

  for (const [legacy, canonical] of Object.entries(REDIRECT_MAP)) {
    test(`${legacy} returns 301 redirect to ${canonical}`, async ({ request }) => {
      const resp = await request.get(`${BASE}${legacy}`, {
        maxRedirects: 0,
      });
      expect(resp.status()).toBe(301);
      const location = resp.headers()['location'];
      expect(location).toBe(canonical);
    });
  }

  test('canonical routes return 200 after auth', async ({ page }) => {
    await page.goto(`${BASE}/Account/Login`);
    await page.waitForLoadState('domcontentloaded');
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    for (const canonical of Object.values(REDIRECT_MAP)) {
      const resp = await page.goto(`${BASE}${canonical}`);
      expect(resp.status()).toBe(200);
    }
    await page.screenshot({ path: path.join(SS_DIR, '04_canonical_200.png') });
  });
});
