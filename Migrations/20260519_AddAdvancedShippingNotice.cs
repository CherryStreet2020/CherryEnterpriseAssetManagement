using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 12A PR #6 — Advanced Shipping Notice domain entity.
    //
    // Replaces the Sprint 11 PR #6 stop-gap that prefixed
    // StockReceipt.SourcePoNumber with "ASN:" — a string identifier with
    // no real entity behind it. PR #6 makes the cockpit ASN Queue tab
    // render real data; the EDI 856 X12 + AS2 ingestion pipeline lands in
    // Sprint 21 (MCP + Agentic AI Launch Package per the 116-reckoning).
    //
    // Tables created (2):
    //   - AdvancedShippingNotices — ASN header. One row per vendor shipment.
    //   - AsnLines                — manifest lines. Many per ASN.
    //
    // Indexes:
    //   - UQ_Asn_VendorAsnNumber  — vendor can't send duplicate ASN numbers
    //   - IX_Asn_ExpectedArrival  — cockpit ByTimeLens hot path
    //   - IX_Asn_Status           — filter by status (Expected/InTransit/etc.)
    //   - IX_AsnLine_AsnId        — parent lookup
    //   - IX_AsnLine_RefPoNumber  — multi-PO ASN line lookup
    //
    // Idempotent (CREATE IF NOT EXISTS). Reversible via Down() drop-cascade.
    //
    // Reference: ADR-018 §D2 (Cockpit four-tab shell — ASN Queue tab).
    [DbContext(typeof(AppDbContext))]
    [Migration("20260519_AddAdvancedShippingNotice")]
    public partial class AddAdvancedShippingNotice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1) AdvancedShippingNotices ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AdvancedShippingNotices"" (
                    ""Id""                     serial          PRIMARY KEY,
                    ""AsnNumber""              varchar(40)     NOT NULL,
                    ""VendorId""               integer         NOT NULL REFERENCES ""Vendors""(""Id"") ON DELETE RESTRICT,
                    ""ShipToSiteId""           integer         NULL REFERENCES ""Sites""(""Id"") ON DELETE SET NULL,
                    ""ShipDate""               timestamptz     NULL,
                    ""ExpectedArrivalDate""    timestamptz     NULL,
                    ""Status""                 integer         NOT NULL DEFAULT 0,
                    ""Carrier""                varchar(50)     NULL,
                    ""TrackingNumber""         varchar(80)     NULL,
                    ""SourcePoNumber""         varchar(20)     NULL,
                    ""Notes""                  varchar(500)    NULL,
                    ""CreatedAt""              timestamptz     NOT NULL DEFAULT now(),
                    ""ReceivedAt""             timestamptz     NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""UQ_Asn_VendorAsnNumber""
                ON ""AdvancedShippingNotices"" (""VendorId"", ""AsnNumber"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Asn_ExpectedArrival""
                ON ""AdvancedShippingNotices"" (""ExpectedArrivalDate"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Asn_Status""
                ON ""AdvancedShippingNotices"" (""Status"");
            ");

            // ---------- 2) AsnLines ----------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AsnLines"" (
                    ""Id""                  serial          PRIMARY KEY,
                    ""AsnId""               integer         NOT NULL REFERENCES ""AdvancedShippingNotices""(""Id"") ON DELETE CASCADE,
                    ""LineNumber""          integer         NOT NULL,
                    ""ItemId""              integer         NULL REFERENCES ""Items""(""Id"") ON DELETE SET NULL,
                    ""Description""         varchar(200)    NOT NULL,
                    ""PartNumber""          varchar(50)     NULL,
                    ""RefPoNumber""         varchar(20)     NULL,
                    ""RefPoLineId""         varchar(40)     NULL,
                    ""Uom""                 varchar(20)     NOT NULL DEFAULT 'EA',
                    ""ExpectedQuantity""    numeric(18,4)   NOT NULL DEFAULT 0,
                    ""ReceivedQuantity""    numeric(18,4)   NOT NULL DEFAULT 0,
                    ""LotNumber""           varchar(80)     NULL,
                    ""SerialNumber""        varchar(120)    NULL,
                    ""ExpirationDate""      timestamptz     NULL,
                    ""HeatNumber""          varchar(60)     NULL,
                    ""Notes""               varchar(500)    NULL
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AsnLine_AsnId""
                ON ""AsnLines"" (""AsnId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_AsnLine_RefPoNumber""
                ON ""AsnLines"" (""RefPoNumber"");
            ");

            // ---------- 3) Seed sample ASNs ----------
            // Operator-credible mix: some overdue, some today, some this week,
            // some upcoming. Carriers from common US freight + parcel network.
            // Vendor IDs reference existing seeded vendors; SELECT-based to
            // tolerate any vendor ID renumbering during a future reseed.
            migrationBuilder.Sql(@"
                INSERT INTO ""AdvancedShippingNotices""
                    (""AsnNumber"", ""VendorId"", ""ShipToSiteId"", ""ShipDate"",
                     ""ExpectedArrivalDate"", ""Status"", ""Carrier"", ""TrackingNumber"",
                     ""SourcePoNumber"", ""Notes"", ""CreatedAt"")
                SELECT t.asn_number, v.""Id"", NULL,
                       (now() AT TIME ZONE 'utc')::timestamptz - (t.ship_offset_days || ' days')::interval,
                       (now() AT TIME ZONE 'utc')::timestamptz + (t.eta_offset_days || ' days')::interval,
                       t.status,
                       t.carrier,
                       t.tracking,
                       t.po_number,
                       t.notes,
                       now()
                FROM (
                    VALUES
                        ('ASN-RCK-2026-0419', 'ROCKWELL AUTOMATION',     3, -7,  1, 'FedEx Freight',    'FXFE 8294 5170 33', 'PO-PWH-0059', 'Servo drive pallet — fragile'),
                        ('ASN-KNM-2026-0312', 'KENNAMETAL INC',          5, -5,  0, 'UPS Ground',       '1Z 3X7 832 04 5829 1144 6', 'PO-PWH-0060', 'Tooling kit'),
                        ('ASN-MSC-2026-0788', 'MSC INDUSTRIAL SUPPLY',   3, -3, -1, 'MSC Direct',       'MSC-AC-44829', 'PO-PWH-0061', 'Daily consumables run'),
                        ('ASN-FST-2026-0901', 'FASTENAL COMPANY',        2, -2,  0, 'Fastenal Truck',   NULL,           'PO-PWH-0062', 'Partial — second truck Wed'),
                        ('ASN-PRK-2026-0145', 'PARKER HANNIFIN',         4,  0,  3, 'R+L Carriers',     'RL 4471-8829-X', 'PO-PWH-0067', 'Hydraulic fittings palletized'),
                        ('ASN-SKF-2026-0337', 'SKF USA INC',             3,  1,  4, 'XPO Logistics',    'XPO 2918 4775', 'PO-PWH-0068', 'Bearings + seals'),
                        ('ASN-RCK-2026-0428', 'ROCKWELL AUTOMATION',     3, -1,  2, 'UPS Next Day Air', '1Z 3X7 832 04 5829 1145 8', 'PO-PWH-0069', 'Spare PLC + I/O modules'),
                        ('ASN-MSC-2026-0791', 'MSC INDUSTRIAL SUPPLY',   3, -1,  7, 'MSC Direct',       'MSC-AC-44833', NULL,        'Multi-PO blanket release'),
                        ('ASN-FST-2026-0910', 'FASTENAL COMPANY',        4, -10, -3, 'Fastenal Truck',  NULL,           'PO-PWH-0062', 'Late — vendor confirmed snowstorm delay'),
                        ('ASN-PRK-2026-0152', 'PARKER HANNIFIN',         5,  2,  9, 'R+L Carriers',     'RL 4471-8901-X', 'PO-PWH-0067', 'Hydraulic manifolds — special order'),
                        ('ASN-KNM-2026-0318', 'KENNAMETAL INC',          3,  4, 14, 'UPS Ground',       '1Z 3X7 832 04 5829 1149 2', NULL, 'Quarterly tooling stock'),
                        ('ASN-SKF-2026-0341', 'SKF USA INC',             4,  0, 21, 'XPO Logistics',    'XPO 2918 4781', NULL,        'Mill-bearing replenishment'),
                        ('ASN-RCK-2026-0451', 'ROCKWELL AUTOMATION',     5,  3,  6, 'FedEx Freight',    'FXFE 8294 5198 11', 'PO-PWH-0073', 'VFDs + soft starters')
                ) AS t(asn_number, vendor_name, status, ship_offset_days, eta_offset_days, carrier, tracking, po_number, notes)
                JOIN ""Vendors"" v ON v.""Name"" = t.vendor_name
                ON CONFLICT (""VendorId"", ""AsnNumber"") DO NOTHING;
            ");

            // Seed manifest lines — 2-4 lines per ASN, referencing parts from
            // the seeded Item Master where possible. Bound to actual Items via
            // PartNumber lookup so it survives item-id renumbering.
            migrationBuilder.Sql(@"
                WITH lines AS (
                    SELECT
                        a.""Id"" AS asn_id,
                        ROW_NUMBER() OVER (PARTITION BY a.""Id"" ORDER BY l.seq) AS line_no,
                        l.part_number,
                        l.description,
                        l.uom,
                        l.qty,
                        l.heat,
                        l.lot
                    FROM ""AdvancedShippingNotices"" a
                    JOIN (
                        VALUES
                            ('ASN-RCK-2026-0419', 1, 'PWR-VFD-25HP',     'Powerflex 525 VFD 25HP',         'EA',  4, NULL, 'LOT-PWR-2026-0419-A'),
                            ('ASN-RCK-2026-0419', 2, 'PLC-ETHIP-MOD',    'EtherNet/IP Communication Module','EA',  6, NULL, 'LOT-PWR-2026-0419-B'),
                            ('ASN-RCK-2026-0419', 3, 'CON-AC-3PH',       '3-Phase AC Contactor 90A',       'EA', 12, NULL, NULL),
                            ('ASN-KNM-2026-0312', 1, 'TOL-END-MILL-12',  'Carbide End Mill 1/2in',         'EA', 24, NULL, 'LOT-KNM-3142'),
                            ('ASN-KNM-2026-0312', 2, 'TOL-INS-CCMT',     'CCMT Carbide Insert',            'EA', 50, NULL, NULL),
                            ('ASN-MSC-2026-0788', 1, 'FLT-OIL-002',      'Cartridge Oil Filter',           'EA', 30, NULL, NULL),
                            ('ASN-MSC-2026-0788', 2, 'GLV-NIT-LRG',      'Nitrile Gloves Large 100ct',     'BX', 12, NULL, NULL),
                            ('ASN-MSC-2026-0788', 3, 'ABR-WHL-FLP-7',    'Flap Disc 7in 80-grit',          'EA', 25, NULL, NULL),
                            ('ASN-FST-2026-0901', 1, 'BLT-HX-M12',       'Hex Bolt M12x40 Grade 8.8',      'BX',  4, NULL, NULL),
                            ('ASN-FST-2026-0901', 2, 'WSH-FLT-M12',      'Flat Washer M12',                'BX',  4, NULL, NULL),
                            ('ASN-PRK-2026-0145', 1, 'HYD-FIT-JIC-08',   'JIC Hydraulic Fitting -08',      'EA', 80, NULL, 'LOT-PRK-2026-0145'),
                            ('ASN-PRK-2026-0145', 2, 'HYD-HSE-25',       'Hydraulic Hose 25ft 1/2in',      'EA',  6, NULL, 'LOT-PRK-2026-0146'),
                            ('ASN-SKF-2026-0337', 1, 'BRG-DEEP-6205',    'Deep Groove Bearing 6205-2RS',   'EA', 40, NULL, 'LOT-SKF-2026-0337'),
                            ('ASN-SKF-2026-0337', 2, 'BRG-TAP-30206',    'Tapered Roller Bearing 30206',   'EA', 12, NULL, 'LOT-SKF-2026-0338'),
                            ('ASN-RCK-2026-0428', 1, 'PLC-1769-L36',     'CompactLogix 1769-L36ERM',       'EA',  2, NULL, NULL),
                            ('ASN-RCK-2026-0428', 2, 'IO-DI-1769-IQ32',  'DI Module 1769-IQ32',            'EA',  4, NULL, NULL),
                            ('ASN-MSC-2026-0791', 1, 'STL-PLT-A36-25',   'A36 Steel Plate 1/4in x 4x8',    'EA',  6, 'HT-A36-2026-0791', NULL),
                            ('ASN-MSC-2026-0791', 2, 'STL-RND-1018-2',   '1018 Round Bar 2in dia 12ft',    'EA', 10, 'HT-1018-2026-0792', NULL),
                            ('ASN-FST-2026-0910', 1, 'BLT-HX-M12',       'Hex Bolt M12x40 Grade 8.8',      'BX',  3, NULL, NULL),
                            ('ASN-FST-2026-0910', 2, 'NUT-HX-M12',       'Hex Nut M12 Grade 8',            'BX',  3, NULL, NULL),
                            ('ASN-PRK-2026-0152', 1, 'HYD-MAN-6PORT',    '6-Port Hydraulic Manifold',      'EA',  2, NULL, 'LOT-PRK-2026-0152'),
                            ('ASN-KNM-2026-0318', 1, 'TOL-END-MILL-12',  'Carbide End Mill 1/2in',         'EA', 60, NULL, 'LOT-KNM-3175'),
                            ('ASN-KNM-2026-0318', 2, 'TOL-INS-CCMT',     'CCMT Carbide Insert',            'EA',150, NULL, NULL),
                            ('ASN-KNM-2026-0318', 3, 'TOL-DRL-HSS-CO',   'HSS-Co Drill Bit Set',           'BX',  8, NULL, NULL),
                            ('ASN-SKF-2026-0341', 1, 'BRG-MIL-22318',    'Spherical Roller Bearing 22318', 'EA',  8, NULL, 'LOT-SKF-2026-0341'),
                            ('ASN-SKF-2026-0341', 2, 'BRG-MIL-22320',    'Spherical Roller Bearing 22320', 'EA',  4, NULL, 'LOT-SKF-2026-0342'),
                            ('ASN-RCK-2026-0451', 1, 'PWR-VFD-50HP',     'Powerflex 755 VFD 50HP',         'EA',  2, NULL, 'LOT-PWR-2026-0451-A'),
                            ('ASN-RCK-2026-0451', 2, 'PWR-SST-30HP',     '30HP Soft Starter SMC-Flex',     'EA',  3, NULL, NULL),
                            ('ASN-RCK-2026-0451', 3, 'CON-AC-3PH',       '3-Phase AC Contactor 90A',       'EA',  6, NULL, NULL)
                    ) AS l(asn_number, seq, part_number, description, uom, qty, heat, lot)
                      ON l.asn_number = a.""AsnNumber""
                )
                INSERT INTO ""AsnLines""
                    (""AsnId"", ""LineNumber"", ""ItemId"", ""Description"", ""PartNumber"", ""Uom"", ""ExpectedQuantity"", ""ReceivedQuantity"", ""LotNumber"", ""HeatNumber"")
                SELECT
                    l.asn_id,
                    l.line_no,
                    i.""Id"",
                    l.description,
                    l.part_number,
                    l.uom,
                    l.qty,
                    0,
                    l.lot,
                    l.heat
                FROM lines l
                LEFT JOIN ""Items"" i ON i.""PartNumber"" = l.part_number
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""AsnLines"" al
                    WHERE al.""AsnId"" = l.asn_id AND al.""LineNumber"" = l.line_no
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AsnLines"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AdvancedShippingNotices"" CASCADE;");
        }
    }
}
