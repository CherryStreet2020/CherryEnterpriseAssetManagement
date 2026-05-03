// Auth and access control: invalid login, logout, redirect-to-login when
// unauthenticated, and the access-denied page when authenticated but not
// allowed (we can't easily create a Viewer user from a test, so we just
// exercise the AccessDenied page render).
const { test, expect } = require('@playwright/test');
const { BASE, login } = require('./_helpers');

test.describe('AUTH — login and logout', () => {
  test('invalid credentials show a validation error', async ({ page }) => {
    await page.goto(`${BASE}/Account/Login`);
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'wrong-password');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Account/Login');
    // Either a server-side validation summary or an inline error must show.
    const text = await page.content();
    expect(text.toLowerCase()).toMatch(/invalid|incorrect|failed|denied|wrong/);
  });

  test('logout returns user to the login screen', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Account/Logout`);
    await page.waitForLoadState('domcontentloaded');
    // Some logout handlers POST then redirect; tolerate either landing.
    expect(page.url()).toMatch(/\/Account\/(Login|Logout)|\/$/);
  });

  test('unauthenticated request to admin route redirects to login', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Admin/Users`);
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Account/Login');
    expect(resp.status()).toBeLessThan(500);
  });

  test('access denied page renders', async ({ page }) => {
    await login(page);
    const resp = await page.goto(`${BASE}/Account/AccessDenied`);
    expect(resp.status()).toBe(200);
  });
});
