const { Client } = require('pg');

const BASE = 'http://127.0.0.1:5000';

async function login(page) {
  await page.goto(`${BASE}/Account/Login`);
  await page.waitForLoadState('domcontentloaded');
  await page.fill('input[name="Username"]', 'admin');
  await page.fill('input[name="Password"]', 'admin123');
  await page.click('button[type="submit"]');
  await page.waitForLoadState('domcontentloaded');
}

async function gotoApp(page, path) {
  const url = path.startsWith('http') ? path : `${BASE}${path.startsWith('/') ? '' : '/'}${path}`;
  const resp = await page.goto(url);
  await page.waitForLoadState('domcontentloaded');
  return resp;
}

function pgConnString() {
  if (process.env.DATABASE_URL) return process.env.DATABASE_URL;
  const host = process.env.PGHOST || 'localhost';
  const user = process.env.PGUSER || 'postgres';
  const pass = process.env.PGPASSWORD || '';
  const db = process.env.PGDATABASE || 'postgres';
  const port = process.env.PGPORT || '5432';
  return `postgresql://${user}:${pass}@${host}:${port}/${db}`;
}

async function dbQuery(text, params = []) {
  const client = new Client({ connectionString: pgConnString() });
  await client.connect();
  try {
    const res = await client.query(text, params);
    return res.rows;
  } finally {
    await client.end();
  }
}

async function dbOne(text, params = []) {
  const rows = await dbQuery(text, params);
  return rows[0] || null;
}

module.exports = { BASE, login, gotoApp, dbQuery, dbOne };
