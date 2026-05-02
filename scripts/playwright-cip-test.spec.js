const { test, expect } = require('@playwright/test');
const path = require('path');
const fs = require('fs');

const SCREENSHOT_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');

test.beforeAll(async () => {
  fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
});

test('CIP E2E Workflow Visual Proof', async ({ page }) => {
  test.setTimeout(120000);

  await page.goto('http://127.0.0.1:5000/CIP', { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(2000);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '01_cip_index.png'), fullPage: false });

  const cipRow = page.locator('text=CIP-E2E-0001');
  if (await cipRow.count() > 0) {
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '02_cip_index_with_project.png'), fullPage: false });
  } else {
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '02_cip_index_no_project_visible.png'), fullPage: false });
  }

  await page.goto('http://127.0.0.1:5000/CIP/Details/7', { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(2000);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '03_cip_details_header.png'), fullPage: false });

  const costSection = page.locator('text=Cost by Type');
  if (await costSection.count() > 0) {
    await costSection.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);
  }
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '04_cip_cost_type_tiles.png'), fullPage: false });

  await page.evaluate(() => window.scrollTo(0, 800));
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '05_cip_project_details_budget.png'), fullPage: false });

  await page.evaluate(() => window.scrollTo(0, 1600));
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '06_cip_ledger_section.png'), fullPage: false });

  await page.evaluate(() => window.scrollTo(0, 2400));
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '07_cip_related_objects.png'), fullPage: false });

  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '08_cip_bottom_capitalized_state.png'), fullPage: false });

  await page.goto('http://127.0.0.1:5000/CIP', { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, '09_cip_index_return.png'), fullPage: false });
});
