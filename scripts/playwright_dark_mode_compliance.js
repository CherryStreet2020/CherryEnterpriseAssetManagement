const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE = process.env.BASE_URL || 'http://localhost:5000';
const SCREENSHOT_DIR = path.join(__dirname, '..', 'proof', 'ui', 'playwright');
const LIGHT_DIR = path.join(SCREENSHOT_DIR, 'after-light');
const DARK_DIR = path.join(SCREENSHOT_DIR, 'after-dark');

fs.mkdirSync(LIGHT_DIR, { recursive: true });
fs.mkdirSync(DARK_DIR, { recursive: true });

const pages = [
  { name: 'login', path: '/Account/Login' },
  { name: 'dashboard', path: '/' },
  { name: 'asset-detail-general', path: '/Assets/Asset/100' },
  { name: 'asset-detail-location', path: '/Assets/Asset/100?tab=location' },
  { name: 'cip-index', path: '/CIP' },
  { name: 'cip-details', path: '/CIP/Details/1' },
];

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    recordVideo: undefined,
  });

  const traceDir = path.join(SCREENSHOT_DIR, 'trace');
  fs.mkdirSync(traceDir, { recursive: true });
  await context.tracing.start({ screenshots: true, snapshots: true });

  for (const pg of pages) {
    const page = await context.newPage();
    const url = BASE + pg.path;
    console.log(`[LIGHT] ${pg.name}: ${url}`);
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
      await page.evaluate(() => {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('cherryai_theme', 'light');
      });
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(LIGHT_DIR, `${pg.name}.png`), fullPage: false });
    } catch (e) {
      console.error(`  ERROR: ${e.message}`);
      await page.screenshot({ path: path.join(LIGHT_DIR, `${pg.name}_error.png`), fullPage: false }).catch(() => {});
    }
    await page.close();
  }

  for (const pg of pages) {
    const page = await context.newPage();
    const url = BASE + pg.path;
    console.log(`[DARK] ${pg.name}: ${url}`);
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
      await page.evaluate(() => {
        document.documentElement.classList.add('dark');
        localStorage.setItem('cherryai_theme', 'dark');
      });
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(DARK_DIR, `${pg.name}.png`), fullPage: false });
    } catch (e) {
      console.error(`  ERROR: ${e.message}`);
      await page.screenshot({ path: path.join(DARK_DIR, `${pg.name}_error.png`), fullPage: false }).catch(() => {});
    }
    await page.close();
  }

  await context.tracing.stop({ path: path.join(SCREENSHOT_DIR, 'trace.zip') });
  await browser.close();
  console.log('\nPlaywright capture complete.');
  console.log(`Light screenshots: ${fs.readdirSync(LIGHT_DIR).length}`);
  console.log(`Dark screenshots: ${fs.readdirSync(DARK_DIR).length}`);
  console.log(`Trace: ${path.join(SCREENSHOT_DIR, 'trace.zip')}`);
})();
