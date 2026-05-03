const http = require('http');

const BASE = 'http://127.0.0.1:5000';

const WARMUP_PATHS = [
  '/Account/Login',
  '/',
  '/Assets',
  '/Assets/Locations',
  '/Admin',
  '/Admin/Barcodes',
  '/Reports',
  '/Inventory',
];

function get(path, timeoutMs) {
  return new Promise((resolve) => {
    const req = http.get(BASE + path, { timeout: timeoutMs }, (res) => {
      res.resume();
      res.on('end', () => resolve({ path, status: res.statusCode }));
    });
    req.on('error', () => resolve({ path, status: 0 }));
    req.on('timeout', () => { req.destroy(); resolve({ path, status: -1 }); });
  });
}

module.exports = async () => {
  const deadline = Date.now() + 90_000;
  let ok = false;
  while (Date.now() < deadline) {
    const r = await get('/Account/Login', 5_000);
    if (r.status && r.status < 500) { ok = true; break; }
    await new Promise(r => setTimeout(r, 1_000));
  }
  if (!ok) {
    console.error('[globalSetup] Web Server not reachable on :5000 within 90s');
    process.exit(1);
  }
  for (const p of WARMUP_PATHS) {
    const r = await get(p, 30_000);
    console.log(`[globalSetup] warmed ${p} -> ${r.status}`);
  }
};
