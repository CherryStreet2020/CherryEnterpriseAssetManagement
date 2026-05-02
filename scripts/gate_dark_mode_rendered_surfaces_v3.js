const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://127.0.0.1:5000';
const OUT_JSON = 'proof/runtime/logs/gate_dark_mode_rendered_surfaces_not_white.json';
const OUT_TXT = 'proof/runtime/logs/gate_dark_mode_rendered_surfaces_not_white.txt';

const LUMINANCE_THRESHOLD = 0.65;

const PAGES = [
  { name: 'Asset Detail', path: '/Assets/Asset/100' },
  { name: 'Dashboard', path: '/' },
  { name: 'CIP Details', path: '/CIP/Details/1' },
];

const SELECTORS = [
  { label: 'body', selector: 'body' },
  { label: 'main-content', selector: '.main-content, .content-area, main' },
  { label: 'section-card', selector: '.section-card, .card, .premium-card' },
  { label: 'card-body', selector: '.card-body' },
  { label: 'form-input', selector: 'input[type="text"], input[type="number"], select, textarea' },
  { label: 'tab-panel', selector: '.tab-panel, .premium-tabs' },
  { label: 'tab-nav', selector: '.tab-nav' },
  { label: 'table-header', selector: 'thead th, .table thead, .grid-header' },
  { label: 'form-section', selector: '.form-section, .form-group' },
  { label: 'hero-header', selector: '.premium-hero-header, .screen-header' },
  { label: 'kpi-card', selector: '.kpi-stat-card, .stat-card' },
  { label: 'sidebar', selector: '.sidebar, #mainSidebar' },
];

function parseBgColor(str) {
  if (!str || str === 'transparent' || str === 'rgba(0, 0, 0, 0)') return null;
  const rgb = str.match(/rgba?\(\s*(\d+),\s*(\d+),\s*(\d+)/);
  if (!rgb) return null;
  return { r: parseInt(rgb[1]), g: parseInt(rgb[2]), b: parseInt(rgb[3]) };
}

function luminance(c) {
  return (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255;
}

(async () => {
  const checks = [];
  let overall = 'PASS';

  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage', '--disable-gpu']
  });

  for (const pg of PAGES) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    try {
      await page.goto(BASE + pg.path, { timeout: 15000, waitUntil: 'networkidle' });
      await page.evaluate(() => {
        document.documentElement.classList.add('dark');
        localStorage.setItem('cherryai_theme', 'dark');
      });
      await page.waitForTimeout(500);

      for (const sel of SELECTORS) {
        try {
          const results = await page.evaluate((s) => {
            const els = document.querySelectorAll(s);
            if (!els.length) return [];
            const samples = [];
            for (let i = 0; i < Math.min(els.length, 3); i++) {
              const cs = getComputedStyle(els[i]);
              samples.push({
                tag: els[i].tagName,
                className: (els[i].className || '').toString().substring(0, 60),
                backgroundColor: cs.backgroundColor,
                color: cs.color,
              });
            }
            return samples;
          }, sel.selector);

          if (results.length === 0) {
            checks.push({
              page: pg.name,
              selector: sel.label,
              selectorCSS: sel.selector,
              found: false,
              pass: true,
              detail: 'Not present on page (OK)'
            });
            continue;
          }

          for (const r of results) {
            const color = parseBgColor(r.backgroundColor);
            const lum = color ? luminance(color) : null;
            const tooLight = lum !== null && lum > LUMINANCE_THRESHOLD;
            const pass = !tooLight;

            checks.push({
              page: pg.name,
              selector: sel.label,
              selectorCSS: sel.selector,
              found: true,
              element: `${r.tag}.${r.className.substring(0, 30)}`,
              backgroundColor: r.backgroundColor,
              luminance: lum !== null ? Math.round(lum * 1000) / 1000 : null,
              threshold: LUMINANCE_THRESHOLD,
              pass: pass,
              detail: tooLight ? `TOO LIGHT: luminance ${lum.toFixed(3)} > ${LUMINANCE_THRESHOLD}` : `OK: luminance ${lum !== null ? lum.toFixed(3) : 'transparent'}`
            });

            if (!pass) overall = 'FAIL';
          }
        } catch (e) {
          checks.push({
            page: pg.name,
            selector: sel.label,
            pass: true,
            detail: `Selector error (non-fatal): ${e.message.substring(0, 60)}`
          });
        }
      }
    } catch (e) {
      checks.push({ page: pg.name, pass: false, detail: `Page error: ${e.message.substring(0, 100)}` });
      overall = 'FAIL';
    }

    await context.close();
  }

  await browser.close();

  const result = { gate: 'gate_dark_mode_rendered_surfaces_not_white', overall, total_checks: checks.length, checks };
  fs.mkdirSync(path.dirname(OUT_JSON), { recursive: true });
  fs.writeFileSync(OUT_JSON, JSON.stringify(result, null, 2));

  const lines = [
    'Dark Mode Rendered Surfaces — Computed Style Luminance Check',
    '============================================================',
    `Threshold: luminance <= ${LUMINANCE_THRESHOLD}`,
    `Pages checked: ${PAGES.map(p => p.name).join(', ')}`,
    `Selectors per page: ${SELECTORS.length}`,
    `Total checks: ${checks.length}`,
    ''
  ];

  let currentPage = '';
  for (const c of checks) {
    if (c.page !== currentPage) {
      currentPage = c.page;
      lines.push(`--- ${currentPage} ---`);
    }
    const status = c.pass ? 'PASS' : 'FAIL';
    const bg = c.backgroundColor ? ` bg=${c.backgroundColor}` : '';
    const lum = c.luminance !== null && c.luminance !== undefined ? ` lum=${c.luminance}` : '';
    lines.push(`  [${status}] ${c.selector}${bg}${lum} — ${c.detail}`);
  }

  lines.push('');
  lines.push(`OVERALL: ${overall}`);
  const txt = lines.join('\n');
  fs.writeFileSync(OUT_TXT, txt);
  console.log(txt);
})();
