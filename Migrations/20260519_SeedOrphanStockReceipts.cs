using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 12A PR #7 — Orphans Tab seed data.
    //
    // Seeds 7 sample "orphan" StockReceipts — receipts that arrived
    // WITHOUT a SourcePoNumber. These are the real-world cases:
    //   - Vendor short-shipped a different PO and labeled it wrong
    //   - Walk-in delivery (no PO created upfront)
    //   - Sample / NCR replacement / blanket-PO release without line tie
    //   - Old paper PO that never made it into the ERP
    //
    // The cockpit "Orphans" tab presents these alongside AI-suggested
    // candidate POs (item-match + vendor-match + recency scoring) so
    // the receiver can match them in 2 clicks without leaving Receiving.
    //
    // Idempotency:
    //   ON CONFLICT ("ReceiptNumber") DO NOTHING. Safe to re-run.
    //
    // Item lookup is forgiving: rows whose PartNumber doesn't resolve
    // to a seeded Item are silently skipped (LEFT JOIN + IS NOT NULL
    // guard). A brand-new database may seed 0 orphans — acceptable.
    //
    // ProfileId resolves to the STEEL profile by default (seeded by
    // ADR-015 Migration PR #1). STEEL is the closest fit for the
    // "MRO + general manufacturing" archetype on this seeded site.
    //
    // Reference: ADR-018 §D2 (Cockpit four-tab shell — Orphans tab).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260519_SeedOrphanStockReceipts")]
    public partial class SeedOrphanStockReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""StockReceipts""
                    (""ReceiptNumber"", ""ProfileId"", ""ItemId"", ""LotNumber"",
                     ""SerialNumber"", ""SourcePoNumber"", ""SourcePoLineId"",
                     ""ReceivedAt"", ""LocationId"", ""QuantityReceived"",
                     ""QuantityRemaining"", ""Uom"", ""Status"", ""Notes"",
                     ""CreatedAt"", ""CreatedBy"", ""Attributes"")
                SELECT
                    t.receipt_number,
                    p.""Id"",
                    i.""Id"",
                    t.lot_number,
                    NULL,
                    NULL,                       -- SourcePoNumber NULL = ORPHAN
                    NULL,
                    (now() AT TIME ZONE 'utc')::timestamptz - (t.received_offset_days || ' days')::interval,
                    NULL,
                    t.qty_received,
                    t.qty_received,             -- nothing consumed yet
                    t.uom,
                    0,                          -- Available
                    t.notes,
                    now(),
                    'seed',
                    NULL
                FROM (
                    VALUES
                        -- Recent arrivals — most likely to have an obvious PO match
                        ('RCPT-ORPHAN-2026-0001', 'BRG-DEEP-6205',   'EA',  20,  1, 'LOT-SKF-2026-WALKIN-A',
                         'Walk-in delivery from SKF rep. No PO referenced. Confirmed bearing replenishment for Line 3.'),

                        ('RCPT-ORPHAN-2026-0002', 'HYD-FIT-JIC-08',  'EA',  40,  2, 'LOT-PRK-2026-NCR-B',
                         'Parker NCR replacement shipment — original PO closed. Need to reopen against original PO or new line.'),

                        ('RCPT-ORPHAN-2026-0003', 'BLT-HX-M12',      'BX',   8,  3, NULL,
                         'Fastenal blanket-PO release arrived without specific release number on packing slip.'),

                        ('RCPT-ORPHAN-2026-0004', 'TOL-END-MILL-12', 'EA',  12,  5, 'LOT-KNM-SAMPLE-2026',
                         'Kennametal sample shipment — eval program. May not have a PO yet; check with engineering.'),

                        ('RCPT-ORPHAN-2026-0005', 'FLT-OIL-002',     'EA',  24,  6, NULL,
                         'MSC delivered without paperwork. Driver said PO was on the side of the box — label damaged.'),

                        -- Older arrivals — already aged on the dock
                        ('RCPT-ORPHAN-2026-0006', 'PLC-ETHIP-MOD',   'EA',   2, 10, 'LOT-RCK-OLD-PO',
                         'Aged 10 days. Rockwell shipped against an old paper PO never entered in the system.'),

                        ('RCPT-ORPHAN-2026-0007', 'CON-AC-3PH',      'EA',   6, 14, NULL,
                         'Aged 14 days. Receiving inspector flagged: no PO, no packing slip, vendor label only.')
                ) AS t(receipt_number, part_number, uom, qty_received, received_offset_days, lot_number, notes)
                JOIN ""Items"" i ON i.""PartNumber"" = t.part_number
                JOIN ""ReceiptProfiles"" p ON p.""Code"" = 'STEEL'
                ON CONFLICT (""ReceiptNumber"") DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""StockReceipts""
                WHERE ""ReceiptNumber"" LIKE 'RCPT-ORPHAN-2026-%';
            ");
        }
    }
}
