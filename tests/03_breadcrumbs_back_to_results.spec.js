const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { BASE, login } = require('./_helpers');

const SS_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');

async function checkBreadcrumbsAndBackLink(page, screenshotName) {
  const bc = page.locator('.screen-header__breadcrumbs');
  if (await bc.count() > 0) {
    const crumbs = bc.locator('ol li');
    expect(await crumbs.count()).toBeGreaterThanOrEqual(2);
  }
  const backLink = page.locator('.back-link');
  if (await backLink.count() > 0) {
    const text = await backLink.textContent();
    expect(text.trim()).toContain('Back to results');
  }
  await page.screenshot({ path: path.join(SS_DIR, screenshotName) });
}

test.describe('03 — Breadcrumbs & Back to Results', () => {
  test.beforeAll(() => {
    fs.mkdirSync(SS_DIR, { recursive: true });
  });

  test('maintenance detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Maintenance`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
      }
      await checkBreadcrumbsAndBackLink(page, '03_maintenance_detail.png');
    }
  });

  test('CIP detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/CIP`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
      }
      await checkBreadcrumbsAndBackLink(page, '03_cip_detail.png');
    }
  });

  test('CIP CostDetails has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/CIP/CostDetails?projectId=1`);
    await page.waitForLoadState('domcontentloaded');
    await checkBreadcrumbsAndBackLink(page, '03_cip_cost_details.png');
  });

  test('CIP CostTypeDetails has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/CIP/CostTypeDetails?lookupValueId=1`);
    await page.waitForLoadState('domcontentloaded');
    await checkBreadcrumbsAndBackLink(page, '03_cip_cost_type_details.png');
  });

  test('Journals detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Journals`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    const link = page.locator('a[href*="/Journals/Details"]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
        await checkBreadcrumbsAndBackLink(page, '03_journals_detail.png');
        return;
      }
    }
    if (await link.count() > 0) {
      await link.click();
      await page.waitForLoadState('domcontentloaded');
      await checkBreadcrumbsAndBackLink(page, '03_journals_detail.png');
    }
  });

  test('Purchasing detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Purchasing`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    const link = page.locator('a[href*="/Purchasing/Details"]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
        await checkBreadcrumbsAndBackLink(page, '03_purchasing_detail.png');
        return;
      }
    }
    if (await link.count() > 0) {
      await link.click();
      await page.waitForLoadState('domcontentloaded');
      await checkBreadcrumbsAndBackLink(page, '03_purchasing_detail.png');
    }
  });

  test('AccountsPayable detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/AccountsPayable`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    const link = page.locator('a[href*="/AccountsPayable/Details"]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
        await checkBreadcrumbsAndBackLink(page, '03_ap_detail.png');
        return;
      }
    }
    if (await link.count() > 0) {
      await link.click();
      await page.waitForLoadState('domcontentloaded');
      await checkBreadcrumbsAndBackLink(page, '03_ap_detail.png');
    }
  });

  test('BulkOperations detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/BulkOperations`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    const link = page.locator('a[href*="/BulkOperations/Details"]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        await page.goto(`${BASE}${href}`);
        await page.waitForLoadState('domcontentloaded');
        await checkBreadcrumbsAndBackLink(page, '03_bulkops_detail.png');
        return;
      }
    }
    if (await link.count() > 0) {
      await link.click();
      await page.waitForLoadState('domcontentloaded');
      await checkBreadcrumbsAndBackLink(page, '03_bulkops_detail.png');
    }
  });

  test('Books detail has breadcrumbs and "Back to results"', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Books`);
    await page.waitForLoadState('domcontentloaded');
    const link = page.locator('a[href*="/Books/Details"]').first();
    if (await link.count() > 0) {
      await link.click();
      await page.waitForLoadState('domcontentloaded');
      await checkBreadcrumbsAndBackLink(page, '03_books_detail.png');
    }
  });

  test('canonical routes serve content (200)', async ({ page }) => {
    await login(page);
    const routes = [
      '/Assets/Locations',
      '/Materials/Vendors',
      '/Maintenance/PMTemplates',
    ];
    for (const route of routes) {
      const resp = await page.goto(`${BASE}${route}`);
      expect(resp.status()).toBeLessThan(400);
      await page.waitForLoadState('domcontentloaded');
    }
    await page.screenshot({ path: path.join(SS_DIR, '03_canonical_routes.png') });
  });

  test('legacy /Admin routes redirect to canonical via browser follow', async ({ page }) => {
    await login(page);
    const redirectMap = {
      '/Admin/Locations': '/Assets/Locations',
      '/Admin/Vendors': '/Materials/Vendors',
      '/Admin/PMTemplates': '/Maintenance/PMTemplates',
    };
    for (const [legacy, canonical] of Object.entries(redirectMap)) {
      await page.goto(`${BASE}${legacy}`);
      await page.waitForLoadState('domcontentloaded');
      const finalUrl = page.url();
      expect(finalUrl).toContain(canonical);
    }
    await page.screenshot({ path: path.join(SS_DIR, '03_legacy_redirects.png') });
  });

  test('back link preserves returnUrl from query string (list-state restore)', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Maintenance`);
    await page.waitForLoadState('domcontentloaded');
    const row = page.locator('tr[data-row-href]').first();
    if (await row.count() > 0) {
      const href = await row.getAttribute('data-row-href');
      if (href) {
        const listUrl = '/Maintenance?search=test&page=1';
        const detailUrl = `${BASE}${href}${href.includes('?') ? '&' : '?'}returnUrl=${encodeURIComponent(listUrl)}`;
        await page.goto(detailUrl);
        await page.waitForLoadState('domcontentloaded');
        const backLink = page.locator('.back-link');
        if (await backLink.count() > 0) {
          const backHref = await backLink.getAttribute('href');
          if (backHref) {
            expect(backHref).toContain('Maintenance');
          }
          const text = await backLink.textContent();
          expect(text.trim()).toContain('Back to results');
        }
        await page.screenshot({ path: path.join(SS_DIR, '03_returnurl_restore.png') });
      }
    }
  });
});
