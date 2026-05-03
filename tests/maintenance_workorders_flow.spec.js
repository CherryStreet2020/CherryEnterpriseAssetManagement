// Maintenance + work-order drill-throughs: events, work requests, technicians,
// PM templates and PM schedules.
const { test, expect } = require('@playwright/test');
const { BASE, login, pickers } = require('./_helpers');

test.describe('MAINTENANCE — drill-through', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('maintenance index renders a grid', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Maintenance`);
    expect(resp.status()).toBe(200);
    expect(await page.locator('table, .grid, .schedule-board').count()).toBeGreaterThan(0);
  });

  test('schedule board renders', async ({ page }) => {
    const resp = await page.goto(`${BASE}/Maintenance/ScheduleBoard`);
    expect(resp.status()).toBe(200);
  });

  test('work request details open', async ({ page }) => {
    const id = await pickers.workRequest();
    test.skip(!id, 'no work requests in DB');
    const resp = await page.goto(`${BASE}/Maintenance/WorkRequests/Details/${id}`);
    // 404 is acceptable when the picked row is filtered by tenant scope.
    expect(resp.status()).toBeLessThan(500);
  });

  test('maintenance event details open', async ({ page }) => {
    const id = await pickers.maintenanceEvent();
    test.skip(!id, 'no maintenance events in DB');
    const resp = await page.goto(`${BASE}/Maintenance/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('technician profile opens', async ({ page }) => {
    const id = await pickers.technician();
    test.skip(!id, 'no technicians in DB');
    const resp = await page.goto(`${BASE}/Maintenance/Technicians/Profile/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('PM template edit opens for a real template', async ({ page }) => {
    const id = await pickers.pmTemplate();
    test.skip(!id, 'no PM templates in DB');
    const resp = await page.goto(`${BASE}/Admin/PMTemplateEdit/${id}`);
    expect(resp.status()).toBe(200);
  });

  test('PM schedule edit opens for a real schedule', async ({ page }) => {
    const id = await pickers.pmSchedule();
    test.skip(!id, 'no PM schedules in DB');
    const resp = await page.goto(`${BASE}/Admin/PMScheduleEdit/${id}`);
    expect(resp.status()).toBe(200);
  });

  test('work-order details opens', async ({ page }) => {
    const id = await pickers.maintenanceEvent();
    test.skip(!id, 'no maintenance events in DB');
    const resp = await page.goto(`${BASE}/WorkOrders/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });
});
