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

async function tableExists(name) {
  const row = await dbOne(
    `SELECT 1 AS x FROM information_schema.tables WHERE table_schema='public' AND table_name=$1`,
    [name]
  );
  return !!row;
}

async function pickFirstId(table, where = '1=1', params = []) {
  if (!(await tableExists(table))) return null;
  const row = await dbOne(
    `SELECT "Id" FROM "${table}" WHERE ${where} ORDER BY "Id" LIMIT 1`,
    params
  );
  return row ? row.Id : null;
}

async function pickAsset({ rank = 0, status = 0, active = true, companyId = 1, requireLocation = false, requireDepartment = false } = {}) {
  const params = [status, active, companyId];
  let sql =
    'SELECT * FROM "Assets" WHERE "Status"=$1 AND "Active"=$2 AND "CompanyId"=$3 AND "AcquisitionCost">0';
  if (requireLocation) sql += ' AND "LocationId" IS NOT NULL';
  if (requireDepartment) sql += ' AND "DepartmentId" IS NOT NULL';
  sql += ` ORDER BY "Id" OFFSET ${Number(rank)} LIMIT 1`;
  const row = await dbOne(sql, params);
  if (!row) throw new Error(`pickAsset: no asset matched (rank=${rank}, status=${status}, companyId=${companyId})`);
  return row;
}

async function pickLookupValueId(typeKey, valueCode) {
  const row = await dbOne(
    `SELECT lv."Id" FROM "LookupValues" lv
       JOIN "LookupTypes" lt ON lt."Id"=lv."LookupTypeId"
       WHERE lt."Key"=$1 AND lv."Code"=$2 AND lv."IsActive"=true LIMIT 1`,
    [typeKey, valueCode]
  );
  return row ? row.Id : null;
}

async function pickAnyLookupValueId(typeKey) {
  const row = await dbOne(
    `SELECT lv."Id" FROM "LookupValues" lv
       JOIN "LookupTypes" lt ON lt."Id"=lv."LookupTypeId"
       WHERE lt."Key"=$1 AND lv."IsActive"=true ORDER BY lv."SortOrder", lv."Id" LIMIT 1`,
    [typeKey]
  );
  return row ? row.Id : null;
}

async function pickActiveLocationOtherThan(locationId, companyId = 1) {
  const row = await dbOne(
    `SELECT "Id" FROM "Locations"
       WHERE "IsActive"=true AND "Id" IS DISTINCT FROM $1
         AND ("CompanyId" IS NULL OR "CompanyId"=$2)
       ORDER BY "Id" LIMIT 1`,
    [locationId, companyId]
  );
  return row ? row.Id : null;
}

async function pickActiveDepartmentOtherThan(departmentId, companyId = 1) {
  const row = await dbOne(
    `SELECT "Id" FROM "Departments"
       WHERE ("IsActive" IS NULL OR "IsActive"=true)
         AND "Id" IS DISTINCT FROM $1
         AND ("CompanyId" IS NULL OR "CompanyId"=$2)
       ORDER BY "Id" LIMIT 1`,
    [departmentId, companyId]
  );
  return row ? row.Id : null;
}

// Generic pickers used by smoke tests. Each returns null when no candidate
// row exists, so callers can skip the test rather than fail.
const pickers = {
  asset:                 () => pickFirstId('Assets'),
  book:                  () => pickFirstId('Books'),
  vendor:                () => pickFirstId('Vendors'),
  item:                  () => pickFirstId('Items'),
  purchaseOrder:         () => pickFirstId('PurchaseOrders'),
  goodsReceipt:          () => pickFirstId('GoodsReceipts'),
  vendorInvoice:         () => pickFirstId('VendorInvoices'),
  cipProject:            () => pickFirstId('CipProjects'),
  cipCost:               () => pickFirstId('CipCosts'),
  journalEntry:          () => pickFirstId('JournalEntries'),
  workRequest:           () => pickFirstId('WorkRequests'),
  workOrder:             () => pickFirstId('WorkOrders'),
  technician:            () => pickFirstId('Technicians'),
  pmTemplate:            () => pickFirstId('PMTemplates'),
  pmSchedule:            () => pickFirstId('PMSchedules'),
  ccaClass:              () => pickFirstId('CcaClasses'),
  location:              () => pickFirstId('Locations'),
  bulkOperation:         () => pickFirstId('BulkOperations'),
};

module.exports = {
  BASE,
  login,
  gotoApp,
  dbQuery,
  dbOne,
  tableExists,
  pickFirstId,
  pickAsset,
  pickLookupValueId,
  pickAnyLookupValueId,
  pickActiveLocationOtherThan,
  pickActiveDepartmentOtherThan,
  pickers,
};
