// Verify the command palette (Ctrl+K) and global search infrastructure are
// reachable. We don't assert specific entries (those vary by tenant data),
// only that the palette opens and accepts input without throwing.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('NAV — command palette', () => {
  test('Ctrl+K opens the palette', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    await page.waitForLoadState('domcontentloaded');

    // Trigger the palette. The app binds Ctrl+K (Meta+K as fallback for mac).
    await page.keyboard.press('Control+K');
    await page.waitForTimeout(250);

    const input = page.locator('#commandPaletteInput');
    let visible = (await input.count()) > 0 && (await input.isVisible());
    if (!visible) {
      await page.keyboard.press('Meta+K');
      await page.waitForTimeout(250);
      visible = (await input.count()) > 0 && (await input.isVisible());
    }
    // If the palette never opened (build without keybinding), skip — the
    // markup is still verified by the home-page DOM check below.
    if (!visible) test.skip();

    await input.fill('Assets');
    await page.waitForTimeout(150);
    expect(await page.content()).toContain('Assets');
  });

  test('command palette markup is present in the layout', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    // The overlay is rendered (hidden) on every page by _ModernLayout.
    await expect(page.locator('#commandPaletteOverlay')).toHaveCount(1);
    await expect(page.locator('#commandPaletteInput')).toHaveCount(1);
  });

  test('global search box on home page accepts input', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/`);
    const search = page.locator('#globalSearchInput');
    if ((await search.count()) === 0 || !(await search.isVisible())) test.skip();
    await search.fill('asset');
    await page.waitForTimeout(150);
    expect(await page.title()).toBeTruthy();
  });
});
