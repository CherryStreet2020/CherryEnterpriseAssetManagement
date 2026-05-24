using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    // Sprint 12A PR #7 — Orphans Tab seed data — QUARANTINED in PR #5c.3 (2026-05-23).
    //
    // ORIGINAL PURPOSE (kept for historical context):
    // Seeded 7 sample "orphan" StockReceipts (RCPT-ORPHAN-2026-0001 through
    // RCPT-ORPHAN-2026-0007) — receipts that arrived WITHOUT a SourcePoNumber.
    // These rendered the cockpit "Orphans" tab demo for ADR-018 §D2.
    //
    // WHY QUARANTINED (PR #5c.3):
    // Per Dean lock 2026-05-23 (feedback_no_shortcuts_multi_tenant_lineage.md):
    // tenant-shaped demo data does NOT live in migrations. The original seed
    // INSERT was ABS-shaped (vendor names like SKF / Parker / Fastenal /
    // Kennametal / MSC / Rockwell tied to a specific tenant's PartNumber
    // catalog) and would pollute every new tenant onboarded via these migrations.
    //
    // WHAT THIS PR CHANGES:
    //   - Up() is now a NO-OP. Existing prod environments already have the 7
    //     rows applied (this migration ran on 2026-05-19) — that data stays.
    //   - The seed SQL was relocated to: seed/dev-demo/abs-machining-receiving.sql
    //   - A dev-only seeder pipeline (lands in PR #5c.4 alongside the tenant-aware
    //     MaterialStructure seeder) will replay the file when
    //     ASPNETCORE_ENVIRONMENT=Development AND the rows are not already present.
    //
    // SAFETY:
    //   - Idempotent (no-op). Safe to re-run on any environment.
    //   - Existing prod orphan-receipt rows are NOT touched.
    //   - EFMigrationsHistory entry stays — fresh installs see this migration
    //     applied and skip it, exactly matching prod.
    //
    // ROLLBACK: Down() unchanged. Still deletes any rows matching the
    // 'RCPT-ORPHAN-2026-%' prefix (the original seed set) if you ever need
    // to retire this migration cleanly.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260519_SeedOrphanStockReceipts")]
    public partial class SeedOrphanStockReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PR #5c.3 — Quarantined. Seed payload moved to
            // seed/dev-demo/abs-machining-receiving.sql.
            // No-op intentionally; see header comment for rationale.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down() deletes the original seed set by ReceiptNumber prefix.
            // Preserved so an environment that ran the original Up() can still
            // cleanly retire this migration's data.
            migrationBuilder.Sql(@"
                DELETE FROM ""StockReceipts""
                WHERE ""ReceiptNumber"" LIKE 'RCPT-ORPHAN-2026-%';
            ");
        }
    }
}
