using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 12A PR #6 — Advanced Shipping Notice domain entity.
    // PARTIALLY QUARANTINED in PR #5c.3 (2026-05-23) — see "What's quarantined" below.
    //
    // Replaces the Sprint 11 PR #6 stop-gap that prefixed
    // StockReceipt.SourcePoNumber with "ASN:" — a string identifier with
    // no real entity behind it. PR #6 makes the cockpit ASN Queue tab
    // render real data; the EDI 856 X12 + AS2 ingestion pipeline lands in
    // Sprint 21 (MCP + Agentic AI Launch Package per the 116-reckoning).
    //
    // Tables created (2) — STAY in this migration (legitimate schema):
    //   - AdvancedShippingNotices — ASN header. One row per vendor shipment.
    //   - AsnLines                — manifest lines. Many per ASN.
    //
    // Indexes — STAY:
    //   - UQ_Asn_VendorAsnNumber  — vendor can't send duplicate ASN numbers
    //   - IX_Asn_ExpectedArrival  — cockpit ByTimeLens hot path
    //   - IX_Asn_Status           — filter by status (Expected/InTransit/etc.)
    //   - IX_AsnLine_AsnId        — parent lookup
    //   - IX_AsnLine_RefPoNumber  — multi-PO ASN line lookup
    //
    // WHAT'S QUARANTINED (PR #5c.3):
    // The original migration also INSERTed 13 sample ASN headers + 29 sample
    // ASN lines tied to ABS-shop-shaped vendor names ("ROCKWELL AUTOMATION",
    // "KENNAMETAL INC", "MSC INDUSTRIAL SUPPLY", "FASTENAL COMPANY", "PARKER
    // HANNIFIN", "SKF USA INC") and the seeded Item Master's part numbers
    // (PWR-VFD-25HP, PLC-ETHIP-MOD, etc.). Per Dean lock 2026-05-23
    // (feedback_no_shortcuts_multi_tenant_lineage.md), tenant-shaped demo
    // data does NOT live in migrations. Those INSERT blocks have been
    // relocated to: seed/dev-demo/abs-machining-receiving.sql
    //
    // Existing prod environments already have those rows from the original
    // Up() (this migration ran 2026-05-19) — that data stays untouched. A
    // dev-only seeder pipeline (lands in PR #5c.4) will replay the SQL file
    // for fresh dev environments when ASPNETCORE_ENVIRONMENT=Development.
    //
    // Idempotent (CREATE IF NOT EXISTS + ON CONFLICT DO NOTHING).
    // Reversible via Down() drop-cascade.
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

            // ---------- 3 + 4) Seed sample ASNs + lines ----------
            // QUARANTINED in PR #5c.3 (2026-05-23). The original migration ran
            // two INSERT blocks (13 ASN headers + 29 ASN lines) with hardcoded
            // ABS-shop-shaped vendor names + PartNumbers. Per Dean lock,
            // tenant-shaped demo data does NOT live in migrations. The SQL has
            // been relocated to:  seed/dev-demo/abs-machining-receiving.sql
            //
            // A dev-only seeder pipeline (PR #5c.4) will replay that file when
            // ASPNETCORE_ENVIRONMENT=Development AND the rows are not already
            // present. Prod environments that ran the original Up() keep their
            // rows untouched.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AsnLines"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AdvancedShippingNotices"" CASCADE;");
        }
    }
}
