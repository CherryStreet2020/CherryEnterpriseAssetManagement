-- ===========================================================================
-- ABS Machining demo seed — Receiving cockpit (ASNs + orphan StockReceipts).
-- ===========================================================================
--
-- ORIGIN: Extracted from two migrations in PR #5c.3 (2026-05-23):
--   - 20260519_AddAdvancedShippingNotice.cs  (the 13 ASN + 29 line INSERTs)
--   - 20260519_SeedOrphanStockReceipts.cs    (the 7 orphan receipt INSERTs)
--
-- AUTHORITY: feedback_no_shortcuts_multi_tenant_lineage.md (Dean lock 2026-05-23)
-- "Tenant-shaped demo data does NOT live in migrations. Move ABS-shaped seeds to
--  seed/dev-demo/abs-machining.sql behind a dev-environment flag."
--
-- INTENDED EXECUTION:
--   A dev-only seeder pipeline (lands in PR #5c.4 alongside the tenant-aware
--   MaterialStructure seeder) replays this file at startup when:
--     - ASPNETCORE_ENVIRONMENT=Development
--     - AND the ABS demo company exists in the tenant catalog
--     - AND a config flag Seed:DemoData:AbsMachining:Enabled is true
--
-- IDEMPOTENCY:
--   ON CONFLICT DO NOTHING + WHERE NOT EXISTS guards throughout — safe to
--   re-run. Re-running produces 0 new rows when data is already present.
--
-- ABS-shop-shaped data (do NOT run on a non-ABS tenant):
--   - Vendor names: ROCKWELL AUTOMATION, KENNAMETAL INC, MSC INDUSTRIAL SUPPLY,
--     FASTENAL COMPANY, PARKER HANNIFIN, SKF USA INC
--   - Part numbers reference the ABS-seeded Item Master (PWR-VFD-25HP, PLC-
--     ETHIP-MOD, BRG-DEEP-6205, TOL-END-MILL-12, HYD-FIT-JIC-08, etc.)
--   - ReceiptProfile lookup expects the 'STEEL' profile to be seeded (ADR-015).
--
-- ===========================================================================
-- Section A: AdvancedShippingNotices headers (13 rows)
-- ===========================================================================

INSERT INTO "AdvancedShippingNotices"
    ("AsnNumber", "VendorId", "ShipToSiteId", "ShipDate",
     "ExpectedArrivalDate", "Status", "Carrier", "TrackingNumber",
     "SourcePoNumber", "Notes", "CreatedAt")
SELECT t.asn_number, v."Id", NULL,
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
JOIN "Vendors" v ON v."Name" = t.vendor_name
ON CONFLICT ("VendorId", "AsnNumber") DO NOTHING;

-- ===========================================================================
-- Section B: AsnLines manifest lines (29 rows across the 13 ASNs)
-- ===========================================================================

WITH lines AS (
    SELECT
        a."Id" AS asn_id,
        ROW_NUMBER() OVER (PARTITION BY a."Id" ORDER BY l.seq) AS line_no,
        l.part_number,
        l.description,
        l.uom,
        l.qty,
        l.heat,
        l.lot
    FROM "AdvancedShippingNotices" a
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
      ON l.asn_number = a."AsnNumber"
)
INSERT INTO "AsnLines"
    ("AsnId", "LineNumber", "ItemId", "Description", "PartNumber", "Uom", "ExpectedQuantity", "ReceivedQuantity", "LotNumber", "HeatNumber")
SELECT
    l.asn_id,
    l.line_no,
    i."Id",
    l.description,
    l.part_number,
    l.uom,
    l.qty,
    0,
    l.lot,
    l.heat
FROM lines l
LEFT JOIN "Items" i ON i."PartNumber" = l.part_number
WHERE NOT EXISTS (
    SELECT 1 FROM "AsnLines" al
    WHERE al."AsnId" = l.asn_id AND al."LineNumber" = l.line_no
);

-- ===========================================================================
-- Section C: Orphan StockReceipts (7 rows — receipts without a SourcePoNumber)
-- ===========================================================================

INSERT INTO "StockReceipts"
    ("ReceiptNumber", "ProfileId", "ItemId", "LotNumber",
     "SerialNumber", "SourcePoNumber", "SourcePoLineId",
     "ReceivedAt", "LocationId", "QuantityReceived",
     "QuantityRemaining", "Uom", "Status", "Notes",
     "CreatedAt", "CreatedBy", "Attributes")
SELECT
    t.receipt_number,
    p."Id",
    i."Id",
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
JOIN "Items" i ON i."PartNumber" = t.part_number
JOIN "ReceiptProfiles" p ON p."Code" = 'STEEL'
ON CONFLICT ("ReceiptNumber") DO NOTHING;
