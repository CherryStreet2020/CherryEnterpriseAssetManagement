// Light flow tests across the procurement chain: items → vendors → POs →
// goods receipts → AP invoices. We don't try to create real transactions
// (those require period setup, GL accounts, etc.) — we drive read-only
// drill-throughs and assert the deeper detail pages render without errors.
const { test, expect } = require('@playwright/test');
const { BASE, login, dbOne, pickers } = require('./_helpers');

test.describe('INVENTORY / PURCHASING / RECEIVING — drill-through', () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test('items index → item edit', async ({ page }) => {
    const id = await pickers.item();
    test.skip(!id, 'no items in DB');
    const resp = await page.goto(`${BASE}/Materials/ItemEdit/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('vendors edit page renders for a real vendor', async ({ page }) => {
    const id = await pickers.vendor();
    test.skip(!id, 'no vendors in DB');
    const resp = await page.goto(`${BASE}/Materials/Vendors/Edit/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('purchasing index lists POs and details opens', async ({ page }) => {
    const id = await pickers.purchaseOrder();
    test.skip(!id, 'no POs in DB');
    const idx = await page.goto(`${BASE}/Purchasing`);
    expect(idx.status()).toBeLessThan(500);
    const resp = await page.goto(`${BASE}/Purchasing/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('receiving index → receive page opens for a PO', async ({ page }) => {
    const id = await pickers.purchaseOrder();
    test.skip(!id, 'no POs in DB');
    await page.goto(`${BASE}/Receiving`);
    const resp = await page.goto(`${BASE}/Receiving/Receive/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('goods receipt details page renders', async ({ page }) => {
    const id = await pickers.goodsReceipt();
    test.skip(!id, 'no goods receipts in DB');
    const resp = await page.goto(`${BASE}/Receiving/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });

  test('accounts payable details renders for a vendor invoice', async ({ page }) => {
    const id = await pickers.vendorInvoice();
    test.skip(!id, 'no AP invoices in DB');
    const resp = await page.goto(`${BASE}/AccountsPayable/Details/${id}`);
    expect(resp.status()).toBeLessThan(500);
  });
});
