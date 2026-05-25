using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <summary>
    /// SnapshotRebaseline_2026_05_25 — NO-OP migration that anchors the
    /// regenerated model snapshot.
    ///
    /// Context. Sprint 13.5 PRA-4 → PRA-11 migrations used raw mb.Sql()
    /// CREATE TABLE statements which bypass EF Core's model-snapshot generation
    /// (see Lock 12 in spaces memory). After ~8 sprints of accumulated drift,
    /// the snapshot was missing 46 Sprint 13.5 master-files tables (Warehouse/
    /// Bin/Lot/Serial/ItemGroup/PostingProfile/UOM/Currency/PaymentTerm/Tax/
    /// PriceList/Employee/WageGroup/LaborRate/AccountingKey/PackLevel + the
    /// CustomerProject foundation + Carriers + Chain-of-Custody graph nodes).
    ///
    /// Combined with 8 entity-config FK drift fixes (FaiProductAccountability,
    /// FaiCharacteristic, CustomerProject→Program, ProjectAmendment/Member/
    /// Phase, ItemCompanyStocking→Item, PMOccurrence→WorkOrder — added
    /// explicit .WithMany(parent => parent.Collection) bindings to eliminate
    /// shadow FK columns), the model now diverges from the prior snapshot by
    /// 441 ops.
    ///
    /// However, EVERY ONE of those 441 ops describes schema that already
    /// physically exists on production (created by the raw-SQL migrations on
    /// helium, replicated to prod Neon via the 2026-05-24 INSERT-into-
    /// __EFMigrationsHistory + COPY recovery — see
    /// discovery_helium_vs_prod_db_2026_05_24 in spaces memory). Running those
    /// ops on prod would either error (CREATE TABLE on existing tables) or
    /// destroy data (DROP TABLE on populated tables).
    ///
    /// Therefore this migration is intentionally a NO-OP. Its sole purpose is
    /// to advance the EF migration pointer + anchor the regenerated snapshot
    /// (held in the matching .Designer.cs) so that:
    ///
    ///   1. MigrateAsync() at startup no longer throws
    ///      PendingModelChangesWarning (snapshot now matches model).
    ///   2. Future `dotnet ef migrations add` runs produce clean small diffs.
    ///   3. Lock 11 (always-on MigrateAsync, removed EnsureCreated trap) and
    ///      Lock 12 (raw-SQL migrations require snapshot update) become safe
    ///      to enforce going forward.
    ///
    /// Shadow FK cleanup (2 dead columns: PMOccurrences.WorkOrderId1 +
    /// ItemCompanyStockings.ItemId1, if they exist on prod) is deferred to a
    /// future curated migration. They're harmless dead columns and prod
    /// behavior is unaffected.
    /// </summary>
    /// <inheritdoc />
    public partial class SnapshotRebaseline_2026_05_25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op. See class doc comment.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op. See class doc comment.
        }
    }
}
