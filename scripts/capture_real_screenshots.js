const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const SCREENSHOT_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright', 'screenshots');
fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage'],
  });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 900 },
    recordVideo: undefined,
  });
  
  await context.tracing.start({ screenshots: true, snapshots: true, sources: true });
  
  const page = await context.newPage();
  const BASE = 'http://127.0.0.1:5000';

  async function shot(name) {
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, name), fullPage: false });
    const stat = fs.statSync(path.join(SCREENSHOT_DIR, name));
    console.log(`  ${name}: ${stat.size} bytes`);
  }

  console.log('1. CIP Index (Org A - PWH Holdings)');
  await page.goto(`${BASE}/CIP`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('01_cip_index_orgA.png');

  console.log('2. CIP Details for project 7');
  await page.goto(`${BASE}/CIP/Details/7`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('02_cip_details_header.png');

  console.log('3. Cost type tiles');
  await page.evaluate(() => window.scrollTo(0, 350));
  await shot('03_cip_cost_type_tiles.png');

  console.log('4. Project details + budget');
  await page.evaluate(() => window.scrollTo(0, 700));
  await shot('04_cip_project_details_budget.png');

  console.log('5. Ledger section');
  await page.evaluate(() => window.scrollTo(0, 1200));
  await shot('05_cip_ledger_costs.png');

  console.log('6. Related objects');
  await page.evaluate(() => window.scrollTo(0, 1800));
  await shot('06_cip_related_objects.png');

  console.log('7. Capitalization state');
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
  await shot('07_cip_capitalized_state.png');

  console.log('8. Party Drilldown (All Companies)');
  await page.goto(`${BASE}/CIP/PartyDrilldown`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('08_party_drilldown_all.png');

  console.log('9. Party Drilldown scroll down');
  await page.evaluate(() => window.scrollTo(0, 400));
  await shot('09_party_drilldown_scroll.png');

  console.log('10. Assets page');
  await page.goto(`${BASE}/Assets`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('10_assets_list.png');

  console.log('11. Vendors page');
  await page.goto(`${BASE}/Vendors`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('11_vendors_list.png');

  console.log('12. Work Orders page');
  await page.goto(`${BASE}/WorkOrders`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('12_work_orders.png');

  console.log('13. Purchasing page');
  await page.goto(`${BASE}/Purchasing`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('13_purchasing_list.png');

  console.log('14. CIP Index return');
  await page.goto(`${BASE}/CIP`, { waitUntil: 'networkidle', timeout: 30000 });
  await shot('14_cip_index_return.png');

  const traceDir = path.join(__dirname, '..', 'proof', 'ui', 'playwright');
  await context.tracing.stop({ path: path.join(traceDir, 'trace.zip') });
  
  await browser.close();
  console.log('Done. Trace saved.');
})();
