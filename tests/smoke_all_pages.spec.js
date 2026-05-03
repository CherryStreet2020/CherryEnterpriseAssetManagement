// Smoke-test every Razor Page in the app: log in once per worker, navigate to
// every known route, assert the response is 200 (or an explicitly tolerated
// 3xx/404 for legacy/redirect or detail-page-when-no-data cases). For pages
// with route parameters we resolve a real Id from the database; if the
// underlying table has no rows we mark the test skipped rather than fail.
const { test, expect } = require('@playwright/test');
const { BASE, login, dbOne, pickers, pickFirstId } = require('./_helpers');

// ----- Static (no-param) pages ---------------------------------------------
const STATIC_PAGES = [
  '/',
  '/Help',
  '/Help/Implementation',
  '/Help/Tasks',
  '/Help/Topic',
  '/Privacy',
  // Account
  '/Account/AccessDenied',
  // Assets module
  '/Assets',
  // BulkOperations
  '/BulkOperations',
  // Books
  '/Books',
  '/Books/Create',
  // Journals
  '/Journals',
  '/Journals/Generate',
  // CCA
  '/CCA',
  '/CCA/ClassReport',
  // CIP
  '/CIP',
  '/CIP/Costs',
  // UsTax
  '/UsTax',
  // AccountsPayable
  '/AccountsPayable',
  '/AccountsPayable/Create',
  // Purchasing
  '/Purchasing',
  '/Purchasing/Create',
  // Receiving
  '/Receiving',
  '/Receiving/History',
  // Maintenance
  '/Maintenance',
  '/Maintenance/Create',
  '/Maintenance/Schedules',
  '/Maintenance/ScheduleBoard',
  '/Maintenance/Assignments',
  '/Maintenance/Technicians',
  '/Maintenance/WorkRequests',
  '/Maintenance/WorkRequests/Create',
  // Inventory
  '/Inventory',
  // Materials
  '/Materials/Items',
  '/Materials/Vendors/Create',
  // Reports
  '/Reports',
  '/Reports/Builder',
  '/Reports/ChartOfAccounts',
  '/Reports/Compliance',
  '/Reports/DepreciationPreview',
  '/Reports/DepreciationSchedule',
  '/Reports/Form4562',
  '/Reports/ReportHub',
  '/Reports/T2Schedule8',
  // AI
  '/AI',
  // API hub pages
  '/API',
  '/API/Import',
  // WorkOrders index page (route is the Details route with no id)
  '/WorkOrders/Details',
  // Admin
  '/Admin',
  '/Admin/Approvals',
  '/Admin/AssetCategories',
  '/Admin/AuditLog',
  '/Admin/Barcodes',
  '/Admin/CcaBackfill',
  '/Admin/Companies',
  '/Admin/Company',
  '/Admin/CostCenters',
  '/Admin/DataImport',
  '/Admin/DataManagement',
  '/Admin/DemoData',
  '/Admin/Departments',
  '/Admin/DepreciationBackfill',
  '/Admin/Diagnostics',
  '/Admin/EnvironmentStatus',
  '/Admin/ExchangeRates',
  '/Admin/Export',
  '/Admin/GlAccounts',
  '/Admin/Import',
  '/Admin/ImportWizard',
  '/Admin/Integrations',
  '/Admin/Integrations/Inbound',
  '/Admin/Integrations/Maps',
  '/Admin/Inventory',
  '/Admin/ItemCategories',
  '/Admin/Items',
  '/Admin/JournalBackfill',
  '/Admin/Kits',
  '/Admin/Locations',
  '/Admin/Lookups',
  '/Admin/Manufacturers',
  '/Admin/Outbox',
  '/Admin/PMSchedules',
  '/Admin/PMTemplates',
  '/Admin/ProjectManagers',
  '/Admin/Requisitions',
  '/Admin/SeedData',
  '/Admin/Sites',
  '/Admin/SmokeTests',
  '/Admin/StockLevels',
  '/Admin/SystemSettings',
  '/Admin/Technicians',
  '/Admin/Tenants',
  '/Admin/Users',
  '/Admin/Vendors',
  '/Admin/Webhooks',
  '/Admin/Webhooks/Deliveries',
];

// Tolerated non-200 responses for some legacy/admin pages that intentionally
// 302 elsewhere when modules are off, or 404 when no row matches a sentinel.
const TOLERATED_REDIRECTS = new Set([
  // documented in tests/04_redirects.spec.js
  '/Admin/Locations',
  '/Admin/Vendors',
  '/Admin/PMTemplates',
  '/Admin/PMSchedules',
  '/Admin/GlAccounts',
  '/Admin/Inventory',
]);

