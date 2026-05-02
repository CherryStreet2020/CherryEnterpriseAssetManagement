const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE = 'http://127.0.0.1:5000';
const ROOT = path.join(__dirname, '..');
const SS_LIGHT = path.join(ROOT, 'proof/ui/playwright/screenshots/after/light');
const SS_DARK = path.join(ROOT, 'proof/ui/playwright/screenshots/after/dark');
const TRACE_PATH = path.join(ROOT, 'proof/ui/playwright/trace.zip');

fs.mkdirSync(SS_LIGHT, { recursive: true });
fs.mkdirSync(SS_DARK, { recursive: true });

const PAGES = [
  { name: 'login', path: '/Account/Login' },
  { name: 'dashboard', path: '/' },
  { name: 'asset-detail-tabs', path: '/Assets/Asset/100' },
  { name: 'asset-detail-location', path: '/Assets/Asset/100?tab=location' },
  { name: 'cip-index', path: '/CIP' },
  { name: 'cip-details', path: '/CIP/Details/1' },
];

(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
  });

  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  await context.tracing.start({ screenshots: true, snapshots: true, sources: true });

  console.log('=== LIGHT MODE SCREENSHOTS ===');
  for (const pg of PAGES) {
    const page = await context.newPage();
    try {
      await page.goto(BASE + pg.path, { timeout: 15000, waitUntil: 'networkidle' });
      await page.evaluate(() => {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('cherryai_theme', 'light');
      });
      await page.waitForTimeout(300);
      const ssPath = path.join(SS_LIGHT, `${pg.name}.png`);
      await page.screenshot({ path: ssPath, fullPage: true });
      const size = fs.statSync(ssPath).size;
      console.log(`  [OK] ${pg.name}.png (${(size/1024).toFixed(1)} KB)`);
    } catch (e) {
      console.error(`  [ERR] ${pg.name}: ${e.message.substring(0, 100)}`);
    }
    await page.close();
  }

  console.log('\n=== DARK MODE SCREENSHOTS ===');
  for (const pg of PAGES) {
    const page = await context.newPage();
    try {
      await page.goto(BASE + pg.path, { timeout: 15000, waitUntil: 'networkidle' });
      await page.evaluate(() => {
        document.documentElement.classList.add('dark');
        localStorage.setItem('cherryai_theme', 'dark');
      });
      await page.waitForTimeout(500);
      const ssPath = path.join(SS_DARK, `${pg.name}.png`);
      await page.screenshot({ path: ssPath, fullPage: true });
      const size = fs.statSync(ssPath).size;
      console.log(`  [OK] ${pg.name}.png (${(size/1024).toFixed(1)} KB)`);
    } catch (e) {
      console.error(`  [ERR] ${pg.name}: ${e.message.substring(0, 100)}`);
    }
    await page.close();
  }

  console.log('\n=== STOPPING TRACE ===');
  await context.tracing.stop({ path: TRACE_PATH });
  const traceSize = fs.statSync(TRACE_PATH).size;
  console.log(`  trace.zip: ${(traceSize/1024).toFixed(1)} KB`);

  await browser.close();

  console.log('\n=== SUMMARY ===');
  const lightFiles = fs.readdirSync(SS_LIGHT).filter(f => f.endsWith('.png'));
  const darkFiles = fs.readdirSync(SS_DARK).filter(f => f.endsWith('.png'));
  console.log(`  Light PNGs: ${lightFiles.length}`);
  console.log(`  Dark PNGs: ${darkFiles.length}`);
  console.log(`  Trace: ${(traceSize/1024).toFixed(1)} KB`);
  console.log('  DONE');
})();
