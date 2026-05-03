// Help center + AI assistant index pages render. We don't try to exercise
// the OpenAI round-trip here (network/cost) — only that the surfaces load.
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('HELP / AI — surfaces render', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of ['/Help', '/Help/Implementation', '/Help/Tasks', '/Help/Topic', '/AI']) {
    test(`renders ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp.status(), `${path} returned ${resp.status()}`).toBe(200);
    });
  }

  test('AI page exposes an input or chat affordance', async ({ page }) => {
    await page.goto(`${BASE}/AI`);
    const inputs = await page
      .locator('textarea, input[type="text"], [contenteditable="true"]')
      .count();
    expect(inputs).toBeGreaterThan(0);
  });
});