// ----- Param pages: each entry resolves to a final URL (or null to skip) ---
const PARAM_PAGES = [
  // Assets (Asset has optional id; without id it is the create page)
  { name: '/Assets/Asset (create)', url: () => '/Assets/Asset' },
  { name: '/Assets/Asset/{id}', url: async () => withId('/Assets/Asset', await pickers.asset()) },
  { name: '/Assets/Schedule/{id}', url: async () => withId('/Assets/Schedule', await pickers.asset()) },
  { name: '/Assets/Improve/{id}', url: async () => withId('/Assets/Improve', await pickers.asset()) },
  { name: '/Assets/Transfer/{id}', url: async () => withId('/Assets/Transfer', await pickers.asset()) },
  { name: '/Assets/Dispose/{id}', url: async () => withId('/Assets/Dispose', await pickers.asset()) },

  // BulkOperations details
  { name: '/BulkOperations/Details/{id}', url: async () => withId('/BulkOperations/Details', await pickers.bulkOperation()) },

  // Books
  { name: '/Books/Edit/{id}', url: async () => withId('/Books/Edit', await pickers.book()) },
  { name: '/Books/Details/{id}', url: async () => withId('/Books/Details', await pickers.book()) },
  { name: '/Books/GlAccounts/{bookId}', url: async () => withId('/Books/GlAccounts', await pickers.book()) },

  // Materials
  { name: '/Materials/ItemEdit (create)', url: () => '/Materials/ItemEdit' },
  { name: '/Materials/ItemEdit/{id}', url: async () => withId('/Materials/ItemEdit', await pickers.item()) },
  { name: '/Materials/Vendors/Edit/{id}', url: async () => withId('/Materials/Vendors/Edit', await pickers.vendor()) },

  // Inventory list (id is locationId)
  { name: '/Inventory/List/{id}', url: async () => withId('/Inventory/List', await pickers.location()) },

  // Purchasing details
  { name: '/Purchasing/Details/{id}', url: async () => withId('/Purchasing/Details', await pickers.purchaseOrder()) },

  // Receiving (most receive pages key off PO id, Inspect/Details key off GoodsReceipt id)
  { name: '/Receiving/Receive/{poId}', url: async () => withId('/Receiving/Receive', await pickers.purchaseOrder()) },
  { name: '/Receiving/Inspect/{id}', url: async () => withId('/Receiving/Inspect', await pickers.goodsReceipt()) },
  { name: '/Receiving/Details/{id}', url: async () => withId('/Receiving/Details', await pickers.goodsReceipt()) },

  // AccountsPayable details
  { name: '/AccountsPayable/Details/{id}', url: async () => withId('/AccountsPayable/Details', await pickers.vendorInvoice()) },

  // CIP
  { name: '/CIP/Details/{id}', url: async () => withId('/CIP/Details', await pickers.cipProject()) },
  {
    name: '/CIP/CostDetails/{projectId}/{costId}',
    url: async () => {
      const cost = await dbOne('SELECT "Id","CipProjectId" FROM "CipCosts" ORDER BY "Id" LIMIT 1');
      return cost ? `/CIP/CostDetails/${cost.CipProjectId}/${cost.Id}` : null;
    },
  },

  // Journals
  { name: '/Journals/Details/{id}', url: async () => withId('/Journals/Details', await pickers.journalEntry()) },

  // Maintenance
  { name: '/Maintenance/Details/{id}', url: async () => withId('/Maintenance/Details', await pickers.maintenanceEvent()) },
  { name: '/Maintenance/WorkRequests/Details/{id}', url: async () => withId('/Maintenance/WorkRequests/Details', await pickers.workRequest()) },
  { name: '/Maintenance/Technicians/Profile/{id}', url: async () => withId('/Maintenance/Technicians/Profile', await pickers.technician()) },

  // Admin PM editors
  { name: '/Admin/PMTemplateEdit (create)', url: () => '/Admin/PMTemplateEdit' },
  { name: '/Admin/PMTemplateEdit/{id}', url: async () => withId('/Admin/PMTemplateEdit', await pickers.pmTemplate()) },
  { name: '/Admin/PMScheduleEdit (create)', url: () => '/Admin/PMScheduleEdit' },
  { name: '/Admin/PMScheduleEdit/{id}', url: async () => withId('/Admin/PMScheduleEdit', await pickers.pmSchedule()) },

  // Admin Lookups editor
  {
    name: '/Admin/Lookups/EditValues',
    url: async () => {
      const id = await pickFirstId('LookupTypes');
      return id ? `/Admin/Lookups/EditValues?lookupTypeId=${id}` : null;
    },
  },

  // WorkOrders details
  { name: '/WorkOrders/Details/{id}', url: async () => withId('/WorkOrders/Details', await pickers.maintenanceEvent()) },
];

function withId(base, id) {
  return id ? `${base}/${id}` : null;
}

test.describe('SMOKE — every static page returns 200', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const path of STATIC_PAGES) {
    test(`GET ${path}`, async ({ page }) => {
      const resp = await page.goto(`${BASE}${path}`);
      expect(resp, `no response for ${path}`).not.toBeNull();
      const status = resp.status();
      if (TOLERATED_REDIRECTS.has(path)) {
        expect([200, 301, 302]).toContain(status);
      } else {
        expect(status, `${path} returned ${status}`).toBe(200);
      }
      // Sanity: the rendered page should not contain a raw stack-trace
      // marker (the dev exception page exposes the word "stack" prominently).
      const body = await page.content();
      expect(body, `${path} looks like an error page`).not.toMatch(/Unhandled exception|System\.NullReference|InvalidOperationException at/);
    });
  }
});

test.describe('SMOKE — every parameterized page returns 200', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  for (const entry of PARAM_PAGES) {
    test(`GET ${entry.name}`, async ({ page }) => {
      const url = await entry.url();
      test.skip(!url, `${entry.name}: no candidate row in DB, skipping`);
      const resp = await page.goto(`${BASE}${url}`);
      expect(resp, `no response for ${url}`).not.toBeNull();
      const status = resp.status();
      // Detail pages may legitimately 404 if the row was filtered by tenant
      // or soft-delete; treat 404 as acceptable but require not-500.
      expect(status, `${url} returned ${status}`).toBeLessThan(500);
      const body = await page.content();
      expect(body, `${url} looks like an error page`).not.toMatch(/Unhandled exception|System\.NullReference|InvalidOperationException at/);
    });
  }
});
