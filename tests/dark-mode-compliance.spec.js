const { test, expect } = require('@playwright/test');

test.describe('Dark Mode Compliance', () => {
  test('light mode pages render correctly', async ({ page }) => {
    await page.goto('http://127.0.0.1:5000/', { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveTitle(/CherryAI/);

    await page.goto('http://127.0.0.1:5000/Account/Login', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('text=Welcome Back')).toBeVisible();

    await page.goto('http://127.0.0.1:5000/Assets/Asset/100', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('h1:has-text("AST-00100")')).toBeVisible();

    await page.goto('http://127.0.0.1:5000/CIP', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('h1:has-text("Construction in Progress")')).toBeVisible();

    await page.goto('http://127.0.0.1:5000/CIP/Details/1', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('h1:has-text("Assembly Line Expansion")')).toBeVisible();
  });

  test('dark mode surfaces are not white', async ({ page }) => {
    await page.goto('http://127.0.0.1:5000/', { waitUntil: 'domcontentloaded' });
    await page.evaluate(() => {
      document.documentElement.classList.add('dark');
      localStorage.setItem('cherryai_theme', 'dark');
    });
    await page.waitForTimeout(300);

    const bgColor = await page.evaluate(() => {
      return getComputedStyle(document.body).backgroundColor;
    });
    expect(bgColor).not.toBe('rgb(255, 255, 255)');

    await page.goto('http://127.0.0.1:5000/Assets/Asset/100', { waitUntil: 'domcontentloaded' });
    await page.evaluate(() => document.documentElement.classList.add('dark'));
    await page.waitForTimeout(300);

    const assetBg = await page.evaluate(() => {
      return getComputedStyle(document.body).backgroundColor;
    });
    expect(assetBg).not.toBe('rgb(255, 255, 255)');
  });

  test('theme CSS stack is loaded on all pages', async ({ page }) => {
    const pages = ['/', '/Account/Login', '/Assets/Asset/100', '/CIP', '/CIP/Details/1'];
    for (const p of pages) {
      await page.goto('http://127.0.0.1:5000' + p, { waitUntil: 'domcontentloaded' });
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
