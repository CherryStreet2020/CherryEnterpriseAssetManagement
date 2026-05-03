const { test, expect } = require('@playwright/test');
const { dbOne, login, BASE } = require('./_helpers');

// Pick real seed IDs once per worker so no test in this file depends on
// a hardcoded `/Asset/100` or `/CIP/Details/1` that drifts with re-seeding.
let _ids = null;
async function getIds() {
  if (_ids) return _ids;
  const asset = await dbOne('SELECT "Id" FROM "Assets" WHERE "Active"=true ORDER BY "Id" LIMIT 1');
  const cip = await dbOne('SELECT "Id" FROM "CipProjects" ORDER BY "Id" LIMIT 1');
  _ids = { assetId: asset ? asset.Id : null, cipId: cip ? cip.Id : null };
  return _ids;
}

test.describe('Dark Mode Compliance', () => {
  test('light mode pages render correctly', async ({ page }) => {
    // Title regex tolerates white-label brands. The app is currently branded
    // "ABS Machining EAM"; the underlying platform is "CherryAI".
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveTitle(/CherryAI|EAM|Login/i);

    await page.goto(`${BASE}/Account/Login`, { waitUntil: 'domcontentloaded' });
    // Login page must render its sign-in form regardless of brand copy.
    await expect(page.locator('input[name="Username"]')).toBeVisible();
    await expect(page.locator('input[name="Password"]')).toBeVisible();

    await login(page);
    const { assetId, cipId } = await getIds();

    if (assetId) {
      await page.goto(`${BASE}/Assets/Asset/${assetId}`, { waitUntil: 'domcontentloaded' });
      await expect(page.locator('h1, .screen-header__title').first()).toBeVisible();
    }

    await page.goto(`${BASE}/CIP`, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('h1, .screen-header__title').first()).toBeVisible();

    if (cipId) {
      await page.goto(`${BASE}/CIP/Details/${cipId}`, { waitUntil: 'domcontentloaded' });
      await expect(page.locator('h1, .screen-header__title').first()).toBeVisible();
    }
  });

  test('dark mode surfaces are not white', async ({ page }) => {
    await login(page);
    const { assetId } = await getIds();

    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await page.evaluate(() => {
      document.documentElement.classList.add('dark');
      localStorage.setItem('cherryai_theme', 'dark');
    });
    await page.waitForTimeout(300);

    const bgColor = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    expect(bgColor).not.toBe('rgb(255, 255, 255)');

    if (assetId) {
      await page.goto(`${BASE}/Assets/Asset/${assetId}`, { waitUntil: 'domcontentloaded' });
      await page.evaluate(() => document.documentElement.classList.add('dark'));
      await page.waitForTimeout(300);
      const assetBg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
      expect(assetBg).not.toBe('rgb(255, 255, 255)');
    }
  });

  test('theme CSS stack is loaded on all pages', async ({ page }) => {
    await login(page);
    const { assetId, cipId } = await getIds();
    const pages = ['/', '/Account/Login', '/CIP'];
    if (assetId) pages.push(`/Assets/Asset/${assetId}`);
    if (cipId) pages.push(`/CIP/Details/${cipId}`);

    for (const p of pages) {
      await page.goto(BASE + p, { waitUntil: 'domcontentloaded' });
      const hasTheme = await page.evaluate(() => {
        const links = [...document.querySelectorAll('link[rel="stylesheet"]')];
        const hrefs = links.map(l => l.getAttribute('href') || '');
        return hrefs.some(h => h.includes('cherryai-theme.css')) &&
               hrefs.some(h => h.includes('cherryai-dark-compliance.css')) &&
               hrefs.some(h => h.includes('tokens.css'));
      });
      expect(hasTheme).toBe(true);
    }
  });
});
