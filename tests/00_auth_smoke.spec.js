const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:5000';
const SS_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');

test.describe('00 — Auth Smoke', () => {
  test.beforeAll(() => {
    fs.mkdirSync(SS_DIR, { recursive: true });
  });

  test('unauth user is redirected to sign-in from protected route', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Admin/Users`);
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Account/Login');
    await page.screenshot({ path: path.join(SS_DIR, '00_unauth_redirect.png') });
  });

  test('login with valid credentials works', async ({ page }) => {
    await page.goto(`${BASE}/Account/Login`);
    await page.waitForLoadState('domcontentloaded');
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).not.toContain('/Account/Login');
    await page.screenshot({ path: path.join(SS_DIR, '00_login_success.png') });
  });

  test('post-login redirect returns to the protected route', async ({ page }) => {
    const protectedRoute = '/Admin/Users';
    await page.goto(`${BASE}${protectedRoute}`);
    await page.waitForLoadState('domcontentloaded');
    if (page.url().includes('/Account/Login')) {
      await page.fill('input[name="Username"]', 'admin');
      await page.fill('input[name="Password"]', 'admin123');
      await page.click('button[type="submit"]');
      await page.waitForLoadState('domcontentloaded');
    }
    expect(page.url()).toContain('/Admin/Users');
    await page.screenshot({ path: path.join(SS_DIR, '00_post_login_redirect.png') });
  });

  test('logout works', async ({ page }) => {
    await page.goto(`${BASE}/Account/Login`);
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');
    await page.goto(`${BASE}/Account/Logout`);
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Account/Login');
    await page.screenshot({ path: path.join(SS_DIR, '00_logout.png') });
  });

  test('after logout, protected route redirects to sign-in again', async ({ page }) => {
    await page.goto(`${BASE}/Account/Login`);
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');
    await page.goto(`${BASE}/Account/Logout`);
    await page.waitForLoadState('domcontentloaded');
    await page.goto(`${BASE}/Admin/Users`);
    await page.waitForLoadState('domcontentloaded');
    expect(page.url()).toContain('/Account/Login');
    await page.screenshot({ path: path.join(SS_DIR, '00_post_logout_redirect.png') });
  });
});
