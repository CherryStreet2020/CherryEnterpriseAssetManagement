using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-5b — AccountingKey (COA segment-key materialization).
    //
    // Master Files Baseline cascade ship #10 of 10 — closes the segment-key gap
    // from docs/research/master-files-baseline-2026-05-24.md §4.2 / §4.3.
    //
    // PURPOSE
    // -------
    // Every real ERP COA posts journal lines against a multi-segment key:
    //
    //   {Company} - {Site} - {Account} - {CostCenter} - {Department}
    //     - {Project} - {InterCoPartner} - {Vertical}
    //
    // Today JournalLine carries only `Account` (varchar(50) account-number
    // string). That means P&L can ONLY slice by Account — no per-Department,
    // per-Project, per-Site, per-Vertical drill. AccountingKey materializes the
    // 8-segment combination into one row so JournalLine carries a single
    // AccountingKeyId FK and the segment dimensionality is reachable for SQL
    // GROUP BY at every roll-up layer.
    //
    // ARCHITECTURAL SHAPE (memo §4.3)
    // -------------------------------
    // - 8 segments, NOT 9 (Customer / Product / Equipment slicing flows
    //   through PostingProfile + SalesOrder pivots, not GL segments — confirmed
    //   with Dean 2026-05-24 pre-flight).
    // - Resolver lives in the existing IGlAccountResolver (ADR-003) — extended
    //   with ResolveAccountingKeyAsync to keep one cascade instead of forking
    //   into a parallel service.
    // - DEF-008 fallback: JournalLine keeps its legacy `Account` varchar(50)
    //   column AND gains an `AccountingKeyId int? FK`. Service-layer writes
    //   BOTH; service-layer reads prefer AccountingKeyId, fall back to Account.
    //   Future cleanup PR drops the legacy column once every read path is
    //   migrated.
    // - JournalEntry has no CompanyId. AccountingKey resolution walks
    //   JournalEntry → BookId → Book.CompanyId at write time.
    //
    // HASH KEY
    // --------
    // `AccountingKeyHash` is a deterministic sha256 hex over the canonical
    // string form of the 8 segments:
    //
    //   "{CompanyId}|{SiteId|''}|{AccountId}|{CostCenterId|''}|{DepartmentId|''}|
    //    {ProjectId|''}|{InterCoPartnerCompanyId|''}|{(short)IndustryVertical|''}"
    //
    // (NULL segments serialize as empty strings, NOT the literal "NULL" or "0",
    // to keep the canonical form unambiguous.) Same 8 inputs → same hash → same
    // AccountingKey row. The (CompanyId, AccountingKeyHash) partial UNIQUE
    // index is the find-or-insert lookup the resolver uses.
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NOT NULL (operational data).
    //
    // BIC ENTITY CHECKLIST (reference_bic_entity_checklist.md) — all 6 pass:
    //   1. Tenant trio: CompanyId NOT NULL. SiteId nullable but FK to Location.
    //   2. Tenant-prefixed UNIQUE: (CompanyId, AccountingKeyHash) UNIQUE.
    //   3. ITenantContext check: enforced inside GlAccountResolver.ResolveAccountingKeyAsync.
    //   4. Chain-of-custody: not applicable — AccountingKey is a lookup
    //      dimension, not a domain entity in the chain graph.
    //   5. No tenant data in migration: zero seed rows. AccountingKey rows are
    //      created on demand at JE-post time per tenant.
    //   6. Snapshot lineage: AccountingKey IS the snapshot — once an existing
    //      JournalLine points at AccountingKeyId X, post-create edits to the
    //      segment masters (rename CostCenter, recode Department) don't
    //      retroactively change history. AccountingKey rows are immutable once
    //      created.
    //
    // FORWARD-LOOKING — NOT IN THIS PR
    // --------------------------------
    // - JournalLine SourceModule / SourceDocumentId / SourceLineId traceability
    //   columns: promised by IPostingService ADR-025 D2 but never landed.
    //   Separate ship after PRA-5b verifies on prod.
    // - MaterialGroup / ProductGroup → AccountPostingProfile matrix wiring:
    //   the matrix table landed in PRA-7 but inventory-movement services don't
    //   consume it yet. Sprint 13 Wave 5 pickup.
    // - GlAccountRollupTag (N-to-N statutory tagging): future cap-table /
    //   reporting work.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §4 (COA deep dive)
    //   - docs/adr/ADR-003-central-gl-account-resolver.md (the cascade order
    //     ResolveAccountingKeyAsync extends)
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - SAP S/4 ACDOCA (Universal Journal segment columns)
    //   - Oracle EBS GL_CODE_COMBINATIONS (8-segment chart accelerator)
    //   - Microsoft Dynamics 365 LedgerDimension + DimensionAttributeValueCombination
    // =============================================================================
    [Table("AccountingKeys")]
    public class AccountingKey
    {
        public int Id { get; set; }

        /// <summary>
        /// Owning company (segment 1 of 8). Always NOT NULL — every JE post is
        /// company-scoped via JournalEntry.Book.CompanyId.
        /// </summary>
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        /// <summary>
        /// Site / Location (segment 2 of 8). NULL for corporate-overhead posts.
        /// Points at Location.Id (the site-scoped EAM hierarchy root).
        /// </summary>
        public int? SiteId { get; set; }
        public Location? Site { get; set; }

        /// <summary>
        /// Natural account (segment 3 of 8). Always NOT NULL — the GlAccount
        /// is the only segment that is structurally mandatory at posting time.
        /// </summary>
        public int AccountId { get; set; }
        public GlAccount? Account { get; set; }

        /// <summary>
        /// Cost center (segment 4 of 8). NOT NULL when
        /// <c>GlAccount.RequiresCostCenter</c>; nullable on the column so
        /// corporate-overhead accounts can post without one.
        /// </summary>
        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        /// <summary>
        /// Department (segment 5 of 8). NOT NULL when
        /// <c>GlAccount.RequiresDepartment</c>; nullable on the column.
        /// </summary>
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>
        /// Customer project (segment 6 of 8). NOT NULL for project-tracked
        /// accounts (work-order / MRO / aerospace contract jobs). Nullable
        /// on the column.
        /// </summary>
        public int? ProjectId { get; set; }

        /// <summary>
        /// Intercompany partner Company.Id (segment 7 of 8). Set ONLY on
        /// intercompany account pairs (IntercompanyReceivables /
        /// IntercompanyPayables / IntercompanySales / IntercompanyCogs etc.).
        /// </summary>
        public int? InterCoPartnerCompanyId { get; set; }

        /// <summary>
        /// Industry vertical (segment 8 of 8). Denormalized from
        /// <c>Company.IndustryVertical</c> at AccountingKey-create time so the
        /// segment dimensionality survives a future Company.IndustryVertical
        /// edit. NULL only when the resolver couldn't infer one (CompanyId
        /// orphan rows during backfill).
        /// </summary>
        public IndustryVertical? IndustryVertical { get; set; }

        /// <summary>
        /// SHA-256 hex (64 chars) of the canonical 8-segment string form.
        /// The (CompanyId, AccountingKeyHash) partial UNIQUE index is the
        /// find-or-insert lookup. NEVER mutated post-create.
        /// </summary>
        [Required, StringLength(64)]
        public string AccountingKeyHash { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable composite (e.g.
        /// <c>"Co=1|Site=17|Acct=5610|CC=110100|Dept=2009|Proj=14|ICP=|Vert=1"</c>).
        /// Cheap to compute, valuable for debugging and ad-hoc SQL.
        /// </summary>
        [StringLength(256)]
        public string? AccountingKeyString { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }
}
