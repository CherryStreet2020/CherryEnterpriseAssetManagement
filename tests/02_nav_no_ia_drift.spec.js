const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { BASE, login } = require('./_helpers');

const SS_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');

const OPERATIONAL_PAGES = [
  '/',
  '/Assets',
  '/Assets/Locations',
  '/Maintenance',
  '/Maintenance/Schedules',
  '/Maintenance/WorkRequests',
  '/CIP',
  '/Journals',
  '/Purchasing',
  '/AccountsPayable',
  '/BulkOperations',
  '/Books',
  '/Reports/ChartOfAccounts',
];

test.describe('02 — Navigation IA Drift Check', () => {
  test.beforeAll(() => {
    fs.mkdirSync(SS_DIR, { recursive: true });
  });

  test('sidebar operational groups do NOT point to /Admin routes', async ({ page }) => {
    await login(page);
    await page.goto(BASE);
    await page.waitForLoadState('domcontentloaded');

    const ADMIN_SECTION_ALLOWED = [
      '/Admin', '/Admin/Sites', '/Admin/Users', '/Admin/Lookups',
      '/Admin/Integrations', '/Admin/AuditLog', '/Admin/SystemSettings',
      '/Admin/DataImport',
    ];

    const sidebarLinks = await page.locator('.sidebar-nav a[href]').evaluateAll(els =>
      els.map(el => el.getAttribute('href')).filter(h => h)
    );
    const drifted = sidebarLinks.filter(h =>
      h.includes('/Admin/') && !ADMIN_SECTION_ALLOWED.includes(h)
    );
    expect(drifted, `Sidebar has /Admin/ drift in operational groups: ${drifted.join(', ')}`).toHaveLength(0);
    await page.screenshot({ path: path.join(SS_DIR, '02_sidebar_no_admin_drift.png') });
  });

  test('module pills do NOT point to /Admin routes', async ({ page }) => {
    await login(page);
    await page.goto(`${BASE}/Maintenance`);
    await page.waitForLoadState('domcontentloaded');

    const pillLinks = await page.locator('.module-pills a[href], .nav-pills a[href], .pill-nav a[href]').evaluateAll(els =>
      els.map(el => el.getAttribute('href')).filter(h => h)
    );
    const adminPills = pillLinks.filter(h => h.includes('/Admin/'));
    expect(adminPills, `Module pills have /Admin/ links: ${adminPills.join(', ')}`).toHaveLength(0);
    await page.screenshot({ path: path.join(SS_DIR, '02_module_pills_no_admin.png') });
  });

  test('command palette routes do NOT include /Admin for operational items', async ({ page }) => {
    await login(page);
    await page.goto(BASE);
    await page.waitForLoadState('domcontentloaded');
    await page.screenshot({ path: path.join(SS_DIR, '02_command_palette_check.png') });
  });

  for (const route of OPERATIONAL_PAGES) {
    const safeName = route.replace(/\//g, '_') || '_root';
    test(`no /Admin/ links in main content on ${route}`, async ({ page }) => {
      await login(page);
      const resp = await page.goto(`${BASE}${route}`);
      if (!resp || resp.status() >= 400) return;
      await page.waitForLoadState('domcontentloaded');

      const mainContent = page.locator('.main-content, main, .page-content, .content-wrapper').first();
      const scope = (await mainContent.count() > 0) ? mainContent : page;

      const hrefLinks = await scope.locator('a[href*="/Admin/"]:not(.sidebar-nav a)').evaluateAll(els =>
        els.map(el => {
          let p = el.parentElement;
          while (p) {
            if (p.classList && (p.classList.contains('sidebar-nav') || p.classList.contains('sidebar'))) return null;
            p = p.parentElement;
          }
          return { href: el.getAttribute('href'), text: (el.textContent || '').trim().slice(0, 80) };
        }).filter(Boolean)
      );

      const dataRowHrefs = await scope.locator('[data-row-href*="/Admin/"]').evaluateAll(els =>
        els.map(el => el.getAttribute('data-row-href'))
      );

      const onclickAdmins = await scope.locator('[onclick*="/Admin/"]').evaluateAll(els =>
        els.map(el => {
          let p = el.parentElement;
          while (p) {
            if (p.classList && (p.classList.contains('sidebar-nav') || p.classList.contains('sidebar'))) return null;
            p = p.parentElement;
          }
          return { onclick: el.getAttribute('onclick'), text: (el.textContent || '').trim().slice(0, 80) };
        }).filter(Boolean)
      );

      const allDrift = [
        ...hrefLinks.map(l => `href="${l.href}" (${l.text})`),
        ...dataRowHrefs.map(h => `data-row-href="${h}"`),
        ...onclickAdmins.map(o => `onclick="${o.onclick}" (${o.text})`),
      ];

      expect(allDrift, `IA drift on ${route}: ${allDrift.join('; ')}`).toHaveLength(0);
      await page.screenshot({ path: path.join(SS_DIR, `02_no_admin${safeName}.png`) });
    });
  }
});
