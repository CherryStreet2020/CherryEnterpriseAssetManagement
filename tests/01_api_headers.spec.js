const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:5000';
const ORG_UUID = 'a0000000-0000-0000-0000-000000000001';
const LOG_DIR = path.join(__dirname, '..', 'PROXY_LOGS');
const LOG_FILE = path.join(LOG_DIR, 'api_requests_headers_verified.log');

test.describe('01 — API Headers & Base URL', () => {
  test('all /api/v1 requests include required headers and use correct base URL', async ({ page }) => {
    const captured = [];

    page.on('request', (req) => {
      const url = req.url();
      if (url.includes('/api/v1')) {
        const headers = req.headers();
        captured.push({
          method: req.method(),
          url: url,
          'x-tenant-id': headers['x-tenant-id'] || 'MISSING',
          'x-user-id': headers['x-user-id'] || 'MISSING',
          'x-org-node-id': headers['x-org-node-id'] || 'MISSING',
        });
      }
    });

    await page.goto(`${BASE}/Account/Login`);
    await page.waitForLoadState('domcontentloaded');
    await page.fill('input[name="Username"]', 'admin');
    await page.fill('input[name="Password"]', 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('domcontentloaded');

    const pages = [
      '/', '/Assets', '/Assets/Locations', '/Maintenance',
      '/Maintenance/PMTemplates', '/Materials/Vendors', '/Materials/Items',
      '/CIP', '/Purchasing', '/WorkOrders', '/Receiving',
      '/Books', '/Journals', '/Admin/Sites', '/Maintenance/Schedules',
      '/Maintenance/Assignments', '/Maintenance/WorkRequests',
      '/AccountsPayable', '/Inventory',
    ];
    for (const p of pages) {
      await page.goto(`${BASE}${p}`);
      await page.waitForLoadState('networkidle');
    }

    fs.mkdirSync(LOG_DIR, { recursive: true });
    const lines = captured.map(
      (c) => `${c.method} ${c.url} | X-Tenant-Id: ${c['x-tenant-id']} | X-User-Id: ${c['x-user-id']} | X-Org-Node-Id: ${c['x-org-node-id']}`
    );
    fs.writeFileSync(LOG_FILE, lines.join('\n') + '\n');

    for (const req of captured) {
      expect(req.url).toMatch(/^http:\/\/127\.0\.0\.1:5000\/api\/v1\//);
      expect(req['x-tenant-id']).toBe('default');
      expect(req['x-user-id']).toBe('system@localhost');
      expect(req['x-org-node-id']).toBe(ORG_UUID);
    }

    expect(lines.length).toBeGreaterThanOrEqual(20);
  });
});
