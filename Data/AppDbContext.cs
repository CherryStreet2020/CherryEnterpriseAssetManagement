using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<Book> Books => Set<Book>();

        // Per-book GL account mappings
        public DbSet<BookGlAccount> BookGlAccounts => Set<BookGlAccount>();

        // ADR-003: per-tenant GL account configuration for the central
        // GL account resolver cascade.
        public DbSet<CompanyGlAccountConfig> CompanyGlAccountConfigs => Set<CompanyGlAccountConfig>();

        // Journals
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<JournalLine> JournalLines => Set<JournalLine>();

        // Sprint 13.5 PRA-5b — segment-keyed posting dimension. See
        // Models/Masters/AccountingKey.cs for the architecture; resolution
        // cascade lives in IGlAccountResolver.ResolveAccountingKeyAsync.
        public DbSet<Abs.FixedAssets.Models.Masters.AccountingKey> AccountingKeys =>
            Set<Abs.FixedAssets.Models.Masters.AccountingKey>();

        // CCA Tax Engine (Canada)
        public DbSet<CcaClass> CcaClasses => Set<CcaClass>();
        public DbSet<AssetTaxSettings> AssetTaxSettings => Set<AssetTaxSettings>();
        public DbSet<CcaClassBalance> CcaClassBalances => Set<CcaClassBalance>();
        public DbSet<CcaTransaction> CcaTransactions => Set<CcaTransaction>();

        // US Tax Engine
        public DbSet<UsTaxSettings> UsTaxSettings => Set<UsTaxSettings>();
        public DbSet<Section179Limits> Section179Limits => Set<Section179Limits>();
        public DbSet<BonusDepreciationRates> BonusDepreciationRates => Set<BonusDepreciationRates>();

        // Multi-Book Settings
        public DbSet<AssetBookSettings> AssetBookSettings => Set<AssetBookSettings>();

        // Asset Transfers and Improvements
        public DbSet<AssetTransfer> AssetTransfers => Set<AssetTransfer>();
        public DbSet<CapitalImprovement> CapitalImprovements => Set<CapitalImprovement>();

        // Tenants (multi-tenancy support)
        public DbSet<Tenant> Tenants => Set<Tenant>();

        // Company / Multi-Company
        public DbSet<Company> Companies => Set<Company>();

        // Sites & Locations
        public DbSet<Site> Sites => Set<Site>();

        // Asset Inventory & Tracking
        public DbSet<AssetInventory> AssetInventories => Set<AssetInventory>();
        public DbSet<InventoryList> InventoryLists => Set<InventoryList>();
        public DbSet<InventoryScan> InventoryScans => Set<InventoryScan>();

        // Asset Maintenance
        public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
        public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
        public DbSet<WorkRequest> WorkRequests => Set<WorkRequest>();
        public DbSet<LessonLearned> LessonsLearned => Set<LessonLearned>();

        // ADR-012 v0.2 / PR #119.2 — Unified WorkOrder configuration backbone.
        public DbSet<Abs.FixedAssets.Models.WorkOrders.WorkOrderFieldVisibility> WorkOrderFieldVisibility
            => Set<Abs.FixedAssets.Models.WorkOrders.WorkOrderFieldVisibility>();

        // ADR-012 v0.2 / PR #119.3 — Per-classification state machine.
        public DbSet<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusProfile> WorkOrderStatusProfile
            => Set<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusProfile>();
        public DbSet<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusLabel> WorkOrderStatusLabel
            => Set<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusLabel>();
        public DbSet<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusTransition> WorkOrderStatusTransition
            => Set<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusTransition>();

        // ADR-012 v0.2 / PR #119.4 — Polymorphic approval chain.
        public DbSet<Abs.FixedAssets.Models.WorkOrders.WorkOrderApproval> WorkOrderApproval
            => Set<Abs.FixedAssets.Models.WorkOrders.WorkOrderApproval>();

        // ADR-012 v0.2 / PR #119.5 — Atomic WO-number generator (SAP NRIV pattern).
        public DbSet<Abs.FixedAssets.Models.WorkOrders.NumberSequence> NumberSequence
            => Set<Abs.FixedAssets.Models.WorkOrders.NumberSequence>();

        // ADR-012 v0.2 / PR #119.8 — CIP satellite (Classification=CIP only).
        public DbSet<Abs.FixedAssets.Models.WorkOrders.CipWorkOrderDetails> CipWorkOrderDetails
            => Set<Abs.FixedAssets.Models.WorkOrders.CipWorkOrderDetails>();

        // ADR-012 v0.2 / PR #119.9 — Quality satellite (Classification=Quality only).
        public DbSet<Abs.FixedAssets.Models.WorkOrders.QualityWorkOrderDetails> QualityWorkOrderDetails
            => Set<Abs.FixedAssets.Models.WorkOrders.QualityWorkOrderDetails>();

        // ADR-012 v0.2 / PR #119.10 — Engineering satellite (Classification=Engineering only).
        public DbSet<Abs.FixedAssets.Models.WorkOrders.EngineeringWorkOrderDetails> EngineeringWorkOrderDetails
            => Set<Abs.FixedAssets.Models.WorkOrders.EngineeringWorkOrderDetails>();

        // ADR-012 v0.2 / PR #119.11 — HSE satellite (Classification=HSE only).
        public DbSet<Abs.FixedAssets.Models.WorkOrders.HseWorkOrderDetails> HseWorkOrderDetails
            => Set<Abs.FixedAssets.Models.WorkOrders.HseWorkOrderDetails>();

        // ADR-013 / PR #119.12 — Production-order header (sibling to WorkOrder).
        public DbSet<Abs.FixedAssets.Models.Production.ProductionOrder> ProductionOrders
            => Set<Abs.FixedAssets.Models.Production.ProductionOrder>();

        // ADR-013 / PR #119.12 — JobShop satellite (ProductionOrder.Type=JobShop only).
        public DbSet<Abs.FixedAssets.Models.Production.ProductionJobShopDetail> ProductionJobShopDetails
            => Set<Abs.FixedAssets.Models.Production.ProductionJobShopDetail>();

        // ADR-013 / PR #119.13a — Polymorphic ProductionBatch parent + subtypes.
        // Shared-operation batching backbone. Tulip-class composable MES at
        // schema level: one ProductionBatch row per physical execution, with
        // Nest or ProcessBatch subtype for type-specific fields.
        public DbSet<Abs.FixedAssets.Models.Production.ProductionBatch> ProductionBatches
            => Set<Abs.FixedAssets.Models.Production.ProductionBatch>();

        public DbSet<Abs.FixedAssets.Models.Production.Nest> Nests
            => Set<Abs.FixedAssets.Models.Production.Nest>();

        public DbSet<Abs.FixedAssets.Models.Production.ProcessBatch> ProcessBatches
            => Set<Abs.FixedAssets.Models.Production.ProcessBatch>();

        public DbSet<Abs.FixedAssets.Models.Production.ProductionBatchAllocation> ProductionBatchAllocations
            => Set<Abs.FixedAssets.Models.Production.ProductionBatchAllocation>();

        public DbSet<Abs.FixedAssets.Models.Production.ProductionBatchEquipmentLink> ProductionBatchEquipmentLinks
            => Set<Abs.FixedAssets.Models.Production.ProductionBatchEquipmentLink>();

        public DbSet<Abs.FixedAssets.Models.Production.ProductionBatchStateEvent> ProductionBatchStateEvents
            => Set<Abs.FixedAssets.Models.Production.ProductionBatchStateEvent>();

        // Stub FK targets so RecipeRevisionId and MrbDispositionId are
        // valid foreign keys from day one. Full content schemas land later.
        public DbSet<Abs.FixedAssets.Models.Production.RecipeRevision> RecipeRevisions
            => Set<Abs.FixedAssets.Models.Production.RecipeRevision>();

        public DbSet<Abs.FixedAssets.Models.Production.MrbDisposition> MrbDispositions
            => Set<Abs.FixedAssets.Models.Production.MrbDisposition>();

        // ADR-013 / PR #119.13b — Sheet & material traceability layer.
        // MaterialMaster reference + StockReceipt physical-lot records +
        // Remnant offcut child + CutListLine to-cut queue.
        public DbSet<Abs.FixedAssets.Models.Production.MaterialMaster> MaterialMasters
            => Set<Abs.FixedAssets.Models.Production.MaterialMaster>();

        public DbSet<Abs.FixedAssets.Models.Production.StockReceipt> StockReceipts
            => Set<Abs.FixedAssets.Models.Production.StockReceipt>();

        public DbSet<Abs.FixedAssets.Models.Production.Remnant> Remnants
            => Set<Abs.FixedAssets.Models.Production.Remnant>();

        public DbSet<Abs.FixedAssets.Models.Production.CutListLine> CutListLines
            => Set<Abs.FixedAssets.Models.Production.CutListLine>();

        // ADR-013 / PR #119.14 — Polymorphic MaterialStructure + subtypes +
        // shared lines + RecipePhases + RegulatoryProfiles. The third
        // polymorphic primitive (after ProductionBatch in #119.13a).
        public DbSet<Abs.FixedAssets.Models.Production.MaterialStructure> MaterialStructures
            => Set<Abs.FixedAssets.Models.Production.MaterialStructure>();

        public DbSet<Abs.FixedAssets.Models.Production.Bom> Boms
            => Set<Abs.FixedAssets.Models.Production.Bom>();

        public DbSet<Abs.FixedAssets.Models.Production.Recipe> Recipes
            => Set<Abs.FixedAssets.Models.Production.Recipe>();

        public DbSet<Abs.FixedAssets.Models.Production.MaterialStructureLine> MaterialStructureLines
            => Set<Abs.FixedAssets.Models.Production.MaterialStructureLine>();

        public DbSet<Abs.FixedAssets.Models.Production.RecipePhase> RecipePhases
            => Set<Abs.FixedAssets.Models.Production.RecipePhase>();

        public DbSet<Abs.FixedAssets.Models.Production.RegulatoryProfile> RegulatoryProfiles
            => Set<Abs.FixedAssets.Models.Production.RegulatoryProfile>();

        // Sprint 13.5 PR #5c — Routing + WorkCenter + ProductionOperation.
        // The MES dispatch + manufacturing-method layer. WorkCenter is the
        // dispatch unit (FK to N Assets via WorkCenterAssetLink for live state
        // rollup). Routing + RoutingOperation = the master template. Production-
        // Operation = the execution-time instance that DowntimeEvent / Scrap-
        // Event / ReworkEvent / MaterialConsumption / OeeEvent (PR #5e-#5g)
        // all FK to. Multi-vertical via RoutingType: Discrete / Repetitive /
        // ETO use these tables; ProcessBatch continues to use Recipe +
        // RecipePhase (above).
        public DbSet<Abs.FixedAssets.Models.Production.WorkCenter> WorkCenters
            => Set<Abs.FixedAssets.Models.Production.WorkCenter>();
        public DbSet<Abs.FixedAssets.Models.Production.WorkCenterAssetLink> WorkCenterAssetLinks
            => Set<Abs.FixedAssets.Models.Production.WorkCenterAssetLink>();
        // B11 R1-2 — alternate-routing links (WC spill targets for the R4 scheduler).
        public DbSet<Abs.FixedAssets.Models.Production.WorkCenterAlternate> WorkCenterAlternates
            => Set<Abs.FixedAssets.Models.Production.WorkCenterAlternate>();
        public DbSet<Abs.FixedAssets.Models.Production.Routing> Routings
            => Set<Abs.FixedAssets.Models.Production.Routing>();
        public DbSet<Abs.FixedAssets.Models.Production.RoutingOperation> RoutingOperations
            => Set<Abs.FixedAssets.Models.Production.RoutingOperation>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionOperation> ProductionOperations
            => Set<Abs.FixedAssets.Models.Production.ProductionOperation>();

        // Sprint 13.5 PR #5d — LaborEntries (operator clock-in/out events).
        public DbSet<Abs.FixedAssets.Models.Production.LaborEntry> LaborEntries
            => Set<Abs.FixedAssets.Models.Production.LaborEntry>();

        // Sprint 13.5 PR #5d (rolling PRA-3) — ReasonCodes catalog (Scrap/Rework/Downtime/Hold).
        public DbSet<Abs.FixedAssets.Models.Production.ReasonCode> ReasonCodes
            => Set<Abs.FixedAssets.Models.Production.ReasonCode>();

        // ADR-015 / Migration PR #1 — Industry-agnostic receipt profile catalog.
        // One row per industry vertical defining the JSON Schema, UiFormSpec,
        // promoted facets, default attributes, and regulatory gates that
        // apply to receipts of that profile. See ADR-015 D2.
        public DbSet<Abs.FixedAssets.Models.Production.ReceiptProfile> ReceiptProfiles
            => Set<Abs.FixedAssets.Models.Production.ReceiptProfile>();

        // ADR-014 / Sprint 4 PR #1 — Voice-ready infrastructure DbSets.
        // VoiceSessions: Sprint 5 multi-turn conversation state.
        // IdempotencyKeys: Stripe-pattern (UserId, Key) dedup.
        public DbSet<Abs.FixedAssets.Models.Infrastructure.VoiceSession> VoiceSessions
            => Set<Abs.FixedAssets.Models.Infrastructure.VoiceSession>();

        public DbSet<Abs.FixedAssets.Models.Infrastructure.IdempotencyKey> IdempotencyKeys
            => Set<Abs.FixedAssets.Models.Infrastructure.IdempotencyKey>();

        // Webhooks & Outbox
        public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
        public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
        public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();

        // Inbound Webhooks & Integrations
        public DbSet<IntegrationEndpoint> IntegrationEndpoints => Set<IntegrationEndpoint>();
        public DbSet<InboundEvent> InboundEvents => Set<InboundEvent>();
        public DbSet<IntegrationMapping> IntegrationMappings => Set<IntegrationMapping>();

        // Construction in Progress (CIP)
        public DbSet<CipProject> CipProjects => Set<CipProject>();
        public DbSet<CipCost> CipCosts => Set<CipCost>();
        public DbSet<CipBudgetLine> CipBudgetLines => Set<CipBudgetLine>();
        public DbSet<CipCapitalization> CipCapitalizations => Set<CipCapitalization>();
        public DbSet<CipCapitalizationCost> CipCapitalizationCosts => Set<CipCapitalizationCost>();

        // Sprint 13.5 PR #1 / ADR-026 — Customer-Project foundation.
        // Program is the optional v2 portfolio bucket. CustomerProject is the
        // customer-facing project entity (distinct from CipProject, which is
        // internal capital improvement). ProjectMember M:N supports joint-
        // venture / pass-through scenarios. ProjectPhase is a flat-but-tree-
        // capable WBS via nullable ParentPhaseId.
        public DbSet<Abs.FixedAssets.Models.Projects.Program> Programs
            => Set<Abs.FixedAssets.Models.Projects.Program>();
        public DbSet<Abs.FixedAssets.Models.Projects.CustomerProject> CustomerProjects
            => Set<Abs.FixedAssets.Models.Projects.CustomerProject>();
        public DbSet<Abs.FixedAssets.Models.Projects.ProjectMember> ProjectMembers
            => Set<Abs.FixedAssets.Models.Projects.ProjectMember>();
        public DbSet<Abs.FixedAssets.Models.Projects.ProjectPhase> ProjectPhases
            => Set<Abs.FixedAssets.Models.Projects.ProjectPhase>();

        // Sprint 13.5 PR #1.5 — append-only change-order log.
        // CustomerProjects.ContractValue stays the immutable baseline;
        // effective value = baseline + SUM(approved amendments.ValueDelta).
        // Postgres trigger fn_block_amendment_status_regression backstops
        // the append-only discipline.
        public DbSet<Abs.FixedAssets.Models.Projects.ProjectAmendment> ProjectAmendments
            => Set<Abs.FixedAssets.Models.Projects.ProjectAmendment>();

        // Sprint 13.5 PR #1.75 — AS9102 First Article Inspection workflow.
        // FaiReports = Form 1 header + lifecycle. FaiCharacteristics =
        // Form 3 per-balloon dim row. FaiProductAccountability = Form 2
        // material / spec / process / test rows. Attachments reused via
        // 3 new nullable FK cols (FaiReportId / FaiCharacteristicId /
        // FaiProductAccountabilityId).
        public DbSet<Abs.FixedAssets.Models.Quality.FaiReport> FaiReports
            => Set<Abs.FixedAssets.Models.Quality.FaiReport>();
        public DbSet<Abs.FixedAssets.Models.Quality.FaiCharacteristic> FaiCharacteristics
            => Set<Abs.FixedAssets.Models.Quality.FaiCharacteristic>();
        public DbSet<Abs.FixedAssets.Models.Quality.FaiProductAccountability> FaiProductAccountability
            => Set<Abs.FixedAssets.Models.Quality.FaiProductAccountability>();

        // Sprint 13.5 PR #337 — /Admin/AssetImport bulk Excel upload.
        // AssetImportBatches = header (one per .xlsx upload). AssetImportRows
        // = per-row staging with raw text + resolved FKs + validation errors
        // + CommittedAssetId stamped on commit. See
        // docs/research/asset-import-pr337-spec-2026-05-25.md.
        public DbSet<Abs.FixedAssets.Models.AssetImport.AssetImportBatch> AssetImportBatches
            => Set<Abs.FixedAssets.Models.AssetImport.AssetImportBatch>();
        public DbSet<Abs.FixedAssets.Models.AssetImport.AssetImportRow> AssetImportRows
            => Set<Abs.FixedAssets.Models.AssetImport.AssetImportRow>();

        // Sprint 13.5 PRA-2 — Country / Subdivision / WorkCalendar / Holiday
        // masters. Countries + Subdivisions are system-wide reference data
        // (NULL CompanyId not applicable — they're global). WorkCalendars
        // and Holidays are tenant-scoped with NULL-CompanyId system fallback
        // (same Carriers pattern as PRA-1).
        public DbSet<Abs.FixedAssets.Models.Masters.Country> Countries
            => Set<Abs.FixedAssets.Models.Masters.Country>();
        public DbSet<Abs.FixedAssets.Models.Masters.Subdivision> Subdivisions
            => Set<Abs.FixedAssets.Models.Masters.Subdivision>();
        public DbSet<Abs.FixedAssets.Models.Masters.WorkCalendar> WorkCalendars
            => Set<Abs.FixedAssets.Models.Masters.WorkCalendar>();
        public DbSet<Abs.FixedAssets.Models.Masters.Holiday> Holidays
            => Set<Abs.FixedAssets.Models.Masters.Holiday>();

        // Sprint 13.5 PRA-4 — Unified UOM master.
        // Replaces the two parallel enums (Models.Item.UnitOfMeasure +
        // Models.Telemetry.UnitOfMeasure) with one master table per the
        // master-files-baseline-2026-05-24 memo.
        public DbSet<Abs.FixedAssets.Models.Masters.UomCategory> UomCategories
            => Set<Abs.FixedAssets.Models.Masters.UomCategory>();
        public DbSet<Abs.FixedAssets.Models.Masters.UnitOfMeasureMaster> UnitsOfMeasure
            => Set<Abs.FixedAssets.Models.Masters.UnitOfMeasureMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.UomConversion> UomConversions
            => Set<Abs.FixedAssets.Models.Masters.UomConversion>();

        // Sprint 13.5 PRA-6 — Currency / PaymentTerm / TaxAuthority / TaxCode
        // master shapes. Naming pattern: appending "Master" to dodge name
        // collision with the pre-existing thin entities in Models/SystemConfig.cs
        // that already own the Currencies/PaymentTerms/TaxCodes table names.
        // The existing thin entities stay for back-compat (5 services depend
        // on them); the new *Master tables carry the BIC-compliant shape with
        // CompanyId nullable cross-tenant ref + partial UNIQUEs + extended
        // metadata (ISO 4217 decimals, 2/10 N30 discount fields, tax-authority
        // FK, etc.). See docs/research/master-files-baseline-2026-05-24.md §6.
        public DbSet<Abs.FixedAssets.Models.Masters.CurrencyMaster> CurrencyMasters
            => Set<Abs.FixedAssets.Models.Masters.CurrencyMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.PaymentTermMaster> PaymentTermMasters
            => Set<Abs.FixedAssets.Models.Masters.PaymentTermMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.TaxAuthority> TaxAuthorities
            => Set<Abs.FixedAssets.Models.Masters.TaxAuthority>();
        public DbSet<Abs.FixedAssets.Models.Masters.TaxCodeMaster> TaxCodeMasters
            => Set<Abs.FixedAssets.Models.Masters.TaxCodeMaster>();

        // Sprint 13.5 PRA-7 — Warehouse + Bin + Lot + Serial + ItemGroup +
        // PostingProfile (Master Files Baseline cascade ship #5 of 8).
        // SAP S/4 + Dynamics 365 separation-of-concerns shape: EAM Location
        // stays as the asset hierarchy; WarehouseMaster + BinMaster carry the
        // financial inventory side; LotMaster + SerialMaster the traceability
        // spine; ItemGroup + PostingProfile the GL routing matrix.
        // See docs/ADR-019-wms-posting-profile-pattern.md.
        public DbSet<Abs.FixedAssets.Models.Masters.WarehouseMaster> WarehouseMasters
            => Set<Abs.FixedAssets.Models.Masters.WarehouseMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.BinMaster> BinMasters
            => Set<Abs.FixedAssets.Models.Masters.BinMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.LotMaster> LotMasters
            => Set<Abs.FixedAssets.Models.Masters.LotMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.SerialMaster> SerialMasters
            => Set<Abs.FixedAssets.Models.Masters.SerialMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.ItemGroup> ItemGroups
            => Set<Abs.FixedAssets.Models.Masters.ItemGroup>();
        public DbSet<Abs.FixedAssets.Models.Masters.PostingProfile> PostingProfiles
            => Set<Abs.FixedAssets.Models.Masters.PostingProfile>();

        // Sprint 13.5 PRA-8 — Employee + WageGroup + LaborRateMaster
        // (Master Files Baseline cascade ship #6 of 10). HR org master,
        // hourly band classification, effective-dated rate matrix.
        // LaborRateMaster suffix dodges legacy LaborRate collision in
        // Models/LaborConfig.cs (DEF-008 pattern, same as PRA-6).
        public DbSet<Abs.FixedAssets.Models.Masters.Employee> Employees
            => Set<Abs.FixedAssets.Models.Masters.Employee>();
        public DbSet<Abs.FixedAssets.Models.Masters.WageGroup> WageGroups
            => Set<Abs.FixedAssets.Models.Masters.WageGroup>();
        public DbSet<Abs.FixedAssets.Models.Masters.LaborRateMaster> LaborRateMasters
            => Set<Abs.FixedAssets.Models.Masters.LaborRateMaster>();

        // Sprint 13.5 PRA-9 — PriceListMaster + PriceListLine + DiscountSchema
        // + RebateAgreement (Master Files Baseline cascade ship #7 of 10).
        // Customer pricing + promotional / contract discounts + back-end
        // rebates. Ships alongside ADR-027 (locks SalesOrder→Line→Release
        // shape for Sprint 19+).
        public DbSet<Abs.FixedAssets.Models.Masters.PriceListMaster> PriceListMasters
            => Set<Abs.FixedAssets.Models.Masters.PriceListMaster>();
        public DbSet<Abs.FixedAssets.Models.Masters.PriceListLine> PriceListLines
            => Set<Abs.FixedAssets.Models.Masters.PriceListLine>();
        public DbSet<Abs.FixedAssets.Models.Masters.DiscountSchema> DiscountSchemas
            => Set<Abs.FixedAssets.Models.Masters.DiscountSchema>();
        public DbSet<Abs.FixedAssets.Models.Masters.RebateAgreement> RebateAgreements
            => Set<Abs.FixedAssets.Models.Masters.RebateAgreement>();

        // Sprint 13.5 PRA-10 — TaxRateMaster (Master Files Baseline cascade
        // ship #8 of 10). Effective-dated tax rate matrix joining
        // (TaxCode × Jurisdiction × ProductClass × DateRange) → rate.
        // AvaTax / Vertex-style resolution. Caps PRA-6's TaxAuthority +
        // TaxCodeMaster (the SHAPE) with the actual rates at points in time.
        public DbSet<Abs.FixedAssets.Models.Masters.TaxRateMaster> TaxRateMasters
            => Set<Abs.FixedAssets.Models.Masters.TaxRateMaster>();

        // Sprint 13.5 PRA-11 — PackLevel + ItemPackHierarchy (Master Files
        // Baseline cascade ship #9 of 10). GS1-style named pack tiers
        // (Each/Inner/Case/Pallet/Truck) + per-Item physical config
        // (qty, dimensions, weights, barcodes) at each tier. Drives WMS
        // slotting, shipping label generation, MRP rounding.
        public DbSet<Abs.FixedAssets.Models.Masters.PackLevel> PackLevels
            => Set<Abs.FixedAssets.Models.Masters.PackLevel>();
        public DbSet<Abs.FixedAssets.Models.Masters.ItemPackHierarchy> ItemPackHierarchies
            => Set<Abs.FixedAssets.Models.Masters.ItemPackHierarchy>();

        // Users
        public DbSet<User> Users => Set<User>();

        // Audit Trail
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<PeriodLock> PeriodLocks => Set<PeriodLock>();

        // Fiscal Calendar
        public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
        public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
        public DbSet<DepreciationRun> DepreciationRuns => Set<DepreciationRun>();
        public DbSet<DepreciationRunDetail> DepreciationRunDetails => Set<DepreciationRunDetail>();

        // Depreciation Policies
        public DbSet<DepreciationPolicy> DepreciationPolicies => Set<DepreciationPolicy>();
        public DbSet<UsefulLifeTable> UsefulLifeTables => Set<UsefulLifeTable>();
        public DbSet<UsefulLifeEntry> UsefulLifeEntries => Set<UsefulLifeEntry>();
        public DbSet<PolicyCategoryDefault> PolicyCategoryDefaults => Set<PolicyCategoryDefault>();

        // Advanced Transactions
        public DbSet<PartialDisposal> PartialDisposals => Set<PartialDisposal>();
        public DbSet<BulkOperation> BulkOperations => Set<BulkOperation>();

        // API Keys
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

        // Attachments
        public DbSet<Attachment> Attachments => Set<Attachment>();

        // Exchange Rates
        public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

        // Technicians, Project Managers, Manufacturers
        public DbSet<Technician> Technicians => Set<Technician>();
        public DbSet<TechnicianCertification> TechnicianCertifications => Set<TechnicianCertification>();
        public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();
        public DbSet<ProjectManager> ProjectManagers => Set<ProjectManager>();
        public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

        // Enterprise Master Files
        public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
        public DbSet<CostCenter> CostCenters => Set<CostCenter>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
        public DbSet<Vendor> Vendors => Set<Vendor>();

        // Sprint 13.5 PRA-1 — first-class Carrier master.
        // Replaces free-text Carrier on AdvancedShippingNotice + ShippingMethod.
        // 12 system-wide seed rows (UPS / FedEx / DHL / USPS / etc.) inserted by migration.
        public DbSet<Carrier> Carriers => Set<Carrier>();

        // Purchasing & Accounts Payable
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
        public DbSet<PurchaseOrderRelease> PurchaseOrderReleases => Set<PurchaseOrderRelease>();
        public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
        public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();

        // Sprint 15.1 PR-2 — Production Supply Demand unified demand record + M:M allocation.
        public DbSet<Abs.FixedAssets.Models.Production.ProductionSupplyDemand> ProductionSupplyDemands
            => Set<Abs.FixedAssets.Models.Production.ProductionSupplyDemand>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionSupplyAllocation> ProductionSupplyAllocations
            => Set<Abs.FixedAssets.Models.Production.ProductionSupplyAllocation>();

        // Sprint 15.1 PR-3 — PO line ↔ Demand consolidation traceability link.
        public DbSet<PurchaseOrderLineDemandLink> PurchaseOrderLineDemandLinks
            => Set<PurchaseOrderLineDemandLink>();

        // Sprint 15.1 PR-4 — Subcontract Operation + Dual Demand binding.
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractOperation> SubcontractOperations
            => Set<Abs.FixedAssets.Models.Production.SubcontractOperation>();
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractDemand> SubcontractDemands
            => Set<Abs.FixedAssets.Models.Production.SubcontractDemand>();

        // Sprint 15.1 PR-5 — Vendor WIP physical-lot tracking (3 entities).
        public DbSet<VendorLocation> VendorLocations => Set<VendorLocation>();
        public DbSet<VendorWipBalance> VendorWipBalances => Set<VendorWipBalance>();
        public DbSet<VendorWipTransaction> VendorWipTransactions => Set<VendorWipTransaction>();

        // Sprint 15.2 PR-6 — SubcontractShipment + SubcontractReceipt (physical
        // WIP-to-vendor / vendor-to-us events with lot/serial/revision tracking +
        // 10 §11 receipt scenarios).
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractShipment> SubcontractShipments
            => Set<Abs.FixedAssets.Models.Production.SubcontractShipment>();
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractShipmentLine> SubcontractShipmentLines
            => Set<Abs.FixedAssets.Models.Production.SubcontractShipmentLine>();
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractReceipt> SubcontractReceipts
            => Set<Abs.FixedAssets.Models.Production.SubcontractReceipt>();
        public DbSet<Abs.FixedAssets.Models.Production.SubcontractReceiptLine> SubcontractReceiptLines
            => Set<Abs.FixedAssets.Models.Production.SubcontractReceiptLine>();

        // Sprint 15.4 PR-16 — PO Acknowledgment / Vendor Confirmation.
        // Header + per-line vendor confirmation with 7-state lifecycle, 5
        // delivery methods, 8 line-exception types. One IsCurrent ack per PO
        // at a time; history preserved for PR-17 vendor re-ack loop.
        public DbSet<POAcknowledgment> POAcknowledgments => Set<POAcknowledgment>();
        public DbSet<POAcknowledgmentLine> POAcknowledgmentLines => Set<POAcknowledgmentLine>();

        // Sprint 15.4 PR-17 — PO Amendment / Change Order (POChangeHistory).
        // Header + per-line snapshot for post-approval PO modifications with
        // 7-state lifecycle (Draft → Previewed → PendingApproval → Approved
        // → Applied | Rejected | Cancelled), 10-value ChangeReason enum, and
        // 7-value LineChangeType enum. THE BIC differentiator caches impact
        // preview counts on the header so the Razor partial can render the
        // affected-demand-link table without re-walking the graph.
        public DbSet<POChangeHistory> POChangeHistories => Set<POChangeHistory>();
        public DbSet<POChangeHistoryLine> POChangeHistoryLines => Set<POChangeHistoryLine>();

        // Sprint 15.4 PR-18 — Vendor Performance / Scorecard (SupplierPerformance).
        // Computed snapshot of OTD %, quality PPM, price variance %, and NCR
        // count per (Vendor, PeriodType) rolling window. One IsCurrent snapshot
        // per pair; history preserved. Feeds §21 tab 13 + PR-20 quote ranker.
        public DbSet<SupplierPerformance> SupplierPerformances => Set<SupplierPerformance>();

        // Sprint 15.4 PR-19 — 3-Way Match (PO ↔ Receipt ↔ Invoice). Persisted
        // match runs with per-line price/qty/date variance + outcome. One
        // IsCurrent result per invoice; exceptions feed the Cost Exceptions tab.
        public DbSet<InvoiceMatchResult> InvoiceMatchResults => Set<InvoiceMatchResult>();
        public DbSet<InvoiceMatchResultLine> InvoiceMatchResultLines => Set<InvoiceMatchResultLine>();

        // Sprint 15.4 PR-20 — RFQ / Quote Flow (CLOSES the purchasing cascade).
        // SupplierRFQ + lines + per-supplier SupplierQuote + lines. Composite
        // ranker stamps score/rank/winner on the quote; awarded quote converts
        // to a Draft PO carrying §17 demand links.
        public DbSet<SupplierRFQ> SupplierRFQs => Set<SupplierRFQ>();
        public DbSet<SupplierRFQLine> SupplierRFQLines => Set<SupplierRFQLine>();
        public DbSet<SupplierQuote> SupplierQuotes => Set<SupplierQuote>();
        public DbSet<SupplierQuoteLine> SupplierQuoteLines => Set<SupplierQuoteLine>();

        // Sprint 12A PR #6 — ASN domain entity (first-class) + lines.
        // Replaces the placeholder "ASN:" prefix on StockReceipt.SourcePoNumber.
        // Real EDI 856 ingestion + AS2 trading-partner pipeline lands in
        // Sprint 21 (MCP + Agentic AI Launch Package); for now seed data.
        public DbSet<AdvancedShippingNotice> AdvancedShippingNotices => Set<AdvancedShippingNotice>();
        public DbSet<AsnLine> AsnLines => Set<AsnLine>();
        public DbSet<VendorInvoice> VendorInvoices => Set<VendorInvoice>();
        public DbSet<VendorInvoiceLine> VendorInvoiceLines => Set<VendorInvoiceLine>();
        public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();

        // Item Master & Inventory
        public DbSet<Item> Items => Set<Item>();
        public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
        public DbSet<ItemVendor> ItemVendors => Set<ItemVendor>();
        public DbSet<ItemRevision> ItemRevisions => Set<ItemRevision>();
        public DbSet<ItemInventory> ItemInventories2 => Set<ItemInventory>();
        public DbSet<ItemTransaction> ItemTransactions => Set<ItemTransaction>();
        public DbSet<ItemImage> ItemImages => Set<ItemImage>();
        public DbSet<ItemCompanyStocking> ItemCompanyStockings => Set<ItemCompanyStocking>();

        // B6 Foundation Sprint PR-FS-2 (2026-05-26) — per-Site Item override rows.
        public DbSet<Abs.FixedAssets.Models.Masters.ItemSite> ItemSites => Set<Abs.FixedAssets.Models.Masters.ItemSite>();

        // B6 Foundation Sprint PR-FS-3 (2026-05-26) — SAP Cost Component Split.
        // Per-Item / per-Site cost element breakdown rows, effective-dated.
        public DbSet<Abs.FixedAssets.Models.Masters.ItemStandardCostElement> ItemStandardCostElements => Set<Abs.FixedAssets.Models.Masters.ItemStandardCostElement>();

        // B6 Foundation Sprint PR-FS-4 (2026-05-26) — FIFO/LIFO/Average cost layers.
        // Per-Item / per-Site inventory valuation layers. SAP MM "stock with values"
        // equivalent. Immutable receipts; consumption decrements RemainingQuantity.
        public DbSet<Abs.FixedAssets.Models.Masters.CostLayer> CostLayers => Set<Abs.FixedAssets.Models.Masters.CostLayer>();

        // B6 Foundation Sprint PR-FS-5 (2026-05-26) — multi-source AVL + priority
        // rules per (Item, optional Site, Vendor). SAP S/4 Source List equivalent.
        // Drives MRP, Make-or-Buy decision input, AS9100 §8.4.1 audit trail.
        public DbSet<Abs.FixedAssets.Models.Masters.ItemSourcingRule> ItemSourcingRules => Set<Abs.FixedAssets.Models.Masters.ItemSourcingRule>();

        // B6 Foundation Sprint PR-FS-6 (2026-05-26) — customer-part-number
        // cross-reference (SAP CMIR equivalent). Bidirectional resolution:
        // customer PN → Item at SO ingest; Item → customer PN at ship/invoice.
        public DbSet<Abs.FixedAssets.Models.Masters.CustomerItemXref> CustomerItemXrefs => Set<Abs.FixedAssets.Models.Masters.CustomerItemXref>();

        // Sprint 14.1 PR-1 (2026-05-26) — per-PO frozen BOM snapshot.
        // Captured by IPoSnapshotService.CaptureAsync at PRO release. Survives
        // subsequent engineering changes to the source MaterialStructure /
        // MaterialStructureLine so cost rollups, MES material-issue, AS9100
        // §8.3 traceability, and ECR-ECO impact analysis all read from a
        // deterministic per-PO snapshot rather than the live engineering view.
        public DbSet<Abs.FixedAssets.Models.Production.ProductionMaterialStructure> ProductionMaterialStructures =>
            Set<Abs.FixedAssets.Models.Production.ProductionMaterialStructure>();

        // Sprint 14.2 PR-1 (2026-05-26 evening) — DMS substrate.
        // Document = controlled engineering artifact with lifecycle.
        // DocumentVersion = monotonic per-revision satellite with content
        // hash + URI + supersession chain.
        // ItemDocumentLink = M:N between Items and Documents by purpose
        // (BillOfDrawing / Specification / InspectionPlan / etc.).
        public DbSet<Abs.FixedAssets.Models.Engineering.Document> Documents =>
            Set<Abs.FixedAssets.Models.Engineering.Document>();
        public DbSet<Abs.FixedAssets.Models.Engineering.DocumentVersion> DocumentVersions =>
            Set<Abs.FixedAssets.Models.Engineering.DocumentVersion>();
        public DbSet<Abs.FixedAssets.Models.Engineering.ItemDocumentLink> ItemDocumentLinks =>
            Set<Abs.FixedAssets.Models.Engineering.ItemDocumentLink>();

        // Sprint 14.3 PR-1 (2026-05-27) — ECR/ECO Change Control substrate.
        // EngineeringChangeRequest = controlled intake for proposed changes.
        // EngineeringChangeOrder = approved execution record; drives
        //   DocumentVersion supersede (via IDocumentService), FAI re-trigger,
        //   customer/regulatory notice, and in-flight PRO impact.
        // EcoLineItem = one affected Item/Document/DocumentVersion per ECO row.
        // EcoApproval = one stage in the ECO's multi-stage approval chain.
        public DbSet<Abs.FixedAssets.Models.Engineering.EngineeringChangeRequest> EngineeringChangeRequests =>
            Set<Abs.FixedAssets.Models.Engineering.EngineeringChangeRequest>();
        public DbSet<Abs.FixedAssets.Models.Engineering.EngineeringChangeOrder> EngineeringChangeOrders =>
            Set<Abs.FixedAssets.Models.Engineering.EngineeringChangeOrder>();
        public DbSet<Abs.FixedAssets.Models.Engineering.EcoLineItem> EcoLineItems =>
            Set<Abs.FixedAssets.Models.Engineering.EcoLineItem>();
        public DbSet<Abs.FixedAssets.Models.Engineering.EcoApproval> EcoApprovals =>
            Set<Abs.FixedAssets.Models.Engineering.EcoApproval>();

        // Sprint 14.3 PR-2 — Deviation (short-term engineering exception)
        public DbSet<Abs.FixedAssets.Models.Engineering.Deviation> Deviations =>
            Set<Abs.FixedAssets.Models.Engineering.Deviation>();

        // Sprint 14.3 PR-3 — Waiver (customer-approved longer-term divergence)
        public DbSet<Abs.FixedAssets.Models.Engineering.Waiver> Waivers =>
            Set<Abs.FixedAssets.Models.Engineering.Waiver>();

        // Sprint 14.3 PR-4 — Concession (retroactive customer acceptance of non-conforming material)
        public DbSet<Abs.FixedAssets.Models.Engineering.Concession> Concessions =>
            Set<Abs.FixedAssets.Models.Engineering.Concession>();

        // Sprint 14.3 PR-5 — Customer Notice (outbound change notification to customers)
        public DbSet<Abs.FixedAssets.Models.Engineering.CustomerNotice> CustomerNotices =>
            Set<Abs.FixedAssets.Models.Engineering.CustomerNotice>();

        // Sprint 14.3 PR-5 — Supplier PCN (outbound process change notification to suppliers)
        public DbSet<Abs.FixedAssets.Models.Engineering.SupplierProcessChangeNotification> SupplierProcessChangeNotifications =>
            Set<Abs.FixedAssets.Models.Engineering.SupplierProcessChangeNotification>();

        // B8 PR-PRO-3 — ProductionMaterialTransaction (material movement log)
        public DbSet<Abs.FixedAssets.Models.Production.ProductionMaterialTransaction> ProductionMaterialTransactions =>
            Set<Abs.FixedAssets.Models.Production.ProductionMaterialTransaction>();

        // Sprint 14.3 PR-6 — CorrectiveActionRequest (CAR/CAPA — 8D quality lifecycle)
        public DbSet<Abs.FixedAssets.Models.Engineering.CorrectiveActionRequest> CorrectiveActionRequests =>
            Set<Abs.FixedAssets.Models.Engineering.CorrectiveActionRequest>();

        // B8 PR-PRO-6 — Complete + Scrap + Rework events
        public DbSet<Abs.FixedAssets.Models.Production.ProductionCompletionEvent> ProductionCompletionEvents =>
            Set<Abs.FixedAssets.Models.Production.ProductionCompletionEvent>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionScrapEvent> ProductionScrapEvents =>
            Set<Abs.FixedAssets.Models.Production.ProductionScrapEvent>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionReworkEvent> ProductionReworkEvents =>
            Set<Abs.FixedAssets.Models.Production.ProductionReworkEvent>();

        // B8 PR-PRO-5 — ProductionWipMove (auto-advance + manual moves between operations)
        public DbSet<Abs.FixedAssets.Models.Production.ProductionWipMove> ProductionWipMoves =>
            Set<Abs.FixedAssets.Models.Production.ProductionWipMove>();

        // Sprint 14.4 PR-1 — CostTransaction (atomic cost ledger), CostTransfer (inter-object value movement),
        // ProductionOrderCostSummary (denormalized cockpit cache)
        public DbSet<Abs.FixedAssets.Models.Production.CostTransaction> CostTransactions =>
            Set<Abs.FixedAssets.Models.Production.CostTransaction>();
        public DbSet<Abs.FixedAssets.Models.Production.CostTransfer> CostTransfers =>
            Set<Abs.FixedAssets.Models.Production.CostTransfer>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionOrderCostSummary> ProductionOrderCostSummaries =>
            Set<Abs.FixedAssets.Models.Production.ProductionOrderCostSummary>();

        // Theme B7 Wave B PR-4 — ItemCrystallization (the harvest-from-actuals
        // audit record: PoFirst PRO's as-built BOM + as-run routing + actual
        // cost promoted into an Item Master standard at ship, or deduped+linked).
        public DbSet<Abs.FixedAssets.Models.Production.ItemCrystallization> ItemCrystallizations =>
            Set<Abs.FixedAssets.Models.Production.ItemCrystallization>();

        // Sprint 14.4 PR-3 — CostRollupRun (rollup execution header), CostRollupLine (output lines),
        // CostRollupException (detected issues). The cost rollup engine audit trail.
        public DbSet<Abs.FixedAssets.Models.Production.CostRollupRun> CostRollupRuns =>
            Set<Abs.FixedAssets.Models.Production.CostRollupRun>();
        public DbSet<Abs.FixedAssets.Models.Production.CostRollupLine> CostRollupLines =>
            Set<Abs.FixedAssets.Models.Production.CostRollupLine>();
        public DbSet<Abs.FixedAssets.Models.Production.CostRollupException> CostRollupExceptions =>
            Set<Abs.FixedAssets.Models.Production.CostRollupException>();

        // Sprint 14.4 PR-4 — ProductionVariance (5+ variance computations per PRO),
        // ProductionCloseEvent (close workflow audit trail with reversal support).
        public DbSet<Abs.FixedAssets.Models.Production.ProductionVariance> ProductionVariances =>
            Set<Abs.FixedAssets.Models.Production.ProductionVariance>();
        public DbSet<Abs.FixedAssets.Models.Production.ProductionCloseEvent> ProductionCloseEvents =>
            Set<Abs.FixedAssets.Models.Production.ProductionCloseEvent>();

        // Sprint 14.3 PR-7 — ChangeImpactAnalysis (ECO blast-radius analysis + FAI re-trigger)
        public DbSet<Abs.FixedAssets.Models.Engineering.ChangeImpactAnalysis> ChangeImpactAnalyses =>
            Set<Abs.FixedAssets.Models.Engineering.ChangeImpactAnalysis>();
        public DbSet<Abs.FixedAssets.Models.Engineering.ChangeImpactLine> ChangeImpactLines =>
            Set<Abs.FixedAssets.Models.Engineering.ChangeImpactLine>();

        // Sprint 14.3 PR-7 — DocumentRedline (structured markup annotations on document versions)
        public DbSet<Abs.FixedAssets.Models.Engineering.DocumentRedline> DocumentRedlines =>
            Set<Abs.FixedAssets.Models.Engineering.DocumentRedline>();

        // B8 PR-PRO-4 — ProductionOperationTransaction (operation state change log)
        public DbSet<Abs.FixedAssets.Models.Production.ProductionOperationTransaction> ProductionOperationTransactions =>
            Set<Abs.FixedAssets.Models.Production.ProductionOperationTransaction>();

        // Purchase Requisitions & Reorder Alerts
        public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
        public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => Set<PurchaseRequisitionLine>();
        public DbSet<ReorderAlert> ReorderAlerts => Set<ReorderAlert>();

        // PM Templates & Maintenance Kits
        public DbSet<PMTemplate> PMTemplates => Set<PMTemplate>();
        public DbSet<PMTemplateItem> PMTemplateItems => Set<PMTemplateItem>();
        public DbSet<PMTemplateAsset> PMTemplateAssets => Set<PMTemplateAsset>();
        public DbSet<PMSchedule> PMSchedules => Set<PMSchedule>();
        public DbSet<PMOccurrence> PMOccurrences => Set<PMOccurrence>();
        public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
        public DbSet<Kit> Kits => Set<Kit>();
        public DbSet<KitItem> KitItems => Set<KitItem>();
        public DbSet<WorkOrderPart> WorkOrderParts => Set<WorkOrderPart>();
        
        // PM Template Revisions
        public DbSet<Abs.FixedAssets.Models.Revisions.PMTemplateRevision> PMTemplateRevisions => Set<Abs.FixedAssets.Models.Revisions.PMTemplateRevision>();
        public DbSet<Abs.FixedAssets.Models.Revisions.PMTemplateRevisionOperation> PMTemplateRevisionOperations => Set<Abs.FixedAssets.Models.Revisions.PMTemplateRevisionOperation>();

        // Item Cross-Reference (Sprint 11)
        public DbSet<Abs.FixedAssets.Models.Revisions.ItemManufacturerPart> ItemManufacturerParts => Set<Abs.FixedAssets.Models.Revisions.ItemManufacturerPart>();
        public DbSet<Abs.FixedAssets.Models.Revisions.VendorItemPart> VendorItemParts => Set<Abs.FixedAssets.Models.Revisions.VendorItemPart>();

        // Procurement-Grade Parts (Sprint 12)
        public DbSet<ItemApprovedVendor> ItemApprovedVendors => Set<ItemApprovedVendor>();
        public DbSet<ItemAlternate> ItemAlternates => Set<ItemAlternate>();
        public DbSet<ItemSupersession> ItemSupersessions => Set<ItemSupersession>();

        // System Configuration Tables
        public DbSet<NumberingSequence> NumberingSequences => Set<NumberingSequence>();
        public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
        public DbSet<UOMDefinition> UOMDefinitions => Set<UOMDefinition>();
        public DbSet<Currency> Currencies => Set<Currency>();
        public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
        public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
        public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
        // Sprint 2 PR #115 — Approval Hierarchy + SoD: immutable decision log.
        public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
        // Sprint 2 PR #117.1 — Real sensor history. Source of truth for the
        // denormalized Asset.Current* cache columns.
        public DbSet<AssetSensorReading> AssetSensorReadings => Set<AssetSensorReading>();

        // Sprint 2 PR #117.2 — Equipment Catalog (per Dean: "Best in Class
        // Process to Produce a Best In Class product"). EquipmentClass +
        // EquipmentModel + SensorProfile are the curated source of asset
        // attributes — replaces hardcoded C# arrays in IndustrialAssetSeeder.
        public DbSet<Abs.FixedAssets.Models.Catalog.EquipmentClass> EquipmentClasses => Set<Abs.FixedAssets.Models.Catalog.EquipmentClass>();
        public DbSet<Abs.FixedAssets.Models.Catalog.EquipmentModel> EquipmentModels => Set<Abs.FixedAssets.Models.Catalog.EquipmentModel>();
        public DbSet<Abs.FixedAssets.Models.Catalog.SensorProfile> SensorProfiles => Set<Abs.FixedAssets.Models.Catalog.SensorProfile>();

        // Sprint 2 PR #118.1 — Industrial Sensor Data Architecture (ADR-011).
        // Six-table substrate backed by TimescaleDB hypertable + continuous
        // aggregates. See docs/adr/ADR-011-industrial-sensor-data-architecture.md.
        // Implementation (ISensorIngestService etc.) lands in PR #118.2.
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorEvent> SensorEvents => Set<Abs.FixedAssets.Models.Telemetry.SensorEvent>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorRollupMinute> SensorRollupMinutes => Set<Abs.FixedAssets.Models.Telemetry.SensorRollupMinute>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorRollupHour> SensorRollupHours => Set<Abs.FixedAssets.Models.Telemetry.SensorRollupHour>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorRollupDay> SensorRollupDays => Set<Abs.FixedAssets.Models.Telemetry.SensorRollupDay>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.AssetSensorLatest> AssetSensorLatest => Set<Abs.FixedAssets.Models.Telemetry.AssetSensorLatest>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorSnapshot> SensorSnapshots => Set<Abs.FixedAssets.Models.Telemetry.SensorSnapshot>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorSnapshotValue> SensorSnapshotValues => Set<Abs.FixedAssets.Models.Telemetry.SensorSnapshotValue>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.SensorAlarm> SensorAlarms => Set<Abs.FixedAssets.Models.Telemetry.SensorAlarm>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.AlarmRationalization> AlarmRationalizations => Set<Abs.FixedAssets.Models.Telemetry.AlarmRationalization>();
        public DbSet<Abs.FixedAssets.Models.Telemetry.UnitConversion> UnitConversions => Set<Abs.FixedAssets.Models.Telemetry.UnitConversion>();

        // Work Order Code Tables
        public DbSet<WorkOrderType> WorkOrderTypes => Set<WorkOrderType>();
        public DbSet<MaintenanceTypeCode> MaintenanceTypeCodes => Set<MaintenanceTypeCode>();
        public DbSet<FailureCode> FailureCodes => Set<FailureCode>();
        public DbSet<CauseCode> CauseCodes => Set<CauseCode>();
        public DbSet<ActionCode> ActionCodes => Set<ActionCode>();
        public DbSet<ProblemCode> ProblemCodes => Set<ProblemCode>();
        public DbSet<PriorityLevel> PriorityLevels => Set<PriorityLevel>();

        // Labor Configuration Tables
        public DbSet<LaborType> LaborTypes => Set<LaborType>();
        public DbSet<LaborRate> LaborRates => Set<LaborRate>();
        public DbSet<Craft> Crafts => Set<Craft>();
        public DbSet<Skill> Skills => Set<Skill>();

        // Lookup Tables (Reference Data)
        public DbSet<LookupType> LookupTypes => Set<LookupType>();
        public DbSet<LookupValue> LookupValues => Set<LookupValue>();

        // Work Order Operations
        public DbSet<WorkOrderOperation> WorkOrderOperations => Set<WorkOrderOperation>();
        public DbSet<WorkOrderOperationLabor> WorkOrderOperationLabors => Set<WorkOrderOperationLabor>();
        public DbSet<WorkOrderOperationTool> WorkOrderOperationTools => Set<WorkOrderOperationTool>();
        public DbSet<WorkOrderOperationPart> WorkOrderOperationParts => Set<WorkOrderOperationPart>();

        // Org Tree (platform schema)
        public DbSet<OrgNode> OrgNodes => Set<OrgNode>();

        // Customers & Customer Invoices
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
        public DbSet<CustomerInvoiceLine> CustomerInvoiceLines => Set<CustomerInvoiceLine>();

        public DbSet<MachineSpecification> MachineSpecifications { get; set; } = null!;

        // Sprint 12C / ADR-020 §D2 + ADR-021 — embedding storage + queue.
        public DbSet<Abs.FixedAssets.Models.Embeddings.Embedding> Embeddings => Set<Abs.FixedAssets.Models.Embeddings.Embedding>();
        public DbSet<Abs.FixedAssets.Models.Embeddings.PendingEmbedding> PendingEmbeddings => Set<Abs.FixedAssets.Models.Embeddings.PendingEmbedding>();

        // Sprint 12D / ADR-022 — chain-of-custody graph (virtual Apache AGE).
        public DbSet<Abs.FixedAssets.Models.ChainOfCustody.ChainNode> ChainNodes => Set<Abs.FixedAssets.Models.ChainOfCustody.ChainNode>();
        public DbSet<Abs.FixedAssets.Models.ChainOfCustody.ChainEdge> ChainEdges => Set<Abs.FixedAssets.Models.ChainOfCustody.ChainEdge>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Sprint 12C / ADR-020 §D2 — pgvector extension + Embeddings table.
            //
            // Provider-guarded: Embedding has a Pgvector.HalfVector property
            // that requires the Npgsql `UseVector()` extension to be mapped.
            // Tests (and any other consumer) using Sqlite / InMemory don't
            // load that mapping, so EF Core sees HalfVector as an undiscoverable
            // entity type and crashes model finalization.
            //
            // Solution: only register the Embedding model + extension when
            // the runtime provider is Npgsql. Non-Postgres contexts get
            // .Ignore<T>() instead — the tables simply don't exist for them,
            // which is fine because no test currently exercises Embeddings.
            if (Database.IsNpgsql())
            {
                modelBuilder.HasPostgresExtension("vector");

                modelBuilder.Entity<Abs.FixedAssets.Models.Embeddings.Embedding>(b =>
                {
                    b.ToTable("Embeddings");
                    b.HasKey(e => e.Id);
                    // One embedding per (entity, model). Lookup-by-entity is hot;
                    // index it. Composite uniqueness enforced via migration UNIQUE.
                    b.HasIndex(e => new { e.EntityType, e.EntityId, e.ModelVersion })
                     .IsUnique()
                     .HasDatabaseName("ix_embeddings_entity_model");
                    b.HasIndex(e => e.TenantId).HasDatabaseName("ix_embeddings_tenant");
                    // The HNSW index for halfvec_cosine_ops is in the migration
                    // (raw SQL) since EF doesn't model halfvec_cosine_ops natively.
                });

                modelBuilder.Entity<Abs.FixedAssets.Models.Embeddings.PendingEmbedding>(b =>
                {
                    b.ToTable("PendingEmbeddings");
                    b.HasKey(p => p.Id);
                    b.HasIndex(p => new { p.EntityType, p.EntityId, p.ContentHash })
                     .HasDatabaseName("ix_pending_embeddings_dedup");
                    b.HasIndex(p => p.EnqueuedAt).HasDatabaseName("ix_pending_embeddings_enqueued");
                    b.HasIndex(p => p.Attempts).HasDatabaseName("ix_pending_embeddings_attempts");
                });
            }
            else
            {
                // Non-Postgres provider (test contexts): skip the Embedding
                // entities entirely so EF doesn't try to materialize HalfVector
                // without the Pgvector plugin.
                modelBuilder.Ignore<Abs.FixedAssets.Models.Embeddings.Embedding>();
                modelBuilder.Ignore<Abs.FixedAssets.Models.Embeddings.PendingEmbedding>();
            }

            // Sprint 12D / ADR-022 — ChainNodes + ChainEdges (regular Postgres
            // tables, no extension dependency). Register on ALL providers
            // including EF InMemory + Sqlite test contexts, since these are
            // plain tables.
            modelBuilder.Entity<Abs.FixedAssets.Models.ChainOfCustody.ChainNode>(b =>
            {
                b.ToTable("ChainNodes");
                b.HasKey(n => n.Id);
                b.HasIndex(n => new { n.NodeType, n.EntityId, n.TenantId })
                 .IsUnique()
                 .HasDatabaseName("ix_chainnodes_entity");
                b.HasIndex(n => n.TenantId).HasDatabaseName("ix_chainnodes_tenant");
                // EF InMemory doesn't model jsonb — guard the column-type config
                // behind a provider check so test contexts skip it.
                if (Database.IsNpgsql())
                {
                    b.Property(n => n.Metadata).HasColumnType("jsonb");
                }
                else
                {
                    // EF InMemory can't materialize JsonDocument either — ignore
                    // the metadata column entirely in test contexts.
                    b.Ignore(n => n.Metadata);
                }
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.ChainOfCustody.ChainEdge>(b =>
            {
                b.ToTable("ChainEdges");
                b.HasKey(e => e.Id);
                b.HasOne(e => e.FromNode)
                 .WithMany()
                 .HasForeignKey(e => e.FromNodeId)
                 .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(e => e.ToNode)
                 .WithMany()
                 .HasForeignKey(e => e.ToNodeId)
                 .OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(e => new { e.FromNodeId, e.EdgeType }).HasDatabaseName("ix_chainedges_from");
                b.HasIndex(e => new { e.ToNodeId, e.EdgeType }).HasDatabaseName("ix_chainedges_to");
                if (Database.IsNpgsql())
                {
                    b.Property(e => e.Metadata).HasColumnType("jsonb");
                }
                else
                {
                    b.Ignore(e => e.Metadata);
                }
            });

            // Configure all DateTime properties to use UTC
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                                v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
                                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(
                            new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                                v => v.HasValue ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime()) : v,
                                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
                    }
                }
            }

            // Assets
            modelBuilder.Entity<Asset>(e =>
            {
                e.Property(a => a.AssetNumber).HasMaxLength(50).IsRequired();
                e.Property(a => a.Description).HasMaxLength(200).IsRequired();
                e.Property(a => a.Currency).HasMaxLength(3).HasDefaultValue("CAD").IsRequired();
                e.HasIndex(a => a.CompanyId);
                e.HasOne(a => a.CostCenterRef)
                    .WithMany(cc => cc.Assets)
                    .HasForeignKey(a => a.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.DepartmentRef)
                    .WithMany(d => d.Assets)
                    .HasForeignKey(a => a.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.LocationRef)
                    .WithMany(l => l.Assets)
                    .HasForeignKey(a => a.LocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.AssetCategory)
                    .WithMany(ac => ac.Assets)
                    .HasForeignKey(a => a.AssetCategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.AssetTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(a => a.AssetTypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.AssetPriorityLookupValue)
                    .WithMany()
                    .HasForeignKey(a => a.AssetPriorityLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.ConditionLookupValue)
                    .WithMany()
                    .HasForeignKey(a => a.ConditionLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.DepreciationMethodLookupValue)
                    .WithMany()
                    .HasForeignKey(a => a.DepreciationMethodLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(a => a.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(a => a.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(a => a.RowVersion);
            });

            // Books
            modelBuilder.Entity<Book>(e =>
            {
                e.Property(b => b.Code).HasMaxLength(20).IsRequired();
                e.Property(b => b.GlAccountAccumDep).HasMaxLength(50);
                e.Property(b => b.GlAccountDepExp).HasMaxLength(50);
                e.HasIndex(b => b.CompanyId);
                e.HasOne(b => b.BookTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(b => b.BookTypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(b => b.MethodLookupValue)
                    .WithMany()
                    .HasForeignKey(b => b.MethodLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(b => b.ConventionLookupValue)
                    .WithMany()
                    .HasForeignKey(b => b.ConventionLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(b => b.TaxJurisdictionLookupValue)
                    .WithMany()
                    .HasForeignKey(b => b.TaxJurisdictionLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(b => b.FrequencyLookupValue)
                    .WithMany()
                    .HasForeignKey(b => b.FrequencyLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // BookGlAccount
            modelBuilder.Entity<BookGlAccount>(e =>
            {
                e.HasIndex(x => x.BookId).IsUnique();
                e.Property(x => x.Asset).HasMaxLength(50);
                e.Property(x => x.AccumulatedDepreciation).HasMaxLength(50);
                e.Property(x => x.DepreciationExpense).HasMaxLength(50);
                e.Property(x => x.GainOnDisposal).HasMaxLength(50);
                e.Property(x => x.LossOnDisposal).HasMaxLength(50);
                e.Property(x => x.Clearing).HasMaxLength(50);
            });

            // ADR-003: per-tenant GL account configuration. One row per
            // (CompanyId, AccountKind) — enforced by unique index.
            modelBuilder.Entity<CompanyGlAccountConfig>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.AccountKind }).IsUnique()
                    .HasDatabaseName("UX_CompanyGlAccountConfigs_CompanyKind");
                e.Property(x => x.GlAccount).HasMaxLength(20).IsRequired();
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // JournalEntry
            modelBuilder.Entity<JournalEntry>(e =>
            {
                e.Property(x => x.Batch).HasMaxLength(30).IsRequired();
                e.Property(x => x.Reference).HasMaxLength(50);
                e.Property(x => x.Source).HasMaxLength(30);
                e.Property(x => x.Description).HasMaxLength(200);

                // Period is required (yyyymm); add an index for faster listing
                e.Property(x => x.Period).IsRequired();
                e.HasIndex(x => x.Period);

                // Optional FK to Book; restrict delete so entries don't vanish if a Book is removed
                e.HasOne(x => x.Book)
                    .WithMany()
                    .HasForeignKey(x => x.BookId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Optional index to speed up lookups by Batch
                e.HasIndex(x => x.Batch);
            });

            // JournalLine
            modelBuilder.Entity<JournalLine>(e =>
            {
                e.Property(x => x.Account).HasMaxLength(50).IsRequired();
                e.Property(x => x.Description).HasMaxLength(200);

                // If you'd rather use Fluent API instead of attributes for money precision:
                // e.Property(x => x.Debit).HasPrecision(18, 2);
                // e.Property(x => x.Credit).HasPrecision(18, 2);

                e.HasOne(x => x.JournalEntry)
                    .WithMany(j => j.Lines)
                    .HasForeignKey(x => x.JournalEntryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Helpful index on (JournalEntryId, LineNo)
                e.HasIndex(x => new { x.JournalEntryId, x.LineNo });

                // Sprint 13.5 PRA-5b — segment-keyed posting dimension.
                // RESTRICT delete so a referenced AccountingKey can't vanish
                // out from under historical JournalLines (the row is the
                // posting-time snapshot of all 8 segments — destroying it
                // would orphan reporting).
                e.HasOne(x => x.AccountingKey)
                    .WithMany()
                    .HasForeignKey(x => x.AccountingKeyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.AccountingKeyId);
            });

            // Sprint 13.5 PRA-5b — AccountingKey segment-key materialization.
            // Most schema lives in the migration (raw SQL pattern matching
            // PRA-7 through PRA-11). Fluent config here only wires the FK
            // navs + delete behaviors; constraints + partial UNIQUE indexes
            // are in 20260524250000_AddAccountingKeyPRA5b.cs.
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.AccountingKey>(e =>
            {
                e.Property(x => x.AccountingKeyHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.AccountingKeyString).HasMaxLength(256);

                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Account)
                    .WithMany()
                    .HasForeignKey(x => x.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Department)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CCA Classes
            modelBuilder.Entity<CcaClass>(e =>
            {
                e.HasIndex(x => x.ClassNumber).IsUnique();
                e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            });

            // Asset Tax Settings
            modelBuilder.Entity<AssetTaxSettings>(e =>
            {
                e.HasIndex(x => x.AssetId).IsUnique();
                e.HasOne(x => x.Asset)
                    .WithOne(a => a.TaxSettings)
                    .HasForeignKey<AssetTaxSettings>(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CcaClass)
                    .WithMany(c => c.AssetTaxSettings)
                    .HasForeignKey(x => x.CcaClassId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CCA Class Balances — scoped per Company so multiple Canadian
            // subsidiaries can each maintain their own UCC roll-forward
            // without collisions.
            modelBuilder.Entity<CcaClassBalance>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.CcaClassId, x.FiscalYear }).IsUnique();
                e.HasOne(x => x.CcaClass)
                    .WithMany(c => c.ClassBalances)
                    .HasForeignKey(x => x.CcaClassId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CCA Transactions
            modelBuilder.Entity<CcaTransaction>(e =>
            {
                e.HasIndex(x => new { x.CcaClassId, x.FiscalYear });
                e.HasOne(x => x.CcaClass)
                    .WithMany()
                    .HasForeignKey(x => x.CcaClassId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Asset Book Settings (multi-book support)
            modelBuilder.Entity<AssetBookSettings>(e =>
            {
                e.HasIndex(x => new { x.AssetId, x.BookId }).IsUnique();
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Book)
                    .WithMany(b => b.AssetBookSettings)
                    .HasForeignKey(x => x.BookId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Asset Transfers
            modelBuilder.Entity<AssetTransfer>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.TransferDate);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ReasonLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.ReasonLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Capital Improvements
            modelBuilder.Entity<CapitalImprovement>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.ImprovementDate);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Users
            modelBuilder.Entity<User>(e =>
            {
                e.HasIndex(x => x.Username).IsUnique();
                e.HasIndex(x => x.Email);
                e.HasOne(x => x.AssignedCompany)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedCompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.AssignedSite)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedSiteId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Audit Logs
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.HasIndex(x => x.EntityType);
                e.HasIndex(x => x.Timestamp);
                e.HasIndex(x => new { x.EntityType, x.EntityId });
            });

            // Period Locks
            modelBuilder.Entity<PeriodLock>(e =>
            {
                e.HasIndex(x => x.Period).IsUnique();
            });

            // Company
            modelBuilder.Entity<Company>(e =>
            {
                e.HasIndex(x => x.Name);
                e.HasIndex(x => x.CompanyCode);
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("USD");
                e.HasOne(x => x.ParentCompany)
                    .WithMany(p => p.ChildCompanies)
                    .HasForeignKey(x => x.ParentCompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Assets)
                    .WithOne(a => a.Company)
                    .HasForeignKey(a => a.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Books)
                    .WithOne(b => b.Company)
                    .HasForeignKey(b => b.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // US Tax Settings
            modelBuilder.Entity<UsTaxSettings>(e =>
            {
                e.HasIndex(x => x.AssetId).IsUnique();
                e.HasOne(x => x.Asset)
                    .WithOne(a => a.UsTaxSettings)
                    .HasForeignKey<UsTaxSettings>(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Section 179 Limits
            modelBuilder.Entity<Section179Limits>(e =>
            {
                e.HasIndex(x => x.TaxYear).IsUnique();
            });

            // Bonus Depreciation Rates
            modelBuilder.Entity<BonusDepreciationRates>(e =>
            {
                e.HasIndex(x => x.TaxYear).IsUnique();
            });

            // Asset Inventory
            modelBuilder.Entity<AssetInventory>(e =>
            {
                e.HasIndex(x => x.AssetId).IsUnique();
                e.HasIndex(x => x.BarcodeNumber);
                e.HasOne(x => x.Asset)
                    .WithOne(a => a.Inventory)
                    .HasForeignKey<AssetInventory>(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.LastInventoryList)
                    .WithMany()
                    .HasForeignKey(x => x.LastInventoryListId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Inventory Lists
            modelBuilder.Entity<InventoryList>(e =>
            {
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.CreatedDate);
            });

            // Inventory Scans
            modelBuilder.Entity<InventoryScan>(e =>
            {
                e.HasIndex(x => x.InventoryListId);
                e.HasIndex(x => x.ScanDate);
                e.HasOne(x => x.InventoryList)
                    .WithMany(l => l.Scans)
                    .HasForeignKey(x => x.InventoryListId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Maintenance Events
            modelBuilder.Entity<WorkOrder>(e =>
            {
                // PR #5c.2 — Direct CompanyId (defensive denormalization from
                // Asset.CompanyId). Indexed for fast tenant-scoped queries that
                // skip the Asset JOIN.
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.ScheduledDate);
                e.HasIndex(x => x.Status);

                // ADR-012 / PR #119.1 — top-level classification for the
                // unified work queue filter chips. Composite partial unique
                // index on (ExternalSource, ExternalWorkOrderId) is created
                // by the migration directly (EF can't express partial unique).
                e.HasIndex(x => x.Classification);

                // ADR-012 v0.2 / PR #119.6 — revision-chain self-FK.
                // (MasterWorkOrderId, Revision DESC) drives the "find all
                // revisions of WO X" lookup. SET NULL on master delete so
                // revisions become orphan masters rather than cascade-deleted.
                e.HasIndex(x => new { x.MasterWorkOrderId, x.Revision });
                e.HasOne(x => x.MasterWorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.MasterWorkOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Asset)
                    .WithMany(a => a.WorkOrders)
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.TypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.TypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PriorityLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.PriorityLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-012 v0.2 / PR #119.2 — WorkOrderFieldVisibility config table.
            // Indexes are created in raw SQL by the migration to use the
            // COALESCE(TenantId, 0) trick for NULL-safe uniqueness.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.WorkOrderFieldVisibility>(e =>
            {
                e.HasIndex(x => new { x.Classification, x.TenantId });
            });

            // ADR-012 v0.2 / PR #119.3 — Status engine config tables.
            // Migration creates the indexes via raw SQL; here we just
            // mirror the keys for EF model snapshot integrity.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusProfile>(e =>
            {
                e.HasKey(x => x.Classification);
            });
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusLabel>(e =>
            {
                e.HasIndex(x => new { x.Classification, x.StatusCode }).IsUnique();
                e.HasIndex(x => new { x.Classification, x.StatusKey }).IsUnique();
            });
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.WorkOrderStatusTransition>(e =>
            {
                e.HasIndex(x => new { x.Classification, x.FromStatusCode, x.ToStatusCode }).IsUnique();
                e.HasIndex(x => new { x.Classification, x.FromStatusCode });
            });

            // ADR-012 v0.2 / PR #119.4 — WorkOrderApproval table.
            // FK to User registered here; FK to WorkOrder + the
            // hot-path index are added by the migration directly.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.WorkOrderApproval>(e =>
            {
                e.HasIndex(x => x.WorkOrderId);
                e.HasIndex(x => new { x.WorkOrderId, x.Stage, x.Decision });
                e.HasOne(x => x.ApproverUser)
                    .WithMany()
                    .HasForeignKey(x => x.ApproverUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-012 v0.2 / PR #119.5 — NumberSequence table.
            // Unique index uses COALESCE(TenantId, 0) — created by the
            // migration via raw SQL.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.NumberSequence>(e =>
            {
                e.HasIndex(x => new { x.Classification, x.Year, x.TenantId });
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-012 v0.2 / PR #119.8 — CipWorkOrderDetails satellite.
            // 1:0..1 with WorkOrder, enforced by UNIQUE on WorkOrderId.
            // No nav property on the WorkOrder side yet; we read this
            // table by WorkOrderId from the renderer in Phase F.
            // Optional FK to Asset (TargetFixedAssetId) — SetNull because
            // a deleted asset shouldn't destroy CIP audit history.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.CipWorkOrderDetails>(e =>
            {
                e.HasIndex(x => x.WorkOrderId).IsUnique();
                e.HasIndex(x => x.AfeNumber);
                e.HasIndex(x => x.Stage);
                e.HasIndex(x => x.TargetFixedAssetId);
            });

            // ADR-012 v0.2 / PR #119.9 — QualityWorkOrderDetails satellite.
            // 1:0..1 with WorkOrder. Two self-links via WorkOrder FK:
            // CapaWorkOrderId (NCR -> CAPA forward), LinkedNcrId (CAPA -> NCR back).
            // Both nullable. EF doesn't enforce these FKs (we use raw SQL
            // in the migration to set ON DELETE SET NULL precisely).
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.QualityWorkOrderDetails>(e =>
            {
                e.HasIndex(x => x.WorkOrderId).IsUnique();
                e.HasIndex(x => x.NcrNumber);
                e.HasIndex(x => x.DispositionCode);
                e.HasIndex(x => x.QualityIssueType);
                e.HasIndex(x => x.CapaWorkOrderId);
                e.HasIndex(x => x.LinkedNcrId);
            });

            // ADR-012 v0.2 / PR #119.10 — EngineeringWorkOrderDetails satellite.
            // 1:0..1 with WorkOrder. LinkedNcrWorkOrderId is an optional
            // self-FK to the Quality NCR that triggered this engineering
            // CAPA, ON DELETE SET NULL in raw SQL.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.EngineeringWorkOrderDetails>(e =>
            {
                e.HasIndex(x => x.WorkOrderId).IsUnique();
                e.HasIndex(x => x.EcoNumber);
                e.HasIndex(x => x.EngineeringIssueType);
                e.HasIndex(x => x.LinkedNcrWorkOrderId);
            });

            // ADR-012 v0.2 / PR #119.11 — HseWorkOrderDetails satellite.
            // 1:0..1 with WorkOrder. RiskScore (1..25, computed from
            // ANSI Z10 5x5 matrix) is indexed so the EHS queue can sort
            // by risk descending.
            modelBuilder.Entity<Abs.FixedAssets.Models.WorkOrders.HseWorkOrderDetails>(e =>
            {
                e.HasIndex(x => x.WorkOrderId).IsUnique();
                e.HasIndex(x => x.OshaCaseNumber);
                e.HasIndex(x => x.HseIssueType);
                e.HasIndex(x => x.RecordabilityClass);
                e.HasIndex(x => x.RiskScore);
            });

            // ADR-013 / PR #119.12 — ProductionOrder header (sibling to WorkOrder).
            // Status, Type, ScheduledStart, ScheduledEnd all carry indexes
            // for queue/dashboard filtering. Revision-chain self-FK mirrors
            // WorkOrder.MasterWorkOrderId pattern (SET NULL on master delete).
            // PR #5c.2 — OrderNumber UNIQUE now tenant-prefixed (CompanyId, OrderNumber).
            // The migration drops the global UNIQUE and creates the composite via
            // raw SQL with a custom name; this HasIndex mirrors that so the EF
            // model stays consistent.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrder>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.OrderNumber })
                    .IsUnique()
                    .HasDatabaseName("IX_ProductionOrders_Company_OrderNumber");
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Type);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ScheduledStart);
                e.HasIndex(x => x.ScheduledEnd);
                e.HasIndex(x => new { x.MasterProductionOrderId, x.Revision });
                e.HasOne(x => x.MasterProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.MasterProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Sprint 12.8 PR #1 / ADR-028 — multi-level BOM parent-child FK.
                // SET NULL on parent delete preserves child cost history.
                // Indexed for "show me the children of order X" queries.
                e.HasIndex(x => x.ParentProductionOrderId)
                    .HasDatabaseName("IX_ProductionOrders_ParentProductionOrderId");
                e.HasOne(x => x.Parent)
                    .WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Sprint 12.8 PR #1 / ADR-028 — no-self-parent CHECK.
                // A ProductionOrder cannot reference itself as parent.
                // Enforced at the DB layer; future BOM-explosion service
                // would never violate this, but seeders + admin tooling
                // could without the constraint.
                e.ToTable(t => t.HasCheckConstraint(
                    "ck_productionorders_no_self_parent",
                    "\"ParentProductionOrderId\" IS NULL OR \"ParentProductionOrderId\" <> \"Id\""));

                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Location)
                    .WithMany()
                    .HasForeignKey(x => x.LocationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                // B8 PR-PO-1 (2026-05-27) — header field expansion per PO Cockpit spec §1.
                // New FKs: PlannerUserId + SupervisorUserId → Users (SET NULL on user delete).
                // New enum defaults per HARD LOCK (feedback_b6_enum_defaults_must_match_model.md):
                //   HoldReason is nullable (null = not on hold) — no HasDefaultValue needed.
                //   LotSerialRequirementType defaults to None (0) — explicit HasDefaultValue.
                // New indexes: PlannerUserId, SupervisorUserId, PromiseDate for cockpit queue filters.
                e.HasOne(x => x.Planner)
                    .WithMany()
                    .HasForeignKey(x => x.PlannerUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Supervisor)
                    .WithMany()
                    .HasForeignKey(x => x.SupervisorUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.PlannerUserId);
                e.HasIndex(x => x.SupervisorUserId);
                e.HasIndex(x => x.PromiseDate);
                e.HasIndex(x => x.HoldReason);
                e.Property(x => x.LotSerialRequirement)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.LotSerialRequirementType.None);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-013 / PR #119.12 — ProductionJobShopDetail satellite.
            // 1:0..1 with ProductionOrder via UNIQUE on ProductionOrderId.
            // PR #119.13a drops the CutListId placeholder column (cut-list
            // lookups go via CutListLine.SourceProductionOrderId) and FK-wires
            // NestPlanId -> Nests below.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionJobShopDetail>(e =>
            {
                e.HasIndex(x => x.ProductionOrderId).IsUnique();
                e.HasIndex(x => x.DrawingNumber);
                e.HasIndex(x => x.PriorityRank);
                e.HasIndex(x => x.HasOutsideOperations);
                e.HasOne(x => x.ProductionOrder)
                    .WithOne(x => x.JobShopDetail)
                    .HasForeignKey<Abs.FixedAssets.Models.Production.ProductionJobShopDetail>(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ADR-013 / PR #119.12 — WorkOrderOperation Vendor FK for outside-
            // processing (SAP PP02 pattern). Optional FK, SET NULL on vendor
            // delete so historical operations retain their record.
            // PR #119.13a extension: ProductionBatch FK + batch-pool tag for
            // shared-operation batching. ProductionBatch SET NULL on delete
            // so operation history survives even if a batch is cleaned up.
            modelBuilder.Entity<WorkOrderOperation>(e =>
            {
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProductionBatch)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.IsExternal);
                e.HasIndex(x => x.VendorId);
                e.HasIndex(x => x.ProductionBatchId);
                e.HasIndex(x => x.BatchPoolCode);
            });

            // ADR-013 / PR #119.13a — ProductionBatch polymorphic parent.
            // PR #5c.2 — BatchNumber UNIQUE is now tenant+site-prefixed
            // (CompanyId, LocationId, BatchNumber). The migration drops the global
            // BatchNumber UNIQUE (P0 cross-tenant leak) and creates the composite
            // via raw SQL; this HasIndex mirrors that so the EF model stays consistent.
            // PrimaryEquipment SET NULL on equipment delete (the batch record outlives
            // the machine). RecipeRevision SET NULL on delete (stub table; full
            // revisioning in PR #119.14). MrbDisposition SET NULL on delete.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionBatch>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.LocationId, x.BatchNumber })
                    .IsUnique()
                    .HasDatabaseName("IX_ProductionBatches_Company_Location_BatchNumber");
                e.HasIndex(x => new { x.CompanyId, x.LocationId })
                    .HasDatabaseName("IX_ProductionBatches_Company_Location");
                e.HasIndex(x => x.BatchType);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.BatchPoolCode);
                e.HasIndex(x => x.ScheduledStartAt);
                e.HasOne(x => x.PrimaryEquipment)
                    .WithMany()
                    .HasForeignKey(x => x.PrimaryEquipmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RecipeRevision)
                    .WithMany()
                    .HasForeignKey(x => x.RecipeRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.QuarantineDisposition)
                    .WithMany()
                    .HasForeignKey(x => x.QuarantineDispositionId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-013 / PR #119.13a — Nest subtype.
            // 1:0..1 with ProductionBatch via UNIQUE on ProductionBatchId,
            // ON DELETE CASCADE. StockItem RESTRICT — can't delete sheet SKU
            // that's referenced by a nest.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.Nest>(e =>
            {
                e.HasIndex(x => x.ProductionBatchId).IsUnique();
                e.HasIndex(x => x.StockItemId);
                e.HasOne(x => x.ProductionBatch)
                    .WithOne(x => x.Nest)
                    .HasForeignKey<Abs.FixedAssets.Models.Production.Nest>(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.StockItem)
                    .WithMany()
                    .HasForeignKey(x => x.StockItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ADR-013 / PR #119.13a — ProcessBatch subtype.
            // 1:0..1 with ProductionBatch via UNIQUE on ProductionBatchId,
            // ON DELETE CASCADE. ProcessType indexed for "all heat-treat
            // batches" / "all paint batches" queries.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProcessBatch>(e =>
            {
                e.HasIndex(x => x.ProductionBatchId).IsUnique();
                e.HasIndex(x => x.ProcessType);
                e.HasOne(x => x.ProductionBatch)
                    .WithOne(x => x.ProcessBatch)
                    .HasForeignKey<Abs.FixedAssets.Models.Production.ProcessBatch>(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ADR-013 / PR #119.13a — ProductionBatchAllocation.
            // UNIQUE on (ProductionBatchId, WorkOrderOperationId) — one
            // allocation per operation per batch. CASCADE from both parents.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionBatchAllocation>(e =>
            {
                e.HasIndex(x => new { x.ProductionBatchId, x.WorkOrderOperationId }).IsUnique();
                e.HasIndex(x => x.ProductionOrderId);
                e.HasOne(x => x.ProductionBatch)
                    .WithMany(x => x.Allocations)
                    .HasForeignKey(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.WorkOrderOperation)
                    .WithMany()
                    .HasForeignKey(x => x.WorkOrderOperationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ADR-013 / PR #119.13a — ProductionBatchEquipmentLink.
            // Multi-equipment child for plating lines + multi-zone furnaces.
            // CASCADE from ProductionBatch; RESTRICT on Equipment (audit trail).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionBatchEquipmentLink>(e =>
            {
                e.HasIndex(x => new { x.ProductionBatchId, x.SequenceNo });
                e.HasIndex(x => x.EquipmentId);
                e.HasOne(x => x.ProductionBatch)
                    .WithMany(x => x.EquipmentLinks)
                    .HasForeignKey(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Equipment)
                    .WithMany()
                    .HasForeignKey(x => x.EquipmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ADR-013 / PR #119.13a — ProductionBatchStateEvent audit log.
            // Append-only. Indexed on batch + change time for chronological
            // reads. CASCADE from ProductionBatch (rare — regulated workflows
            // don't delete batches).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionBatchStateEvent>(e =>
            {
                e.HasIndex(x => new { x.ProductionBatchId, x.ChangedAt });
                e.HasOne(x => x.ProductionBatch)
                    .WithMany(x => x.StateEvents)
                    .HasForeignKey(x => x.ProductionBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.MrbDisposition)
                    .WithMany()
                    .HasForeignKey(x => x.MrbDispositionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.13a — RecipeRevision stub.
            // Master-revision self-FK SET NULL on master delete (mirrors
            // WorkOrder.MasterWorkOrder pattern).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.RecipeRevision>(e =>
            {
                e.HasIndex(x => new { x.Name, x.Version }).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.MasterRecipe)
                    .WithMany()
                    .HasForeignKey(x => x.MasterRecipeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.13a — MrbDisposition stub.
            // DispositionNumber UNIQUE. Outcome indexed for "all open MRBs"
            // queries.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MrbDisposition>(e =>
            {
                e.HasIndex(x => x.DispositionNumber).IsUnique();
                e.HasIndex(x => x.Outcome);
            });

            // ADR-013 / PR #119.13b — MaterialMaster reference.
            // PR #5c.2 — Cross-tenant reference pattern: nullable CompanyId/LocationId.
            //   NULL CompanyId   = system reference (cross-tenant, shared)
            //   NOT NULL Company = tenant-specific extension
            // Two partial UNIQUEs replace the global ShopCode UNIQUE (P0 leak):
            //   IX_MaterialMasters_System_ShopCode   WHERE CompanyId IS NULL
            //   IX_MaterialMasters_Company_ShopCode  WHERE CompanyId IS NOT NULL
            // (Replit prod-validator gotcha from PR #5c.1.1 — partial indexes, no
            // COALESCE-in-index.) AstmDesignation indexed for cross-shop analytics joins.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MaterialMaster>(e =>
            {
                e.HasIndex(x => x.ShopCode)
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NULL")
                    .HasDatabaseName("IX_MaterialMasters_System_ShopCode");
                e.HasIndex(x => new { x.CompanyId, x.ShopCode })
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .HasDatabaseName("IX_MaterialMasters_Company_ShopCode");
                e.HasIndex(x => x.CompanyId)
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .HasDatabaseName("IX_MaterialMasters_Company");
                e.HasIndex(x => x.AstmDesignation);
                e.HasIndex(x => x.Form);
            });

            // ADR-013 / PR #119.13b — StockReceipt physical-lot record.
            // ReceiptNumber UNIQUE. ItemId RESTRICT (can't delete SKU with
            // receipts). MaterialMasterId / ReceivedByUserId / LocationId
            // SET NULL.
            //
            // ADR-015 Migration PR #3 (2026-05-19) DROPPED the HeatNumber
            // column. Heat # lookups now go through the GIN index on
            // Attributes (added by Migration PR #1) + the expression index
            // ((Attributes ->> 'heatNumber')) which Migration PR #1 added
            // for the STEEL promoted facet. Audit queries get equivalent
            // or better performance.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.StockReceipt>(e =>
            {
                e.HasIndex(x => x.ReceiptNumber).IsUnique();
                e.HasIndex(x => x.LotNumber);
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ReceivedAt);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.MaterialMaster)
                    .WithMany()
                    .HasForeignKey(x => x.MaterialMasterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ReceivedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ReceivedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Location)
                    .WithMany()
                    .HasForeignKey(x => x.LocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-013 / PR #119.13b — Remnant child of StockReceipt.
            // RemnantNumber UNIQUE. ParentReceipt RESTRICT (provenance).
            // ParentNest / ConsumedByNest SET NULL. HeatNumber indexed
            // for cross-stock audit ("find all stock from heat X" joins
            // both StockReceipts and Remnants).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.Remnant>(e =>
            {
                e.HasIndex(x => x.RemnantNumber).IsUnique();
                e.HasIndex(x => x.HeatNumber);
                e.HasIndex(x => x.ParentReceiptId);
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.ParentReceipt)
                    .WithMany()
                    .HasForeignKey(x => x.ParentReceiptId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ParentNest)
                    .WithMany()
                    .HasForeignKey(x => x.ParentNestId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ConsumedByNest)
                    .WithMany()
                    .HasForeignKey(x => x.ConsumedByNestId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.MaterialMaster)
                    .WithMany()
                    .HasForeignKey(x => x.MaterialMasterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Location)
                    .WithMany()
                    .HasForeignKey(x => x.LocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.13b — CutListLine to-cut queue.
            // Item RESTRICT (can't delete SKU with cut-list lines).
            // Nest / ProductionOrder / MaterialMaster SET NULL.
            // Composite (NestId, Status) index for "what's left to cut
            // in this nest" queries. (SourceProductionOrderId, Status)
            // for "cut list for this order".
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CutListLine>(e =>
            {
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => new { x.NestId, x.Status });
                e.HasIndex(x => new { x.SourceProductionOrderId, x.Status });
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.Priority);
                e.HasIndex(x => x.DueDate);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Nest)
                    .WithMany()
                    .HasForeignKey(x => x.NestId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.SourceProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SourceProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.MaterialMaster)
                    .WithMany()
                    .HasForeignKey(x => x.MaterialMasterId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.13b — Nest extension: StockReceiptId FK.
            // SET NULL on receipt delete (rare — physical sheets usually
            // transition to FullyConsumed or Scrapped via Status flag).
            // Wiring lives in a separate Entity<> call to keep the
            // per-PR change isolated; EF accumulates config.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.Nest>(e =>
            {
                e.HasIndex(x => x.StockReceiptId);
                e.HasOne(x => x.StockReceipt)
                    .WithMany()
                    .HasForeignKey(x => x.StockReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.14 — MaterialStructure polymorphic parent.
            // PR #5c.2 — Tenant scoping + site-or-template pattern (mirrors PR #5c.1
            // Routings):
            //   CompanyId NOT NULL — every BOM/Recipe belongs to one company.
            //   LocationId NULL    — site-scoped when set, company-wide engineering
            //                        template when NULL + IsSiteWideTemplate=TRUE.
            // Two partial UNIQUEs replace the global StructureNumber UNIQUE (P0 leak):
            //   IX_MaterialStructures_Site_StructureNumber_Rev      WHERE LocationId IS NOT NULL
            //   IX_MaterialStructures_Template_StructureNumber_Rev  WHERE LocationId IS NULL
            // (Replit prod-validator gotcha from PR #5c.1.1 — partial, no COALESCE.)
            // StructureType / Status indexed for queue/dashboard filtering.
            // Revision-chain self-FK mirrors RecipeRevision (SET NULL on master delete).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MaterialStructure>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.LocationId, x.StructureNumber, x.Revision })
                    .IsUnique()
                    .HasFilter("\"LocationId\" IS NOT NULL")
                    .HasDatabaseName("IX_MaterialStructures_Site_StructureNumber_Rev");
                e.HasIndex(x => new { x.CompanyId, x.StructureNumber, x.Revision })
                    .IsUnique()
                    .HasFilter("\"LocationId\" IS NULL")
                    .HasDatabaseName("IX_MaterialStructures_Template_StructureNumber_Rev");
                e.HasIndex(x => new { x.CompanyId, x.LocationId })
                    .HasDatabaseName("IX_MaterialStructures_Company_Location");
                e.HasIndex(x => x.StructureType);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.OutputItemId);
                e.HasOne(x => x.OutputItem)
                    .WithMany()
                    .HasForeignKey(x => x.OutputItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.MasterStructure)
                    .WithMany()
                    .HasForeignKey(x => x.MasterStructureId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RegulatoryProfile)
                    .WithMany()
                    .HasForeignKey(x => x.RegulatoryProfileId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ADR-013 / PR #119.14 — Bom subtype.
            // 1:0..1 with MaterialStructure via UNIQUE on MaterialStructureId,
            // ON DELETE CASCADE.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.Bom>(e =>
            {
                e.HasIndex(x => x.MaterialStructureId).IsUnique();
                e.HasIndex(x => x.BomType);
                e.HasOne(x => x.MaterialStructure)
                    .WithOne(x => x.Bom)
                    .HasForeignKey<Abs.FixedAssets.Models.Production.Bom>(x => x.MaterialStructureId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ADR-013 / PR #119.14 — Recipe subtype.
            // 1:0..1 with MaterialStructure. RecipeRevision SET NULL on delete
            // (links into the stub from #119.13a). IntermediateItem RESTRICT.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.Recipe>(e =>
            {
                e.HasIndex(x => x.MaterialStructureId).IsUnique();
                e.HasIndex(x => x.RecipeRevisionId);
                e.HasOne(x => x.MaterialStructure)
                    .WithOne(x => x.Recipe)
                    .HasForeignKey<Abs.FixedAssets.Models.Production.Recipe>(x => x.MaterialStructureId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.RecipeRevision)
                    .WithMany()
                    .HasForeignKey(x => x.RecipeRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.IntermediateItem)
                    .WithMany()
                    .HasForeignKey(x => x.IntermediateItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ADR-013 / PR #119.14 — MaterialStructureLine.
            // Shared by Bom and Recipe subtypes. CASCADE from parent.
            // Item RESTRICT (can't delete SKU with active structure lines).
            // (MaterialStructureId, Sequence) UNIQUE for deterministic ordering.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MaterialStructureLine>(e =>
            {
                e.HasIndex(x => new { x.MaterialStructureId, x.Sequence }).IsUnique();
                e.HasIndex(x => x.LineKind);
                e.HasIndex(x => x.ItemId);
                e.HasOne(x => x.MaterialStructure)
                    .WithMany(x => x.Lines)
                    .HasForeignKey(x => x.MaterialStructureId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ADR-013 / PR #119.14 — RecipePhase.
            // 1:N with Recipe via CASCADE. (RecipeId, Sequence) UNIQUE.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.RecipePhase>(e =>
            {
                e.HasIndex(x => new { x.RecipeId, x.Sequence }).IsUnique();
                e.HasOne(x => x.Recipe)
                    .WithMany(x => x.Phases)
                    .HasForeignKey(x => x.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ADR-013 / PR #119.14 — RegulatoryProfile config.
            // Name UNIQUE. Regime indexed for "find all FDA profiles" queries.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.RegulatoryProfile>(e =>
            {
                e.HasIndex(x => x.Name).IsUnique();
                e.HasIndex(x => x.Regime);
                e.HasIndex(x => x.IsActive);
            });

            // ADR-014 / Sprint 4 PR #1 — VoiceSession. Per-tenant + per-user
            // indexes for "find this user's recent sessions" queries.
            modelBuilder.Entity<Abs.FixedAssets.Models.Infrastructure.VoiceSession>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TenantId, x.UserId, x.LastTurnAt });
                e.HasIndex(x => x.ExpiresAt);
            });

            // ADR-014 / Sprint 4 PR #1 — IdempotencyKey. Composite PK on
            // (UserId, Key). ExpiresAt indexed for the future TTL-sweep job.
            modelBuilder.Entity<Abs.FixedAssets.Models.Infrastructure.IdempotencyKey>(e =>
            {
                e.HasKey(x => new { x.UserId, x.Key });
                e.HasIndex(x => x.ExpiresAt);
            });

            // ADR-013 / PR #119.14 — wire ProductionOrder.MaterialStructureId
            // FK to MaterialStructures. SET NULL on structure delete so the
            // order's history (status, schedule, allocations) survives even
            // if the structure is administratively retired.
            //
            // Sprint 14.1 PR-1 — wire ProductionOrder.SourceItemRevisionId FK to
            // ItemRevisions. SET NULL on revision delete so the snapshot
            // survives revision archival. Plus the 1:N nav to the frozen BOM
            // snapshot (ProductionMaterialStructures).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrder>(e =>
            {
                e.HasIndex(x => x.MaterialStructureId);
                e.HasOne(x => x.MaterialStructure)
                    .WithMany()
                    .HasForeignKey(x => x.MaterialStructureId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Sprint 14.1 PR-1 — frozen source revision FK.
                e.HasIndex(x => x.SourceItemRevisionId)
                    .HasDatabaseName("IX_ProductionOrders_SourceItemRevisionId");
                e.HasOne(x => x.SourceItemRevision)
                    .WithMany()
                    .HasForeignKey(x => x.SourceItemRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Sprint 14.1 PR-1 — partial index on captured snapshots so the
                // probe + cost engine + MES can quickly find PROs that have
                // been snapshotted vs those that haven't.
                e.HasIndex(x => x.SnapshotCapturedAtUtc)
                    .HasFilter("\"SnapshotCapturedAtUtc\" IS NOT NULL")
                    .HasDatabaseName("IX_ProductionOrders_SnapshotCaptured_Partial");

                // ----- Theme B7 Wave A PR-2 — master-optional (PoFirst) identity -----
                // IsPoFirst: bool default false == CLR sentinel — HasDefaultValue
                // is safe (enum-defaults HARD LOCK). Partial index for the
                // "find master-less orders" buyer/planner query.
                e.Property(x => x.IsPoFirst).HasDefaultValue(false);
                e.HasIndex(x => x.IsPoFirst)
                    .HasFilter("\"IsPoFirst\" = TRUE")
                    .HasDatabaseName("IX_ProductionOrders_IsPoFirst_Partial");

                // CrystallizedItemId — second FK to Items (distinct from ItemId).
                // SET NULL on item delete so order history survives master
                // archival. ItemId is configured Restrict in the first
                // ProductionOrder config block (HasOne(x => x.Item)); EF merges
                // the blocks, so this FK needs its own WithMany() to
                // disambiguate the two Item relationships.
                e.HasOne(x => x.CrystallizedItem)
                    .WithMany()
                    .HasForeignKey(x => x.CrystallizedItemId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.CrystallizedItemId)
                    .HasFilter("\"CrystallizedItemId\" IS NOT NULL")
                    .HasDatabaseName("IX_ProductionOrders_CrystallizedItemId_Partial");
            });

            // Sprint 14.1 PR-1 (2026-05-26) — ProductionMaterialStructure.
            // Per-PO frozen BOM snapshot. Tenant trio + RowVersion + enum
            // HasDefaultValue (BomIssueMethod.Pull) all baked in from day one
            // per PR-FS-2/4/5/7 lessons (especially the PR #363 enum-default
            // P1 Codex catch). Service-side NULL-safe uniqueness on
            // (ProductionOrderId, Sequence) — partial UNIQUE complemented by
            // service-layer pre-check.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionMaterialStructure>(e =>
            {
                // Enum DB default — Pull is the industry-default issue method
                // (just-in-time per-operation pull). HARD LOCK per
                // feedback_b6_enum_defaults_must_match_model.md: every new
                // enum column MUST have HasDefaultValue matching the model
                // default so legacy backfill + raw-SQL inserts land on
                // semantic-default Pull (not Push at index 1).
                e.Property(x => x.IssueMethod)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.BomIssueMethod.Pull);

                // LineKind also gets explicit HasDefaultValue — Component is
                // the model default (and what the resolver hits 95% of the
                // time). Without this, a raw-SQL insert with omitted LineKind
                // would also map to 0 — but Component IS 0, so this is
                // belt-and-suspenders. Wire it anyway for posterity per the
                // hard-lock.
                e.Property(x => x.LineKind)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.LineKind.Component);

                // PR-14.1-1.1 hotfix: concurrency via Postgres xmin (project
                // convention used by 17 other entities — see
                // Data/XminRowVersionExtensions.cs). The original
                // `IsRowVersion()` generated a real `bytea NOT NULL` column
                // that EF excluded from INSERT (rowVersion annotation =
                // value-generated-on-add) but Postgres doesn't auto-populate
                // bytea, so every INSERT threw 23502 NOT NULL violation. The
                // xmin pattern avoids the bytea column entirely.
                e.MapXminRowVersion(x => x.RowVersion);

                // Deterministic ordering per PO — UNIQUE on (PO, Sequence).
                e.HasIndex(x => new { x.ProductionOrderId, x.Sequence })
                    .IsUnique()
                    .HasDatabaseName("UX_ProdMatStruct_PO_Sequence");

                // Read-path indexes.
                e.HasIndex(x => x.ProductionOrderId)
                    .HasDatabaseName("IX_ProdMatStruct_PO");
                e.HasIndex(x => x.ChildItemId)
                    .HasDatabaseName("IX_ProdMatStruct_ChildItem");
                e.HasIndex(x => x.CompanyId)
                    .HasDatabaseName("IX_ProdMatStruct_Company");
                e.HasIndex(x => x.SourceMaterialStructureLineId);
                e.HasIndex(x => x.SourceMaterialStructureId);
                e.HasIndex(x => x.ChildItemRevisionId);

                // FK config — CASCADE on PO delete (snapshot doesn't outlive
                // its order); RESTRICT on ChildItem (orphaning a snapshot
                // would erase the component audit trail); SET NULL on source
                // structure / line / revision (snapshot survives engineering
                // archival).
                e.HasOne(x => x.ProductionOrder)
                    .WithMany(p => p.MaterialSnapshot)
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ChildItem)
                    .WithMany()
                    .HasForeignKey(x => x.ChildItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SourceMaterialStructureLine)
                    .WithMany()
                    .HasForeignKey(x => x.SourceMaterialStructureLineId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.SourceMaterialStructure)
                    .WithMany()
                    .HasForeignKey(x => x.SourceMaterialStructureId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.ChildItemRevision)
                    .WithMany()
                    .HasForeignKey(x => x.ChildItemRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // B8 PR-PRO-2 — execution-side enum defaults + indexes + self-FK.
                e.Property(x => x.LineStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.BomLineStatus.NotRequiredYet);
                e.Property(x => x.SupplyType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SupplyType.Pull);
                e.Property(x => x.IssueTiming)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.IssueTiming.AtOperationStart);
                e.Property(x => x.CostBucket)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostBucket.Material);
                e.HasIndex(x => x.LineStatus).HasDatabaseName("IX_ProdMatStruct_LineStatus");
                e.HasIndex(x => x.ConsumingOperationSequence).HasDatabaseName("IX_ProdMatStruct_ConsumingOpSeq");
                e.HasOne(x => x.AlternateBomLine).WithMany()
                    .HasForeignKey(x => x.AlternateBomLineId).OnDelete(DeleteBehavior.SetNull);

                // B8 PR-PRO-7 — Material Supply Link enum defaults + indexes.
                // HARD LOCK feedback_b6_enum_defaults_must_match_model.md: every
                // enum column MUST have HasDefaultValue matching the model default
                // BEFORE migration generation.
                e.Property(x => x.MaterialSupplyType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.MaterialSupplyType.PurchaseToJob);
                e.Property(x => x.MaterialSupplyStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.MaterialSupplyStatus.Available);
                e.Property(x => x.LinkedSupplyRecordType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.LinkedSupplyRecordType.None);
                e.Property(x => x.SupplyRisk)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SupplyRisk.None);

                // Supply link read-path indexes.
                e.HasIndex(x => x.MaterialSupplyStatus)
                    .HasDatabaseName("IX_ProdMatStruct_SupplyStatus");
                e.HasIndex(x => x.SupplyRisk)
                    .HasDatabaseName("IX_ProdMatStruct_SupplyRisk");
                e.HasIndex(x => new { x.LinkedSupplyRecordType, x.LinkedSupplyRecordId })
                    .HasDatabaseName("IX_ProdMatStruct_LinkedSupply");
                e.HasIndex(x => x.SupplyRequiredDate)
                    .HasDatabaseName("IX_ProdMatStruct_SupplyRequiredDate");
            });

            // Theme B7 Wave B PR-4 (2026-05-29) — ItemCrystallization.
            // The crystallization-at-ship audit record. Tenant trio + xmin +
            // enum HasDefaultValue (value-0 sentinels) + two FKs to Item +
            // RESTRICT on the source PRO + UNIQUE crystallization number, all
            // baked in from day one per the B6/B7 hard-locks.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ItemCrystallization>(e =>
            {
                // Enum DB defaults — value-0 semantic sentinels, HasDefaultValue
                // safe (HARD LOCK feedback_b6_enum_defaults_must_match_model.md).
                // Outcome default Pending (a stub being prepared); CostSource
                // default FirstActual (the §5.4 rule).
                e.Property(x => x.Outcome)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CrystallizationOutcome.Pending);
                e.Property(x => x.CostSource)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CrystallizationCostSource.FirstActual);

                // xmin concurrency (HARD LOCK feedback_xmin_pattern_for_concurrency_lock.md).
                e.MapXminRowVersion(x => x.RowVersion);

                // UNIQUE crystallization number per tenant. The two-phase
                // placeholder (CRYST-PEND-{guid}) is globally unique, so this
                // never collides during the insert→patch window.
                e.HasIndex(x => new { x.CompanyId, x.CrystallizationNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_ItemCrystallization_Company_Number");

                // Read-path indexes.
                e.HasIndex(x => x.SourceProductionOrderId)
                    .HasDatabaseName("IX_ItemCrystallization_SourcePRO");
                e.HasIndex(x => x.CompanyId)
                    .HasDatabaseName("IX_ItemCrystallization_Company");
                e.HasIndex(x => x.CreatedItemId)
                    .HasFilter("\"CreatedItemId\" IS NOT NULL")
                    .HasDatabaseName("IX_ItemCrystallization_CreatedItem_Partial");
                e.HasIndex(x => x.StructureFingerprintHash)
                    .HasFilter("\"StructureFingerprintHash\" IS NOT NULL")
                    .HasDatabaseName("IX_ItemCrystallization_Fingerprint_Partial");

                // FK config — RESTRICT on the source PRO (a crystallized order
                // can't be deleted out from under its audit trail); SET NULL on
                // both Item FKs (the audit record survives master archival).
                // Two distinct Item relationships need separate WithMany() to
                // disambiguate.
                e.HasOne(x => x.SourceProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SourceProductionOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedItem)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedItemId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.MatchedItem)
                    .WithMany()
                    .HasForeignKey(x => x.MatchedItemId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.2 PR-1 (2026-05-26 evening) — DMS substrate.
            //
            // Document = controlled artifact header. Tenant trio + xmin
            // concurrency + UNIQUE on (CompanyId, DocumentNumber) via
            // partial UNIQUE (null-safe for TenantId per PR-FS-2 lesson).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.Document>(e =>
            {
                // Enum DB defaults (HARD LOCK feedback_b6_enum_defaults_must_match_model.md).
                e.Property(x => x.DocumentType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.DocumentType.Drawing);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.DocumentStatus.Draft);

                // xmin concurrency (HARD LOCK feedback_xmin_pattern_for_concurrency_lock.md).
                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.CompanyId, x.DocumentNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_Documents_Company_DocNumber");

                e.HasIndex(x => x.DocumentType)
                    .HasDatabaseName("IX_Documents_Type");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("IX_Documents_Status");
                e.HasIndex(x => x.CompanyId);
            });

            // DocumentVersion = per-revision satellite. Auto-incremented
            // VersionNumber per Document (service-layer); UNIQUE on
            // (DocumentId, VersionNumber) and (DocumentId, RevisionCode).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.DocumentVersion>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.DocumentStatus.Draft);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.DocumentId, x.VersionNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_DocVersions_Doc_VersionNumber");
                e.HasIndex(x => new { x.DocumentId, x.RevisionCode })
                    .IsUnique()
                    .HasDatabaseName("UX_DocVersions_Doc_RevisionCode");

                e.HasIndex(x => x.DocumentId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("IX_DocVersions_Status");
                e.HasIndex(x => x.SupersedesVersionId);
                e.HasIndex(x => x.ContentHash);
                e.HasIndex(x => x.SourceEcoNumber);

                e.HasOne(x => x.Document)
                    .WithMany(d => d.Versions)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.SupersedesVersion)
                    .WithMany()
                    .HasForeignKey(x => x.SupersedesVersionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ItemDocumentLink = M:N Items↔Documents with link purpose +
            // primary flag. Idempotent per (Item, Document, Purpose) via
            // UNIQUE; service-layer LinkToItem short-circuits if existing.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.ItemDocumentLink>(e =>
            {
                e.Property(x => x.LinkPurpose)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ItemDocumentLinkPurpose.BillOfDrawing);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.ItemId, x.DocumentId, x.LinkPurpose })
                    .IsUnique()
                    .HasDatabaseName("UX_ItemDocLinks_Item_Doc_Purpose");

                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.DocumentId);
                e.HasIndex(x => x.CompanyId);

                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Document)
                    .WithMany(d => d.ItemLinks)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Sprint 14.3 PR-1 (2026-05-27) — ECR/ECO Change Control.
            //
            // EngineeringChangeRequest — the controlled intake. Tenant trio +
            // xmin + enum HasDefaultValue + UNIQUE (CompanyId, EcrNumber) +
            // FKs to affected things (Item / Document / PRO / Customer / ECO).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.EngineeringChangeRequest>(e =>
            {
                e.Property(x => x.ChangeReason)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ChangeReason.Other);
                e.Property(x => x.Urgency)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ChangeUrgency.Routine);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.EcrStatus.Draft);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.CompanyId, x.EcrNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_Ecr_Company_Number");

                e.HasIndex(x => x.Status).HasDatabaseName("IX_Ecr_Status");
                e.HasIndex(x => x.Urgency).HasDatabaseName("IX_Ecr_Urgency");
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.LinkedItemId);
                e.HasIndex(x => x.LinkedDocumentId);
                e.HasIndex(x => x.LinkedProductionOrderId);
                e.HasIndex(x => x.LinkedCustomerId);
                e.HasIndex(x => x.ResultingEcoId);

                e.HasOne(x => x.LinkedItem)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedItemId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedDocument)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedDocumentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedCustomer)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedCustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                // ResultingEco FK: SET NULL — ECR row survives ECO delete
                // (rare path; closure of the chain requires both sides
                // normally, but admin cleanup is allowed).
                e.HasOne(x => x.ResultingEco)
                    .WithMany()
                    .HasForeignKey(x => x.ResultingEcoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // EngineeringChangeOrder — execution record from approved ECR.
            // RESTRICT on SourceEcrId — an ECO can't outlive the ECR that
            // created it. Multi-stage approval + effectivity rules wire
            // through navigation properties.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.EngineeringChangeOrder>(e =>
            {
                e.Property(x => x.Urgency)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ChangeUrgency.Routine);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.EcoStatus.Draft);
                e.Property(x => x.EffectivityType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.EcoEffectivityType.Immediate);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.CompanyId, x.EcoNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_Eco_Company_Number");

                e.HasIndex(x => x.Status).HasDatabaseName("IX_Eco_Status");
                e.HasIndex(x => x.Urgency).HasDatabaseName("IX_Eco_Urgency");
                e.HasIndex(x => x.EffectivityType).HasDatabaseName("IX_Eco_EffType");
                e.HasIndex(x => x.SourceEcrId);
                e.HasIndex(x => x.EffectivityProductionOrderId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.EffectiveFromUtc)
                    .HasFilter("\"EffectiveFromUtc\" IS NOT NULL")
                    .HasDatabaseName("IX_Eco_EffectiveFrom_Partial");

                e.HasOne(x => x.SourceEcr)
                    .WithMany()
                    .HasForeignKey(x => x.SourceEcrId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.EffectivityProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.EffectivityProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // EcoLineItem — one affected thing per ECO row. CASCADE on ECO
            // delete (rare admin path). RESTRICT on Item/Document/Version
            // — line preserves the change-record audit trail.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.EcoLineItem>(e =>
            {
                e.Property(x => x.Disposition)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.EcoLineItemDisposition.NotApplicable);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.EcoId, x.Sequence })
                    .IsUnique()
                    .HasDatabaseName("UX_EcoLineItem_Eco_Sequence");

                e.HasIndex(x => x.EcoId);
                e.HasIndex(x => x.AffectedItemId);
                e.HasIndex(x => x.AffectedDocumentId);
                e.HasIndex(x => x.AffectedDocumentVersionId);
                e.HasIndex(x => x.NewDocumentVersionId);
                e.HasIndex(x => x.CompanyId);

                e.HasOne(x => x.Eco)
                    .WithMany(eco => eco.LineItems)
                    .HasForeignKey(x => x.EcoId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.AffectedItem)
                    .WithMany()
                    .HasForeignKey(x => x.AffectedItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AffectedDocument)
                    .WithMany()
                    .HasForeignKey(x => x.AffectedDocumentId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AffectedDocumentVersion)
                    .WithMany()
                    .HasForeignKey(x => x.AffectedDocumentVersionId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.NewDocumentVersion)
                    .WithMany()
                    .HasForeignKey(x => x.NewDocumentVersionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // EcoApproval — one stage in the multi-stage approval chain.
            // UNIQUE per (EcoId, StageOrder). Service enforces in-order
            // approval (earlier stages first).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.EcoApproval>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.EcoApprovalStatus.Pending);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.EcoId, x.StageOrder })
                    .IsUnique()
                    .HasDatabaseName("UX_EcoApproval_Eco_Stage");

                e.HasIndex(x => x.EcoId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_EcoApproval_Status");
                e.HasIndex(x => x.CompanyId);

                e.HasOne(x => x.Eco)
                    .WithMany(eco => eco.Approvals)
                    .HasForeignKey(x => x.EcoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Sprint 14.3 PR-2 (2026-05-27) — Deviation (short-term engineering
            // exception). Tenant trio + xmin + enum defaults per HARD LOCKS.
            // Unique on (CompanyId, DeviationNumber). FKs: Item (Restrict),
            // ProductionOrder (SetNull), OriginatingEcr (SetNull).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.Deviation>(e =>
            {
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.DeviationType.Material);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.DeviationStatus.Draft);

                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.CompanyId, x.DeviationNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_Deviation_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_Deviation_Status");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.ProductionOrderId);
                e.HasIndex(x => x.ExpirationDateUtc).HasDatabaseName("IX_Deviation_Expiry");

                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingEcr)
                    .WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-3 — Waiver (customer-approved longer-term divergence).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.Waiver>(e =>
            {
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.WaiverType.Material);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.WaiverStatus.Draft);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.WaiverNumber })
                    .IsUnique().HasDatabaseName("UX_Waiver_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_Waiver_Status");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.CustomerId);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingEcr).WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RelatedDeviation).WithMany()
                    .HasForeignKey(x => x.RelatedDeviationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer).WithMany()
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-4 — Concession entity config.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.Concession>(e =>
            {
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ConcessionType.Material);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ConcessionStatus.Draft);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ConcessionNumber })
                    .IsUnique().HasDatabaseName("UX_Concession_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_Concession_Status");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.CustomerId);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingEcr).WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RelatedDeviation).WithMany()
                    .HasForeignKey(x => x.RelatedDeviationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer).WithMany()
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-5 — CustomerNotice entity config.
            // Outbound notification to customers for engineering changes.
            // Unique on (CompanyId, NoticeNumber). FKs: Customer (SetNull), Item (Restrict),
            // OriginatingEcr (SetNull), OriginatingDeviation (SetNull),
            // OriginatingWaiver (SetNull), OriginatingConcession (SetNull).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.CustomerNotice>(e =>
            {
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.CustomerNoticeType.EngineeringChange);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.CustomerNoticeStatus.Draft);
                e.Property(x => x.DeliveryMethod)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.NotificationDeliveryMethod.Email);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.NoticeNumber })
                    .IsUnique().HasDatabaseName("UX_CustomerNotice_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_CustomerNotice_Status");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.CustomerId);
                e.HasOne(x => x.Customer).WithMany()
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.OriginatingEcr).WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingDeviation).WithMany()
                    .HasForeignKey(x => x.OriginatingDeviationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingWaiver).WithMany()
                    .HasForeignKey(x => x.OriginatingWaiverId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingConcession).WithMany()
                    .HasForeignKey(x => x.OriginatingConcessionId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-5 — SupplierProcessChangeNotification entity config.
            // Outbound PCN to suppliers for process/material/tooling changes.
            // Unique on (CompanyId, PcnNumber). FKs: Vendor (SetNull), Item (Restrict),
            // OriginatingEcr (SetNull).
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.SupplierProcessChangeNotification>(e =>
            {
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.PcnType.ProcessChange);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.PcnStatus.Draft);
                e.Property(x => x.DeliveryMethod)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.NotificationDeliveryMethod.Email);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.PcnNumber })
                    .IsUnique().HasDatabaseName("UX_SupplierPcn_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_SupplierPcn_Status");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.VendorId);
                e.HasOne(x => x.Vendor).WithMany()
                    .HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.OriginatingEcr).WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-3 — ProductionMaterialTransaction entity config.
            // Material movement log for every Issue/Return/Transfer/Scrap/Substitute
            // action against frozen BOM lines. 12 transaction types.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionMaterialTransaction>(e =>
            {
                e.Property(x => x.TransactionType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.MaterialTransactionType.Issue);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.MaterialTransactionStatus.Posted);
                e.Property(x => x.CostBucket)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostBucket.Material);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.TransactionNumber })
                    .IsUnique().HasDatabaseName("UX_MatTxn_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_MatTxn_PRO");
                e.HasIndex(x => x.BomLineId).HasDatabaseName("IX_MatTxn_BomLine");
                e.HasIndex(x => x.ItemId).HasDatabaseName("IX_MatTxn_Item");
                e.HasIndex(x => x.TransactionType).HasDatabaseName("IX_MatTxn_Type");
                e.HasIndex(x => x.TransactionDateUtc).HasDatabaseName("IX_MatTxn_Date");
                e.HasIndex(x => x.TransferPairId).HasDatabaseName("IX_MatTxn_TransferPair");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.BomLine).WithMany()
                    .HasForeignKey(x => x.BomLineId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.OriginalTransaction).WithMany()
                    .HasForeignKey(x => x.OriginalTransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.TransferProductionOrder).WithMany()
                    .HasForeignKey(x => x.TransferProductionOrderId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.TransferBomLine).WithMany()
                    .HasForeignKey(x => x.TransferBomLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginalItem).WithMany()
                    .HasForeignKey(x => x.OriginalItemId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-6 — CorrectiveActionRequest entity config.
            // CAR/CAPA 8D lifecycle. AS9100 §10.2.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.CorrectiveActionRequest>(e =>
            {
                e.Property(x => x.Source)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.CarSource.InternalAudit);
                e.Property(x => x.Severity)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.CarSeverity.Minor);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.CarStatus.Draft);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.CarNumber })
                    .IsUnique().HasDatabaseName("UX_Car_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_Car_Status");
                e.HasIndex(x => x.Severity).HasDatabaseName("IX_Car_Severity");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.CustomerId);
                e.HasIndex(x => x.VendorId);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer).WithMany()
                    .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Vendor).WithMany()
                    .HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginatingEcr).WithMany()
                    .HasForeignKey(x => x.OriginatingEcrId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RelatedDeviation).WithMany()
                    .HasForeignKey(x => x.RelatedDeviationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.RelatedConcession).WithMany()
                    .HasForeignKey(x => x.RelatedConcessionId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-6 — ProductionCompletionEvent entity config.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionCompletionEvent>(e =>
            {
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.CompletionNumber })
                    .IsUnique().HasDatabaseName("UX_CmpEvt_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_CmpEvt_PRO");
                e.HasIndex(x => x.OperationId).HasDatabaseName("IX_CmpEvt_Op");
                e.HasIndex(x => x.CompletedAtUtc).HasDatabaseName("IX_CmpEvt_Date");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Operation).WithMany()
                    .HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Transaction).WithMany()
                    .HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WipMove).WithMany()
                    .HasForeignKey(x => x.WipMoveId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-6 — ProductionScrapEvent entity config.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionScrapEvent>(e =>
            {
                e.Property(x => x.ResponsibleArea)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ScrapResponsibleArea.Machine);
                e.Property(x => x.Disposition)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ScrapDisposition.Scrap);
                e.Property(x => x.CostTreatment)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostTreatment.AbsorbToJob);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ScrapNumber })
                    .IsUnique().HasDatabaseName("UX_ScpEvt_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ScpEvt_PRO");
                e.HasIndex(x => x.DetectedAtOperationId).HasDatabaseName("IX_ScpEvt_DetOp");
                e.HasIndex(x => x.Disposition).HasDatabaseName("IX_ScpEvt_Disp");
                e.HasIndex(x => x.ScrapRecordedAtUtc).HasDatabaseName("IX_ScpEvt_Date");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.DetectedAtOperation).WithMany()
                    .HasForeignKey(x => x.DetectedAtOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CausedAtOperation).WithMany()
                    .HasForeignKey(x => x.CausedAtOperationId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-6 — ProductionReworkEvent entity config.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionReworkEvent>(e =>
            {
                e.Property(x => x.RoutingType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ReworkRoutingType.ReturnToExistingOp);
                e.Property(x => x.CostTreatment)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostTreatment.AbsorbToJob);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ReworkNumber })
                    .IsUnique().HasDatabaseName("UX_RwkEvt_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_RwkEvt_PRO");
                e.HasIndex(x => x.SourceOperationId).HasDatabaseName("IX_RwkEvt_SrcOp");
                e.HasIndex(x => x.ReworkDecisionAtUtc).HasDatabaseName("IX_RwkEvt_Date");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SourceOperation).WithMany()
                    .HasForeignKey(x => x.SourceOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ReworkOperation).WithMany()
                    .HasForeignKey(x => x.ReworkOperationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WipMove).WithMany()
                    .HasForeignKey(x => x.WipMoveId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-5 — ProductionWipMove entity config.
            // Auto-advance on completion + manual moves. Full audit trail.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionWipMove>(e =>
            {
                e.Property(x => x.MoveType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.WipMoveType.AutoAdvance);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.WipMoveStatus.Completed);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.MoveNumber })
                    .IsUnique().HasDatabaseName("UX_WipMove_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_WipMove_PRO");
                e.HasIndex(x => x.FromOperationId).HasDatabaseName("IX_WipMove_FromOp");
                e.HasIndex(x => x.ToOperationId).HasDatabaseName("IX_WipMove_ToOp");
                e.HasIndex(x => x.MoveType).HasDatabaseName("IX_WipMove_Type");
                e.HasIndex(x => x.MovedAtUtc).HasDatabaseName("IX_WipMove_Date");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.FromOperation).WithMany()
                    .HasForeignKey(x => x.FromOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ToOperation).WithMany()
                    .HasForeignKey(x => x.ToOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.TriggeredByTransaction).WithMany()
                    .HasForeignKey(x => x.TriggeredByTransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OriginalMove).WithMany()
                    .HasForeignKey(x => x.OriginalMoveId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.4 PR-1 — CostTransaction entity config.
            // Atomic cost ledger. Every production cost event creates one row.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CostTransaction>(e =>
            {
                e.Property(x => x.TransactionType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostTransactionType.MaterialIssue)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostTransactionType.MaterialIssue);
                e.Property(x => x.CostBucket)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionCostBucket.DirectMaterial);
                e.Property(x => x.CostElement)
                    .HasDefaultValue(Abs.FixedAssets.Models.Masters.CostElementType.Material);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.TransactionNumber })
                    .IsUnique().HasDatabaseName("UX_CostTxn_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.SiteId).HasDatabaseName("IX_CostTxn_Site");
                e.HasIndex(x => new { x.CostObjectType, x.CostObjectId })
                    .HasDatabaseName("IX_CostTxn_CostObject");
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_CostTxn_PRO");
                e.HasIndex(x => x.TransactionType).HasDatabaseName("IX_CostTxn_Type");
                e.HasIndex(x => x.CostBucket).HasDatabaseName("IX_CostTxn_Bucket");
                e.HasIndex(x => x.EffectiveCostDate).HasDatabaseName("IX_CostTxn_Date");
                e.HasIndex(x => x.RollupAdditiveFlag).HasDatabaseName("IX_CostTxn_Additive");
            });

            // Sprint 14.4 PR-1 — CostTransfer entity config.
            // Movement of value between cost objects (Layer B).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CostTransfer>(e =>
            {
                e.Property(x => x.TransferType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostTransferType.ChildCompletionToParent);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.TransferNumber })
                    .IsUnique().HasDatabaseName("UX_CostXfer_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => new { x.SourceCostObjectType, x.SourceCostObjectId })
                    .HasDatabaseName("IX_CostXfer_Source");
                e.HasIndex(x => new { x.DestinationCostObjectType, x.DestinationCostObjectId })
                    .HasDatabaseName("IX_CostXfer_Dest");
                e.HasIndex(x => x.TransferType).HasDatabaseName("IX_CostXfer_Type");
            });

            // Sprint 14.4 PR-1 — ProductionOrderCostSummary entity config.
            // Denormalized cost cache per PRO for cockpit rendering.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrderCostSummary>(e =>
            {
                e.Property(x => x.CostStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionCostStatus.Estimated);
                // B7 PR-3 — variance baseline mode. Default ItemMasterStandard (value 0
                // == CLR sentinel) → HasDefaultValue safe per enum-defaults HARD LOCK.
                e.Property(x => x.VarianceBaselineMode)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.VarianceBaselineMode.ItemMasterStandard);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ProductionOrderId })
                    .IsUnique().HasDatabaseName("UX_ProCostSum_Company_PRO");
                e.HasIndex(x => x.VarianceBaselineMode).HasDatabaseName("IX_ProCostSum_BaselineMode");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.CostStatus).HasDatabaseName("IX_ProCostSum_Status");
            });

            // Sprint 14.4 PR-3 — CostRollupRun entity config.
            // Rollup execution header. Audit trail for "when was cost last rolled up?"
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CostRollupRun>(e =>
            {
                e.Property(x => x.Mode)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostRollupMode.Financial)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostRollupMode.Financial);
                e.Property(x => x.RootCostObjectType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostObjectType.ProductionOrder)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostObjectType.ProductionOrder);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostRollupRunStatus.Running)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostRollupRunStatus.Running);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.RunNumber })
                    .IsUnique().HasDatabaseName("UX_CostRollup_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_CostRollup_PRO");
                e.HasIndex(x => x.Mode).HasDatabaseName("IX_CostRollup_Mode");
                e.HasIndex(x => x.Status).HasDatabaseName("IX_CostRollup_Status");
                e.HasIndex(x => x.StartedAtUtc).HasDatabaseName("IX_CostRollup_Started");
            });

            // Sprint 14.4 PR-3 — CostRollupLine entity config.
            // Each line in rollup output. Tagged with classification for UI rendering.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CostRollupLine>(e =>
            {
                e.Property(x => x.Classification)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostRollupLineClassification.Additive)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostRollupLineClassification.Additive);
                e.Property(x => x.CostBucket)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionCostBucket.DirectMaterial);
                e.HasIndex(x => x.CostRollupRunId).HasDatabaseName("IX_CostRollupLine_RunId");
                e.HasIndex(x => new { x.CostObjectType, x.CostObjectId })
                    .HasDatabaseName("IX_CostRollupLine_CostObject");
                e.HasIndex(x => x.Classification).HasDatabaseName("IX_CostRollupLine_Classification");
                e.HasOne(x => x.CostRollupRun).WithMany(r => r.Lines)
                    .HasForeignKey(x => x.CostRollupRunId).OnDelete(DeleteBehavior.Cascade);
            });

            // Sprint 14.4 PR-3 — CostRollupException entity config.
            // Detected issues during rollup. Per §13 — 16+ exception types.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.CostRollupException>(e =>
            {
                e.Property(x => x.ExceptionType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostExceptionType.MaterialIssuedZeroCost)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostExceptionType.MaterialIssuedZeroCost);
                e.Property(x => x.Severity)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.CostExceptionSeverity.Info)
                    .HasSentinel(Abs.FixedAssets.Models.Production.CostExceptionSeverity.Info);
                e.HasIndex(x => x.CostRollupRunId).HasDatabaseName("IX_CostRollupExc_RunId");
                e.HasIndex(x => x.ExceptionType).HasDatabaseName("IX_CostRollupExc_Type");
                e.HasIndex(x => x.Severity).HasDatabaseName("IX_CostRollupExc_Severity");
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_CostRollupExc_PRO");
                e.HasOne(x => x.CostRollupRun).WithMany(r => r.Exceptions)
                    .HasForeignKey(x => x.CostRollupRunId).OnDelete(DeleteBehavior.Cascade);
            });

            // Sprint 14.4 PR-4 — ProductionVariance entity config.
            // Variance computation per PRO. 5+ variance types.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionVariance>(e =>
            {
                e.Property(x => x.VarianceType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionVarianceType.MaterialUsage)
                    .HasSentinel(Abs.FixedAssets.Models.Production.ProductionVarianceType.MaterialUsage);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ProductionOrderId })
                    .HasDatabaseName("IX_ProdVar_Company_PRO");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.VarianceType).HasDatabaseName("IX_ProdVar_Type");
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ProdVar_PRO");
            });

            // Sprint 14.4 PR-4 — ProductionCloseEvent entity config.
            // Close workflow audit trail with reversal support.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionCloseEvent>(e =>
            {
                e.Property(x => x.Step)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionCloseStep.VarianceComputed)
                    .HasSentinel(Abs.FixedAssets.Models.Production.ProductionCloseStep.VarianceComputed);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.ProductionOrderId })
                    .HasDatabaseName("IX_ProdClose_Company_PRO");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ProdClose_PRO");
                e.HasIndex(x => x.Step).HasDatabaseName("IX_ProdClose_Step");
            });

            // Sprint 14.3 PR-7 — ChangeImpactAnalysis entity config.
            // ECO blast-radius analysis. One per ECO (1:1). CLOSES Sprint 14.3.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.ChangeImpactAnalysis>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ImpactAnalysisStatus.Pending);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.AnalysisNumber })
                    .IsUnique().HasDatabaseName("UX_CIA_Company_Number");
                e.HasIndex(x => new { x.CompanyId, x.EcoId })
                    .IsUnique().HasDatabaseName("UX_CIA_Company_Eco");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status).HasDatabaseName("IX_CIA_Status");
                e.HasOne(x => x.Eco).WithMany()
                    .HasForeignKey(x => x.EcoId).OnDelete(DeleteBehavior.Cascade);
            });

            // Sprint 14.3 PR-7 — ChangeImpactLine entity config.
            // Individual impact lines — one per affected entity.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.ChangeImpactLine>(e =>
            {
                e.Property(x => x.LineType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ImpactLineType.ProductionOrder);
                e.Property(x => x.Severity)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.ImpactSeverity.Info);
                e.HasIndex(x => x.ChangeImpactAnalysisId).HasDatabaseName("IX_CIL_Analysis");
                e.HasIndex(x => x.LineType).HasDatabaseName("IX_CIL_Type");
                e.HasIndex(x => x.AffectedItemId).HasDatabaseName("IX_CIL_Item");
                e.HasOne(x => x.Analysis).WithMany(a => a.Lines)
                    .HasForeignKey(x => x.ChangeImpactAnalysisId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.AffectedItem).WithMany()
                    .HasForeignKey(x => x.AffectedItemId).OnDelete(DeleteBehavior.SetNull);
            });

            // Sprint 14.3 PR-7 — DocumentRedline entity config.
            // Structured markup annotations on document versions.
            modelBuilder.Entity<Abs.FixedAssets.Models.Engineering.DocumentRedline>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.RedlineStatus.Draft);
                e.Property(x => x.Type)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.RedlineType.Dimension);
                e.Property(x => x.Severity)
                    .HasDefaultValue(Abs.FixedAssets.Models.Engineering.RedlineSeverity.Minor);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.RedlineNumber })
                    .IsUnique().HasDatabaseName("UX_DRL_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.DocumentVersionId).HasDatabaseName("IX_DRL_DocVer");
                e.HasIndex(x => x.EcoId).HasDatabaseName("IX_DRL_Eco");
                e.HasIndex(x => x.Status).HasDatabaseName("IX_DRL_Status");
                e.HasOne(x => x.DocumentVersion).WithMany()
                    .HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Eco).WithMany()
                    .HasForeignKey(x => x.EcoId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany()
                    .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.SetNull);
            });

            // B8 PR-PRO-4 — ProductionOperationTransaction entity config.
            // Operation state change log. 19 transaction types.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOperationTransaction>(e =>
            {
                e.Property(x => x.TransactionType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.OperationTransactionType.Start);
                e.Property(x => x.StatusBefore)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionOperationStatus.Scheduled);
                e.Property(x => x.StatusAfter)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.ProductionOperationStatus.Scheduled);
                e.MapXminRowVersion(x => x.RowVersion);
                e.HasIndex(x => new { x.CompanyId, x.TransactionNumber })
                    .IsUnique().HasDatabaseName("UX_OpTxn_Company_Number");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_OpTxn_PRO");
                e.HasIndex(x => x.OperationId).HasDatabaseName("IX_OpTxn_Op");
                e.HasIndex(x => x.TransactionType).HasDatabaseName("IX_OpTxn_Type");
                e.HasIndex(x => x.TransactionDateUtc).HasDatabaseName("IX_OpTxn_Date");
                e.HasOne(x => x.ProductionOrder).WithMany()
                    .HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Operation).WithMany()
                    .HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.OriginalTransaction).WithMany()
                    .HasForeignKey(x => x.OriginalTransactionId).OnDelete(DeleteBehavior.SetNull);
            });

            // ADR-013 / PR #119.13a — extend ProductionJobShopDetail with
            // FK to Nests on the existing NestPlanId placeholder column.
            // SET NULL on nest delete preserves the order history.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionJobShopDetail>(e =>
            {
                e.HasOne(x => x.NestPlan)
                    .WithMany()
                    .HasForeignKey(x => x.NestPlanId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Maintenance Schedules
            modelBuilder.Entity<MaintenanceSchedule>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.NextDueDate);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CIP Projects
            modelBuilder.Entity<CipProject>(e =>
            {
                e.HasIndex(x => x.ProjectNumber).IsUnique();
                e.HasIndex(x => x.Status);
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("CAD");
                e.Ignore(x => x.IsLocked);
                e.HasOne(x => x.ConvertedAsset)
                    .WithMany()
                    .HasForeignKey(x => x.ConvertedAssetId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DepartmentRef)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.GlAccountRef)
                    .WithMany()
                    .HasForeignKey(x => x.GlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // CIP Costs
            modelBuilder.Entity<CipCost>(e =>
            {
                e.HasIndex(x => x.CipProjectId);
                e.HasIndex(x => x.TransactionDate);
                e.HasIndex(x => new { x.CipProjectId, x.CostTypeLookupValueId });
                e.HasOne(x => x.Project)
                    .WithMany(p => p.Costs)
                    .HasForeignKey(x => x.CipProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CostTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.CostTypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.WorkOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrderRef)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrderLineRef)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.GoodsReceiptRef)
                    .WithMany()
                    .HasForeignKey(x => x.GoodsReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.GoodsReceiptLineRef)
                    .WithMany()
                    .HasForeignKey(x => x.GoodsReceiptLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.VendorInvoiceRef)
                    .WithMany()
                    .HasForeignKey(x => x.VendorInvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.VendorInvoiceLineRef)
                    .WithMany()
                    .HasForeignKey(x => x.VendorInvoiceLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.JournalEntryRef)
                    .WithMany()
                    .HasForeignKey(x => x.JournalEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.VendorRef)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CIP Budget Lines
            modelBuilder.Entity<CipBudgetLine>(e =>
            {
                e.HasIndex(x => x.CipProjectId);
                e.HasOne(x => x.Project)
                    .WithMany(p => p.BudgetLines)
                    .HasForeignKey(x => x.CipProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CostTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.CipCostTypeLookupValueId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // CIP Capitalizations
            modelBuilder.Entity<CipCapitalization>(e =>
            {
                e.HasIndex(x => x.CipProjectId);
                e.HasOne(x => x.Project)
                    .WithMany(p => p.Capitalizations)
                    .HasForeignKey(x => x.CipProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.JournalEntry)
                    .WithMany()
                    .HasForeignKey(x => x.JournalEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CIP Capitalization Costs (mapping table)
            modelBuilder.Entity<CipCapitalizationCost>(e =>
            {
                e.HasIndex(x => x.CipCapitalizationId);
                e.HasOne(x => x.Capitalization)
                    .WithMany(c => c.CostMappings)
                    .HasForeignKey(x => x.CipCapitalizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Cost)
                    .WithMany()
                    .HasForeignKey(x => x.CipCostId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Purchase Orders
            modelBuilder.Entity<PurchaseOrder>(e =>
            {
                e.HasOne(x => x.POTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.POTypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // Sprint 12A PR #6 — Advanced Shipping Notice (ASN) header + lines.
            // First-class domain entity replacing the Sprint 11 stop-gap
            // "ASN:" prefix on StockReceipt.SourcePoNumber. The cockpit ASN
            // Queue tab consumes these via GetAsnQueueAsync.
            modelBuilder.Entity<AdvancedShippingNotice>(e =>
            {
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ShipToSite)
                    .WithMany()
                    .HasForeignKey(x => x.ShipToSiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Unique (VendorId, AsnNumber) — vendor can't send dup ASNs.
                e.HasIndex(x => new { x.VendorId, x.AsnNumber }).IsUnique();

                // Cockpit query hot-path: ExpectedArrivalDate drives the
                // ByTimeLens bucketing (Overdue / Today / This Week / Later).
                e.HasIndex(x => x.ExpectedArrivalDate);
                e.HasIndex(x => x.Status);

                e.HasMany(x => x.Lines)
                    .WithOne(l => l.Asn)
                    .HasForeignKey(l => l.AsnId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AsnLine>(e =>
            {
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => x.AsnId);
                // Index for line-level PO reference lookups (multi-PO ASN case).
                e.HasIndex(x => x.RefPoNumber);
            });

            // Attachments
            modelBuilder.Entity<Attachment>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.WorkOrderId);
                e.HasIndex(x => x.CipProjectId);
                e.HasOne(x => x.Asset)
                    .WithMany(a => a.Attachments)
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.WorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.WorkOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CipProject)
                    .WithMany()
                    .HasForeignKey(x => x.CipProjectId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CipCost)
                    .WithMany()
                    .HasForeignKey(x => x.CipCostId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.AssetTransfer)
                    .WithMany()
                    .HasForeignKey(x => x.AssetTransferId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CapitalImprovement)
                    .WithMany()
                    .HasForeignKey(x => x.CapitalImprovementId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CategoryLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Technicians
            modelBuilder.Entity<Technician>(e =>
            {
                e.HasIndex(x => x.Name);
                e.HasIndex(x => x.Active);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.EmployeeId);
                e.HasMany(x => x.WorkOrders)
                    .WithOne(m => m.Technician)
                    .HasForeignKey(m => m.TechnicianId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DepartmentRef)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Supervisor)
                    .WithMany()
                    .HasForeignKey(x => x.SupervisorTechnicianId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.Certifications)
                    .WithOne(c => c.Technician)
                    .HasForeignKey(c => c.TechnicianId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Skills)
                    .WithOne(s => s.Technician)
                    .HasForeignKey(s => s.TechnicianId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Project Managers
            modelBuilder.Entity<ProjectManager>(e =>
            {
                e.HasIndex(x => x.Name);
                e.HasIndex(x => x.Active);
                e.HasMany(x => x.Projects)
                    .WithOne(p => p.ProjectManager)
                    .HasForeignKey(p => p.ProjectManagerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DepartmentRef)
                    .WithMany()
                    .HasForeignKey(x => x.DepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Manufacturers
            modelBuilder.Entity<Manufacturer>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
                e.HasIndex(x => x.Name);
                e.HasIndex(x => x.Active);
                e.HasMany(x => x.Assets)
                    .WithOne(a => a.Manufacturer)
                    .HasForeignKey(a => a.ManufacturerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.ManufacturerParts)
                    .WithOne(mp => mp.Manufacturer)
                    .HasForeignKey(mp => mp.ManufacturerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // GL Accounts
            modelBuilder.Entity<GlAccount>(e =>
            {
                e.HasIndex(x => x.AccountNumber);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Category);
                e.HasOne(x => x.ParentAccount)
                    .WithMany(p => p.ChildAccounts)
                    .HasForeignKey(x => x.ParentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Cost Centers
            modelBuilder.Entity<CostCenter>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasIndex(x => x.CompanyId);
                e.HasOne(x => x.ParentCostCenter)
                    .WithMany(p => p.ChildCostCenters)
                    .HasForeignKey(x => x.ParentCostCenterId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Departments
            modelBuilder.Entity<Department>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasIndex(x => x.CompanyId);
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);

                // B11 R1-1 — production-org backbone.
                // Self-referencing nesting (Site→Dept→sub-Dept). RESTRICT so a
                // parent can't be deleted out from under its children.
                e.HasOne(x => x.ParentDepartment)
                    .WithMany(p => p.ChildDepartments)
                    .HasForeignKey(x => x.ParentDepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.ParentDepartmentId)
                    .HasFilter("\"ParentDepartmentId\" IS NOT NULL")
                    .HasDatabaseName("IX_Departments_Parent_Partial");
                e.HasIndex(x => x.SiteId)
                    .HasFilter("\"SiteId\" IS NOT NULL")
                    .HasDatabaseName("IX_Departments_Site_Partial");
                e.HasIndex(x => x.IsProductionDepartment)
                    .HasFilter("\"IsProductionDepartment\" = TRUE")
                    .HasDatabaseName("IX_Departments_IsProduction_Partial");
                // B11 R1-2 — SiteId real FK → Site (canonical plant tier). SET NULL.
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                // xmin concurrency (HARD LOCK feedback_xmin_pattern_for_concurrency_lock.md).
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // B11 R1-1/R1-2 — production-org backbone + scheduling hardening.
            // SET NULL on department delete (a WC survives its owning department's
            // removal; the floor unit doesn't vanish because the org changed).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.WorkCenter>(e =>
            {
                // R1-2: inverse collection (Department.WorkCenters).
                e.HasOne(x => x.OwningDepartment)
                    .WithMany(d => d.WorkCenters)
                    .HasForeignKey(x => x.OwningDepartmentId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.OwningDepartmentId)
                    .HasFilter("\"OwningDepartmentId\" IS NOT NULL")
                    .HasDatabaseName("IX_WorkCenters_OwningDepartment_Partial");

                // R1-2 — SiteId canonical plant tier (FK → Site). SET NULL.
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.SiteId)
                    .HasFilter("\"SiteId\" IS NOT NULL")
                    .HasDatabaseName("IX_WorkCenters_Site_Partial");

                // R1-2 — bottleneck/drum partial index for the scheduler's constraint scan.
                e.HasIndex(x => x.BottleneckFlag)
                    .HasFilter("\"BottleneckFlag\" = TRUE")
                    .HasDatabaseName("IX_WorkCenters_Bottleneck_Partial");

                // R1-2 — xmin concurrency (closes the WC concurrency gap).
                e.MapXminRowVersion(x => x.RowVersion);

                // R1-3 — CostCenter link (production cost posts here). SET NULL.
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.CostCenterId)
                    .HasFilter("\"CostCenterId\" IS NOT NULL")
                    .HasDatabaseName("IX_WorkCenters_CostCenter_Partial");
            });

            // B11 R1-2 — WorkCenterAlternate (ordered spill targets for the R4 scheduler).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.WorkCenterAlternate>(e =>
            {
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => new { x.WorkCenterId, x.Preference })
                    .HasDatabaseName("IX_WorkCenterAlternates_WC_Preference");
                e.HasIndex(x => new { x.WorkCenterId, x.AlternateWorkCenterId })
                    .IsUnique()
                    .HasDatabaseName("UX_WorkCenterAlternates_WC_Alt");
                // Two FKs to WorkCenter — both RESTRICT to avoid multiple-cascade-paths.
                e.HasOne(x => x.WorkCenter)
                    .WithMany(w => w.Alternates)
                    .HasForeignKey(x => x.WorkCenterId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.AlternateWorkCenter)
                    .WithMany()
                    .HasForeignKey(x => x.AlternateWorkCenterId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            // Locations
            modelBuilder.Entity<Location>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasIndex(x => x.CompanyId);
                e.HasOne(x => x.ParentLocation)
                    .WithMany(p => p.ChildLocations)
                    .HasForeignKey(x => x.ParentLocationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CostCenter)
                    .WithMany()
                    .HasForeignKey(x => x.CostCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Asset Categories
            modelBuilder.Entity<AssetCategory>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasIndex(x => x.CompanyId);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.AssetGlAccount)
                    .WithMany()
                    .HasForeignKey(x => x.AssetGlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.AccumDepGlAccount)
                    .WithMany()
                    .HasForeignKey(x => x.AccumDepGlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DepExpGlAccount)
                    .WithMany()
                    .HasForeignKey(x => x.DepExpGlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Items (Part Master)
            modelBuilder.Entity<Item>(e =>
            {
                e.HasIndex(x => x.PartNumber);
                e.HasIndex(x => x.CompanyId);
                // B6 Foundation Sprint PR-FS-7 (2026-05-26) — searchable + filterable
                // indexes for the new 18-column expansion. Trade compliance fields
                // (ECCN/ScheduleB/IntrastatCode) are looked up at order-export time;
                // PlanningPolicy + MakeBuyCode + LotSizingRule + LifecycleStage are
                // filtered in MRP + Make-or-Buy decision service queries; ItemFamily
                // is used for analytics rollups; IsSellable + AS9100Critical gate
                // production routing.
                e.HasIndex(x => x.PlanningPolicy);
                e.HasIndex(x => x.MakeBuyCode);
                e.HasIndex(x => x.LotSizingRule);
                e.HasIndex(x => x.LifecycleStage);
                // PR-FS-7 Codex P1: DB-side defaults must match C# model defaults so that
                // raw-SQL inserts + EF migration backfill land on the documented semantics.
                // MakeBuyCode.Buy = 1; LifecycleStage.Production = 5. The other two enum
                // properties (PlanningPolicy.MakeToStock = 0, LotSizingRule.LotForLot = 0)
                // already align with the EF-auto zero default and need no override.
                e.Property(x => x.MakeBuyCode).HasDefaultValue(MakeBuyCode.Buy);
                e.Property(x => x.LifecycleStage).HasDefaultValue(LifecycleStage.Production);
                // Theme B7 Wave A PR-1 — PO-as-Standard + Make-or-Buy duality.
                // SourcePattern.StandardFirst = 0, MakeBuyPolicy.Inherit = 0, and
                // DefaultSourcePreference.LetSystemDecide = 0 all align with the EF-auto
                // zero default (the documented semantic default), so per the PR-FS-7
                // Codex P1 lesson they need NO HasDefaultValue override — only indexes
                // for the Make-or-Buy decision service + PoFirst queries.
                e.HasIndex(x => x.SourcePattern);
                e.HasIndex(x => x.MakeBuyPolicy);
                // Theme B7 Wave B PR-6 — StandardCostBasis.Forecast = 0 aligns with the
                // EF-auto zero default (same PR-FS-7 lesson as the PR-1 enums): NO
                // HasDefaultValue override, just an index for cost-basis filtering.
                e.HasIndex(x => x.StandardCostBasis);
                e.HasIndex(x => x.IsSourceControlled);
                e.HasIndex(x => x.ItemFamily);
                e.HasIndex(x => x.IsSellable);
                e.HasIndex(x => x.AS9100Critical);
                e.HasIndex(x => x.ECCN);
                e.HasIndex(x => x.ScheduleB);
                e.HasIndex(x => x.IntrastatCode);
                e.HasIndex(x => x.MrpPlannerCode);
                e.HasOne(x => x.Category)
                    .WithMany(c => c.Items)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PrimaryVendor)
                    .WithMany()
                    .HasForeignKey(x => x.PrimaryVendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Manufacturer)
                    .WithMany()
                    .HasForeignKey(x => x.ManufacturerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.TypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.TypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.CostMethodLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.CostMethodLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.TrackingTypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.TrackingTypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                // DEF-008: best-in-class item-location preference.
                e.HasOne(x => x.DefaultLocationRef)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // DEF-008: per-company item-location preference override.
            modelBuilder.Entity<ItemCompanyStocking>(e =>
            {
                e.HasIndex(x => new { x.ItemId, x.CompanyId }).IsUnique();
                // Drift fix 2026-05-25: name the Item.CompanyStockingSettings collection
                // so EF doesn't create a shadow ItemId1 FK.
                e.HasOne(x => x.Item)
                    .WithMany(i => i.CompanyStockingSettings)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PreferredVendor)
                    .WithMany()
                    .HasForeignKey(x => x.PreferredVendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DefaultLocationRef)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // B6 Foundation Sprint PR-FS-6 (2026-05-26) — CustomerItemXref
            // (SAP CMIR / Oracle Customer Item Cross Reference equivalent).
            // Bidirectional customer-PN ↔ Item resolution. Tenant trio +
            // RowVersion + null-safe partial UNIQUE all baked in from day one
            // per PR-FS-2/4/5 lessons. Service-side NULL-safe uniqueness
            // check on add (PR-FS-5 lesson).
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.CustomerItemXref>(e =>
            {
                // PR-XminBackfill 2026-05-27: converted from IsRowVersion()+bytea
                // to xmin pattern. Was latent 23502 bug on first INSERT — the bytea
                // column had no DB-side default, and IsRowVersion() excluded it from
                // the INSERT statement. Pattern match: PR #365 hotfix for
                // ProductionMaterialStructures.
                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.TenantId, x.CustomerId, x.CustomerPartNumber, x.CustomerRevision })
                    .IsUnique()
                    .HasFilter("\"Status\" = 0 AND \"IsActive\" = TRUE")
                    .HasDatabaseName("UX_CustItmXref_Tenant_Cust_PN_Rev_Active");
                e.HasIndex(x => new { x.CustomerId, x.CustomerPartNumber, x.CustomerRevision })
                    .IsUnique()
                    .HasFilter("\"TenantId\" IS NULL AND \"Status\" = 0 AND \"IsActive\" = TRUE")
                    .HasDatabaseName("UX_CustItmXref_Cust_PN_Rev_Active_NullTenant");

                e.HasIndex(x => new { x.CustomerId, x.CustomerPartNumber })
                    .HasDatabaseName("IX_CustItmXref_Cust_PN");
                e.HasIndex(x => new { x.ItemId, x.CustomerId })
                    .HasDatabaseName("IX_CustItmXref_Item_Cust");
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.SupersededByXrefId);
                e.HasIndex(x => x.CustomerDrawingNumber);
                e.HasIndex(x => x.CustomerSpecificationNumber);

                e.HasOne(x => x.Item)
                    .WithMany(i => i.CustomerXrefs)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.SupersededByXref)
                    .WithMany()
                    .HasForeignKey(x => x.SupersededByXrefId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // B6 Foundation Sprint PR-FS-5 (2026-05-26) — ItemSourcingRule
            // (SAP S/4 Source List equivalent). Multi-source AVL + priority +
            // approval state machine + customer-mandated AS9100 §8.4.1 flag.
            //
            // Uniqueness: a tenant cannot have two rules with the SAME (Item,
            // Site, Vendor, Priority) active simultaneously — that's a duplicate.
            // Apply both the full TenantId-aware UNIQUE and a partial UNIQUE for
            // NULL-tenant rows (PR-FS-2 lesson, applied prophylactically).
            //
            // Concurrency token: RowVersion (PR-FS-4 lesson, applied
            // prophylactically) — ApprovalState transitions are concurrent-write
            // hot paths.
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.ItemSourcingRule>(e =>
            {
                // PR-XminBackfill 2026-05-27: converted from IsRowVersion()+bytea
                // to xmin pattern. Same latent 23502 bug as CostLayer + CustomerItemXref.
                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.TenantId, x.ItemId, x.SiteId, x.VendorId, x.Priority })
                    .IsUnique()
                    .HasFilter("\"IsActive\" = TRUE")
                    .HasDatabaseName("UX_ItemSrcRule_Tenant_Item_Site_Vendor_Prio_Active");
                e.HasIndex(x => new { x.ItemId, x.SiteId, x.VendorId, x.Priority })
                    .IsUnique()
                    .HasFilter("\"TenantId\" IS NULL AND \"IsActive\" = TRUE")
                    .HasDatabaseName("UX_ItemSrcRule_Item_Site_Vendor_Prio_Active_NullTenant");

                e.HasIndex(x => new { x.ItemId, x.SiteId, x.ApprovalState, x.Priority })
                    .HasDatabaseName("IX_ItemSrcRule_ItemSiteState_Prio");
                e.HasIndex(x => x.VendorId);
                e.HasIndex(x => x.TransferFromSiteId);
                e.HasIndex(x => x.CustomerId);
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);

                e.HasOne(x => x.Item)
                    .WithMany(i => i.SourcingRules)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                // PR-FS-5 P2 fix (Codex on PR #361): TransferFromSiteId was
                // only an indexed scalar; promote to a real FK so dangling
                // site IDs can't persist and SetNull cascades on Site delete.
                e.HasOne(x => x.TransferFromSite)
                    .WithMany()
                    .HasForeignKey(x => x.TransferFromSiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // B6 Foundation Sprint PR-FS-4 (2026-05-26) — CostLayer (FIFO/LIFO/Average
            // inventory valuation; SAP MM "stock with values" equivalent). Per
            // (Item, optional Site) LayerNumber is monotonically increasing.
            // Null-safe partial UNIQUE on (ItemId, SiteId, LayerNumber) WHERE TenantId
            // IS NULL — applying the PR-FS-2 Codex P1 lesson prophylactically.
            //
            // The non-partial UNIQUE is (TenantId, ItemId, SiteId, LayerNumber).
            // Secondary lookups: FIFO/LIFO ordering on (ItemId, SiteId, Status,
            // ReceivedAtUtc); reference resolution on (ReceiptType, ReceiptReferenceId).
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.CostLayer>(e =>
            {
                // PR-XminBackfill 2026-05-27: converted from IsRowVersion()+bytea
                // to xmin pattern. Original Codex P1 fix (PR #360) was correct at
                // the time — the xmin pattern wasn't discovered until PR #364/365.
                // Prevents lost-update on concurrent ConsumeQuantityAsync calls.
                e.MapXminRowVersion(x => x.RowVersion);

                e.HasIndex(x => new { x.TenantId, x.ItemId, x.SiteId, x.LayerNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_CostLayer_Tenant_Item_Site_Layer");
                e.HasIndex(x => new { x.ItemId, x.SiteId, x.LayerNumber })
                    .IsUnique()
                    .HasFilter("\"TenantId\" IS NULL")
                    .HasDatabaseName("UX_CostLayer_Item_Site_Layer_NullTenant");
                e.HasIndex(x => new { x.ItemId, x.SiteId, x.Status, x.ReceivedAtUtc })
                    .HasDatabaseName("IX_CostLayer_ItemSiteStatus_ReceivedAt");
                e.HasIndex(x => new { x.ReceiptType, x.ReceiptReferenceId })
                    .HasDatabaseName("IX_CostLayer_ReceiptType_Ref");
                e.HasIndex(x => x.LotNumber);
                e.HasIndex(x => x.SerialNumber);
                e.HasIndex(x => x.HeatNumber);
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.CompanyId);

                e.HasOne(x => x.Item)
                    .WithMany(i => i.CostLayers)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // B6 Foundation Sprint PR-FS-3 (2026-05-26) — Item Standard Cost Element
            // (SAP Cost Component Split equivalent). Effective-dated per (Item, optional
            // Site, ElementType). UNIQUE on (TenantId, ItemId, SiteId, ElementType,
            // EffectiveFromUtc) + partial UNIQUE for NULL-tenant case (lesson from
            // PR-FS-2's Codex P1 — Postgres treats NULL as distinct in unique indexes).
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.ItemStandardCostElement>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.ItemId, x.SiteId, x.ElementType, x.EffectiveFromUtc })
                    .IsUnique()
                    .HasDatabaseName("UX_ItemStdCost_Tenant_Item_Site_Elem_From");
                e.HasIndex(x => new { x.ItemId, x.SiteId, x.ElementType, x.EffectiveFromUtc })
                    .IsUnique()
                    .HasFilter("\"TenantId\" IS NULL")
                    .HasDatabaseName("UX_ItemStdCost_Item_Site_Elem_From_NullTenant");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => new { x.ItemId, x.ElementType, x.EffectiveToUtc });

                e.HasOne(x => x.Item)
                    .WithMany(i => i.StandardCostElements)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // B6 Foundation Sprint PR-FS-2 (2026-05-26) — per-Site Item overrides
            // (SAP MARC equivalent). Unique on (TenantId, ItemId, SiteId). Tenant
            // trio (TenantId, CompanyId, SiteId) per [[reference_bic_entity_checklist]].
            //
            // PR-FS-2 P1 fix (Codex on PR #358): TenantId is nullable, and Postgres
            // treats NULL as distinct in unique indexes — so the
            // (TenantId, ItemId, SiteId) unique alone permits two rows with
            // TenantId=NULL for the same (Item, Site) pair. A SECOND partial unique
            // index closes that hole — when TenantId IS NULL, (ItemId, SiteId) alone
            // must be unique. Lock 12 satisfied: EF .HasFilter() emits the partial
            // index via typed migrationBuilder, not raw SQL.
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.ItemSite>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.ItemId, x.SiteId }).IsUnique();
                e.HasIndex(x => new { x.ItemId, x.SiteId })
                    .IsUnique()
                    .HasFilter("\"TenantId\" IS NULL");
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.ItemGroupId);
                e.HasIndex(x => x.PreferredVendorId);
                e.HasIndex(x => x.DefaultLocationId);

                e.HasOne(x => x.Item)
                    .WithMany(i => i.SiteOverrides)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PreferredVendor)
                    .WithMany()
                    .HasForeignKey(x => x.PreferredVendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DefaultLocationRef)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ItemGroup)
                    .WithMany()
                    .HasForeignKey(x => x.ItemGroupId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Item Categories
            modelBuilder.Entity<ItemCategory>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasOne(x => x.ParentCategory)
                    .WithMany(p => p.ChildCategories)
                    .HasForeignKey(x => x.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.DefaultGlAccount)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultGlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ExpenseGlAccount)
                    .WithMany()
                    .HasForeignKey(x => x.ExpenseGlAccountId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Item Vendors (multiple vendors per item)
            modelBuilder.Entity<ItemVendor>(e =>
            {
                e.HasIndex(x => new { x.ItemId, x.VendorId }).IsUnique();
                e.HasOne(x => x.Item)
                    .WithMany(i => i.ItemVendors)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Item Revisions
            modelBuilder.Entity<ItemRevision>(e =>
            {
                e.HasIndex(x => new { x.ItemId, x.RevisionCode }).IsUnique();
                e.Property(x => x.RevisionCode).HasMaxLength(10).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200);
                e.Ignore(x => x.Revision);
                e.Ignore(x => x.ChangeDescription);
                e.Ignore(x => x.EffectiveDate);
                e.Ignore(x => x.SupersededDate);
                e.Ignore(x => x.ChangedBy);
                e.Ignore(x => x.ApprovedBy);
                e.Ignore(x => x.ApprovedDate);
                e.Ignore(x => x.IsCurrent);
                e.Ignore(x => x.CreatedAt);
                e.HasOne(x => x.Item)
                    .WithMany(i => i.Revisions)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SupersedesRevision)
                    .WithMany()
                    .HasForeignKey(x => x.SupersedesItemRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Item + CurrentReleasedRevision relationship
            modelBuilder.Entity<Item>(e =>
            {
                e.HasOne(x => x.CurrentReleasedRevision)
                    .WithMany()
                    .HasForeignKey(x => x.CurrentReleasedRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Item Manufacturer Parts (MPN cross-reference)
            modelBuilder.Entity<Abs.FixedAssets.Models.Revisions.ItemManufacturerPart>(e =>
            {
                e.HasIndex(x => new { x.ItemId, x.ManufacturerId, x.MfrPartNumber }).IsUnique();
                e.HasIndex(x => x.MfrPartNumber);
                e.Property(x => x.MfrPartNumber).HasMaxLength(100).IsRequired();
                e.HasOne(x => x.Item)
                    .WithMany(i => i.ManufacturerParts)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Vendor Item Parts (VPN cross-reference)
            modelBuilder.Entity<Abs.FixedAssets.Models.Revisions.VendorItemPart>(e =>
            {
                e.HasIndex(x => new { x.VendorId, x.VendorPartNumber }).IsUnique();
                e.HasIndex(x => x.VendorPartNumber);
                e.Property(x => x.VendorPartNumber).HasMaxLength(100).IsRequired();
                e.HasOne(x => x.Vendor)
                    .WithMany(v => v.VendorItemParts)
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany(i => i.VendorItemParts)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ItemManufacturerPart)
                    .WithMany(mp => mp.VendorItemParts)
                    .HasForeignKey(x => x.ItemManufacturerPartId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Procurement-Grade Parts (Sprint 12)
            modelBuilder.Entity<ItemApprovedVendor>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.ItemId, x.VendorId }).IsUnique();
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ItemAlternate>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.ItemId, x.AlternateItemId }).IsUnique();
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.AlternateItem).WithMany().HasForeignKey(x => x.AlternateItemId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ItemSupersession>(e =>
            {
                e.HasIndex(x => new { x.TenantId, x.OldItemId }).IsUnique();
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.OldItem).WithMany().HasForeignKey(x => x.OldItemId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.NewItem).WithMany().HasForeignKey(x => x.NewItemId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            // Item Inventory
            modelBuilder.Entity<ItemInventory>(e =>
            {
                e.HasIndex(x => new { x.ItemId, x.LocationId, x.Bin });
                e.HasOne(x => x.Item)
                    .WithMany(i => i.Inventory)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Location)
                    .WithMany()
                    .HasForeignKey(x => x.LocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Item Transactions
            modelBuilder.Entity<ItemTransaction>(e =>
            {
                e.HasIndex(x => x.TransactionNumber);
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.TransactionDate);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.FromLocation)
                    .WithMany()
                    .HasForeignKey(x => x.FromLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ToLocation)
                    .WithMany()
                    .HasForeignKey(x => x.ToLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PM Templates
            modelBuilder.Entity<PMTemplate>(e =>
            {
                e.HasIndex(x => x.Code);
                e.HasIndex(x => x.CompanyId);
                e.HasOne(x => x.AssetCategory)
                    .WithMany()
                    .HasForeignKey(x => x.AssetCategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Manufacturer)
                    .WithMany()
                    .HasForeignKey(x => x.ManufacturerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PM Template Items (parts list for PM)
            modelBuilder.Entity<PMTemplateItem>(e =>
            {
                e.HasIndex(x => new { x.PMTemplateId, x.ItemId }).IsUnique();
                e.HasOne(x => x.PMTemplate)
                    .WithMany(t => t.Items)
                    .HasForeignKey(x => x.PMTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany(i => i.PMTemplateItems)
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PM Template Assets (assets linked to templates)
            modelBuilder.Entity<PMTemplateAsset>(e =>
            {
                e.HasIndex(x => new { x.PMTemplateId, x.AssetId }).IsUnique();
                e.HasOne(x => x.PMTemplate)
                    .WithMany(t => t.Assets)
                    .HasForeignKey(x => x.PMTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Meter Readings
            modelBuilder.Entity<MeterReading>(e =>
            {
                e.HasIndex(x => new { x.AssetId, x.MeterType, x.ReadingDate });
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Kits
            modelBuilder.Entity<Kit>(e =>
            {
                e.HasIndex(x => x.KitNumber);
                e.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Kit Items
            modelBuilder.Entity<KitItem>(e =>
            {
                e.HasIndex(x => new { x.KitId, x.ItemId }).IsUnique();
                e.HasOne(x => x.Kit)
                    .WithMany(k => k.Items)
                    .HasForeignKey(x => x.KitId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Work Order Parts
            modelBuilder.Entity<WorkOrderPart>(e =>
            {
                e.HasIndex(x => new { x.WorkOrderId, x.ItemId });
                e.HasOne(x => x.WorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.WorkOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.IssuedFromLocation)
                    .WithMany()
                    .HasForeignKey(x => x.IssuedFromLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Lessons Learned
            modelBuilder.Entity<LessonLearned>(e =>
            {
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.SourceWorkOrderId);
                e.HasIndex(x => x.FailureCode);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.AssetCategory)
                    .WithMany()
                    .HasForeignKey(x => x.AssetCategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.SourceWorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.SourceWorkOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Outbox Events
            modelBuilder.Entity<OutboxEvent>(e =>
            {
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => new { x.Status, x.NextAttemptAt });
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Webhook Subscriptions
            modelBuilder.Entity<WebhookSubscription>(e =>
            {
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.IsActive);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Webhook Delivery Logs
            modelBuilder.Entity<WebhookDeliveryLog>(e =>
            {
                e.HasIndex(x => x.WebhookSubscriptionId);
                e.HasIndex(x => x.OutboxEventId);
                e.HasOne(x => x.WebhookSubscription)
                    .WithMany(s => s.DeliveryLogs)
                    .HasForeignKey(x => x.WebhookSubscriptionId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.OutboxEvent)
                    .WithMany()
                    .HasForeignKey(x => x.OutboxEventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PM Template Revisions
            modelBuilder.Entity<Abs.FixedAssets.Models.Revisions.PMTemplateRevision>(e =>
            {
                e.HasIndex(x => new { x.PMTemplateId, x.RevisionCode }).IsUnique();
                e.HasOne(x => x.PMTemplate)
                    .WithMany(t => t.Revisions)
                    .HasForeignKey(x => x.PMTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SupersedesRevision)
                    .WithMany()
                    .HasForeignKey(x => x.SupersedesRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PM Template Revision Operations
            modelBuilder.Entity<Abs.FixedAssets.Models.Revisions.PMTemplateRevisionOperation>(e =>
            {
                e.HasIndex(x => new { x.PMTemplateRevisionId, x.Sequence });
                e.HasOne(x => x.PMTemplateRevision)
                    .WithMany(r => r.Operations)
                    .HasForeignKey(x => x.PMTemplateRevisionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PM Template - CurrentReleasedRevision FK
            modelBuilder.Entity<PMTemplate>(e =>
            {
                e.HasOne(x => x.CurrentReleasedRevision)
                    .WithMany()
                    .HasForeignKey(x => x.CurrentReleasedRevisionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PMOccurrence — drift fix 2026-05-25: previously had no explicit
            // configuration, so EF Convention paired with WorkOrder.PMOccurrenceId
            // (an intentionally-FK-only int column with no navigation) and created
            // a shadow PMOccurrence.WorkOrderId1 column. Explicitly anchoring the
            // PMOccurrence -> WorkOrder relationship here tells EF this is the
            // ONLY relationship between the two entities; WorkOrder.PMOccurrenceId
            // remains a convenience lookup column without a navigation.
            modelBuilder.Entity<PMOccurrence>(e =>
            {
                e.HasOne(x => x.WorkOrder)
                    .WithMany()
                    .HasForeignKey(x => x.WorkOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // PartialDisposal
            modelBuilder.Entity<PartialDisposal>(e =>
            {
                e.HasOne(x => x.ReasonLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.ReasonLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Site
            modelBuilder.Entity<Site>(e =>
            {
                e.HasOne(x => x.TypeLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.TypeLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // GoodsReceipt
            modelBuilder.Entity<GoodsReceipt>(e =>
            {
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // GoodsReceiptLine — Sprint 15.1 PR-1 Receipt-to-Job FKs.
            // Direct-to-job receipt lines bypass inventory and post cost directly
            // to a Production Order BOM line. RESTRICT on PRO FK (don't orphan
            // receipt audit trail). SetNull on BOM line FK (snapshot line may be
            // re-captured on PRO revision — receipt history survives).
            modelBuilder.Entity<GoodsReceiptLine>(e =>
            {
                e.HasOne(x => x.DirectToJobProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.DirectToJobProductionOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.DirectToJobBomLine)
                    .WithMany()
                    .HasForeignKey(x => x.DirectToJobBomLineId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // VendorInvoice
            modelBuilder.Entity<VendorInvoice>(e =>
            {
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // PurchaseRequisition
            modelBuilder.Entity<PurchaseRequisition>(e =>
            {
                e.HasOne(x => x.StatusLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.StatusLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PriorityLookupValue)
                    .WithMany()
                    .HasForeignKey(x => x.PriorityLookupValueId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<LookupType>(e =>
            {
                e.ToTable("LookupTypes");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TenantId, x.CompanyId, x.Key }).IsUnique();
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
                e.Property(x => x.Key).HasMaxLength(100).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            });

            modelBuilder.Entity<LookupValue>(e =>
            {
                e.ToTable("LookupValues");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.LookupTypeId, x.Code }).IsUnique();
                e.HasIndex(x => new { x.LookupTypeId, x.IsActive, x.SortOrder });
                e.HasOne(x => x.LookupType).WithMany(t => t.Values).HasForeignKey(x => x.LookupTypeId).OnDelete(DeleteBehavior.Cascade);
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Metadata).HasColumnType("jsonb");
            });

            modelBuilder.Entity<OrgNode>(e =>
            {
                e.ToTable("org_node", "platform");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.TenantCode).HasColumnName("tenant_code").HasMaxLength(50).IsRequired();
                e.Property(x => x.NodeType).HasColumnName("node_type").HasMaxLength(20).IsRequired();
                e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
                e.Property(x => x.Code).HasColumnName("code").HasMaxLength(50);
                e.Property(x => x.ParentId).HasColumnName("parent_id");
                e.Property(x => x.CompanyId).HasColumnName("company_id");
                e.Property(x => x.SiteId).HasColumnName("site_id");
                e.Property(x => x.LocationId).HasColumnName("location_id");
                e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                e.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
                e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.TenantCode, x.NodeType });
                e.HasIndex(x => x.ParentId);
                e.HasIndex(x => x.CompanyId);
                e.HasIndex(x => x.SiteId);
                e.HasIndex(x => x.LocationId);
            });

            modelBuilder.Entity<Customer>(e =>
            {
                e.ToTable("Customers");
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.CompanyId, x.CustomerCode }).IsUnique();
                e.Property(x => x.CustomerCode).HasMaxLength(20).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            });

            modelBuilder.Entity<CustomerInvoice>(e =>
            {
                e.ToTable("CustomerInvoices");
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Customer).WithMany(c => c.Invoices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.CompanyId, x.InvoiceNumber }).IsUnique();
                e.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
            });

            modelBuilder.Entity<CustomerInvoiceLine>(e =>
            {
                e.ToTable("CustomerInvoiceLines");
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Invoice).WithMany(i => i.Lines).HasForeignKey(x => x.CustomerInvoiceId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MachineSpecification>(entity =>
            {
                entity.HasOne(ms => ms.Asset)
                    .WithOne(a => a.MachineSpecification)
                    .HasForeignKey<MachineSpecification>(ms => ms.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(ms => ms.AssetId).IsUnique();
                entity.HasIndex(ms => ms.TenantId);
            });

            // Sprint 2 PR #117.2 — Equipment Catalog fluent config.
            // The catalog is small (~20 classes, ~100 models, ~150 sensor
            // profiles) but it's the source of truth that every asset seed
            // references, so the indexes matter for the seeder lookup path.
            modelBuilder.Entity<Abs.FixedAssets.Models.Catalog.EquipmentClass>(e =>
            {
                e.ToTable("EquipmentClasses");
                e.HasIndex(x => x.Code).IsUnique();
                e.HasIndex(x => x.Category);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Catalog.EquipmentModel>(e =>
            {
                e.ToTable("EquipmentModels");
                e.HasOne(x => x.EquipmentClass)
                    .WithMany(c => c.Models)
                    .HasForeignKey(x => x.EquipmentClassId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => x.EquipmentClassId);
                e.HasIndex(x => new { x.Manufacturer, x.ModelNumber }).IsUnique();
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Catalog.SensorProfile>(e =>
            {
                e.ToTable("SensorProfiles");
                e.HasOne(x => x.EquipmentClass)
                    .WithMany(c => c.SensorProfiles)
                    .HasForeignKey(x => x.EquipmentClassId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => x.EquipmentClassId);
                e.HasIndex(x => new { x.EquipmentClassId, x.ReadingType });
            });

            // -------- Sprint 2 PR #118.1 — Telemetry substrate (ADR-011) --------

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorEvent>(e =>
            {
                // TimescaleDB hypertable requires the partitioning column
                // (ReadingAt) to participate in every uniqueness constraint.
                // Composite PK (Id, ReadingAt). Id remains bigserial; the
                // app-layer hot path queries by (AssetId, ReadingType,
                // ReadingAt) per the index below.
                e.HasKey(x => new { x.Id, x.ReadingAt });
                e.Property(x => x.Id).ValueGeneratedOnAdd();

                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.AssetId, x.ReadingType, x.ReadingAt })
                    .HasDatabaseName("ix_sensorevent_asset_type_time");

                // Partial index for the OOS-only hot path used by
                // SensorAlarmService + AssetHealthService.
                e.HasIndex(x => new { x.AssetId, x.ReadingAt })
                    .HasFilter("\"IsOutOfSpec\" = true")
                    .HasDatabaseName("ix_sensorevent_oos");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorRollupMinute>(e =>
            {
                e.HasNoKey();
                e.ToView("SensorRollupMinute");
            });
            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorRollupHour>(e =>
            {
                e.HasNoKey();
                e.ToView("SensorRollupHour");
            });
            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorRollupDay>(e =>
            {
                e.HasNoKey();
                e.ToView("SensorRollupDay");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.AssetSensorLatest>(e =>
            {
                e.HasKey(x => new { x.AssetId, x.ReadingType });

                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorSnapshot>(e =>
            {
                e.HasOne(x => x.CapturedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CapturedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => x.CapturedAt);
                e.HasIndex(x => new { x.Reason, x.CapturedAt });
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorSnapshotValue>(e =>
            {
                e.HasOne(x => x.Snapshot)
                    .WithMany(s => s.Values)
                    .HasForeignKey(x => x.SnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.SnapshotId, x.AssetId, x.ReadingType });
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.SensorAlarm>(e =>
            {
                e.HasOne(x => x.Asset)
                    .WithMany()
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Rationalization)
                    .WithMany()
                    .HasForeignKey(x => x.RationalizationId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.AcknowledgedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.AcknowledgedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.ShelvedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ShelvedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => new { x.AssetId, x.ReadingType, x.State })
                    .HasDatabaseName("ix_sensoralarm_asset_type_state");
                e.HasIndex(x => x.State)
                    .HasDatabaseName("ix_sensoralarm_state");
                e.HasIndex(x => x.OpenedAt);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.AlarmRationalization>(e =>
            {
                e.HasOne(x => x.EquipmentClass)
                    .WithMany()
                    .HasForeignKey(x => x.EquipmentClassId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Lookup index: resolve the active rationalization for a
                // given (class, reading-type, priority) at alarm-open time.
                // Not unique because we keep historical revisions.
                e.HasIndex(x => new { x.EquipmentClassId, x.ReadingType, x.Priority, x.Active })
                    .HasDatabaseName("ix_alarmrationalization_lookup");
                e.HasIndex(x => new { x.AlarmKey, x.Version }).IsUnique();
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Telemetry.UnitConversion>(e =>
            {
                e.HasIndex(x => new { x.FromUnit, x.ToUnit, x.Active })
                    .HasDatabaseName("ix_unitconversion_lookup");
            });

            // ============================================================
            // Sprint 13.5 PR #1 / ADR-026 — Customer-Project foundation.
            //
            // - Program  : portfolio bucket. Empty in v1; reserved for v2
            //              EVM / DCAA portfolio rollup. UNIQUE (CompanyId, Code).
            // - CustomerProject : customer-facing project. UNIQUE (CompanyId,
            //              Code). Optional links to Company / Program /
            //              PrimaryCustomer / ProjectManager — all SET NULL
            //              so project history survives administrative cleanup.
            // - ProjectMember   : M:N junction for joint-venture / pass-
            //              through. UNIQUE (CustomerProjectId, CustomerId, Role).
            //              CASCADE on project delete (member rows are not
            //              independently meaningful).
            // - ProjectPhase    : flat-but-tree-capable WBS via ParentPhaseId
            //              self-FK (SET NULL on parent delete). UNIQUE
            //              (CustomerProjectId, Code). CASCADE on project delete.
            //
            // - ProductionOrder extension : nullable CustomerProjectId /
            //              ProjectPhaseId / ProjectPostingMode columns wired
            //              with SET NULL on project / phase delete. Partial
            //              indexes (WHERE CustomerProjectId IS NOT NULL) so
            //              job-shop-mode (no-project) lookups remain cheap.
            // ============================================================

            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.Program>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.CustomerProject>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.Mode);
                e.HasIndex(x => x.PrimaryCustomerId);
                e.HasIndex(x => x.ProgramId);
                e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("CAD");
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
                // Drift fix 2026-05-25: name the Program.Projects collection
                // so EF doesn't create a shadow ProgramId1 FK.
                e.HasOne(x => x.Program)
                    .WithMany(p => p.Projects)
                    .HasForeignKey(x => x.ProgramId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PrimaryCustomer)
                    .WithMany()
                    .HasForeignKey(x => x.PrimaryCustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProjectManager)
                    .WithMany()
                    .HasForeignKey(x => x.ProjectManagerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.ProjectMember>(e =>
            {
                e.HasIndex(x => new { x.CustomerProjectId, x.CustomerId, x.Role }).IsUnique();
                e.HasIndex(x => x.CustomerId);
                // Drift fix 2026-05-25: name the CustomerProject.Members collection
                // so EF doesn't create a shadow CustomerProjectId1 FK.
                e.HasOne(x => x.Project)
                    .WithMany(p => p.Members)
                    .HasForeignKey(x => x.CustomerProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.ProjectPhase>(e =>
            {
                e.HasIndex(x => new { x.CustomerProjectId, x.Code }).IsUnique();
                e.HasIndex(x => x.ParentPhaseId);
                // Drift fix 2026-05-25: name the CustomerProject.Phases collection
                // so EF doesn't create a shadow CustomerProjectId1 FK.
                e.HasOne(x => x.Project)
                    .WithMany(p => p.Phases)
                    .HasForeignKey(x => x.CustomerProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ParentPhase)
                    .WithMany()
                    .HasForeignKey(x => x.ParentPhaseId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ProductionOrder gets the new nullable FKs + a partial index
            // on CustomerProjectId so job-shop-mode lookups (the vast
            // majority of rows have NULL here) stay fast.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrder>(e =>
            {
                e.HasOne(x => x.CustomerProject)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerProjectId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProjectPhase)
                    .WithMany()
                    .HasForeignKey(x => x.ProjectPhaseId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.CustomerProjectId)
                    .HasDatabaseName("ix_productionorders_customerproject")
                    .HasFilter("\"CustomerProjectId\" IS NOT NULL");
                e.HasIndex(x => x.ProjectPhaseId)
                    .HasDatabaseName("ix_productionorders_projectphase")
                    .HasFilter("\"ProjectPhaseId\" IS NOT NULL");
            });

            // ============================================================
            // Sprint 13.5 PR #1.5 — Field expansion + ProjectAmendments.
            //
            // Most of this layer's DDL (CHECK constraints, triggers, partial
            // indexes with the cockpit at-risk filter, Companies governance
            // flag) lives in the raw-SQL migration 20260523_Add
            // CustomerProjectFieldExpansion. This fluent block wires the
            // EF relationship + relational indexes for the LINQ side.
            // See docs/research/customerproject-field-set.md for the
            // full design rationale.
            // ============================================================

            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.ProjectAmendment>(e =>
            {
                e.HasIndex(x => new { x.CustomerProjectId, x.AmendmentNumber })
                    .IsUnique()
                    .HasDatabaseName("ix_projectamendments_project_number");
                e.HasIndex(x => new { x.CustomerProjectId, x.Status, x.EffectiveDate })
                    .HasDatabaseName("ix_projectamendments_project_status_date");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("ix_projectamendments_status")
                    .HasFilter("\"Status\" IN (0, 1)");
                // Drift fix 2026-05-25: name the CustomerProject.Amendments collection
                // so EF doesn't create a shadow CustomerProjectId1 FK.
                e.HasOne(x => x.Project)
                    .WithMany(p => p.Amendments)
                    .HasForeignKey(x => x.CustomerProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ApprovedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovedById)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CustomerProject gets the new cockpit-sort indexes. The raw-
            // SQL migration creates them with the cockpit filter; this
            // declaration tells EF they exist so .Include / sort hints
            // can opt in correctly.
            modelBuilder.Entity<Abs.FixedAssets.Models.Projects.CustomerProject>(e =>
            {
                e.HasIndex(x => x.RiskScore)
                    .HasDatabaseName("ix_customerprojects_riskscore")
                    .HasFilter("\"RiskScore\" IS NOT NULL")
                    .IsDescending();
                e.HasIndex(x => x.RiskScore)
                    .HasDatabaseName("ix_customerprojects_atrisk_queue")
                    .HasFilter("\"RiskTone\" IN (1, 2)")
                    .IsDescending();
            });

            // ============================================================
            // Sprint 13.5 PRA-1 — Master Files: Carrier + Customer/Vendor/Manufacturer
            // verticalization + Company.IndustryVertical. Raw-SQL migration
            // 20260524_AddMasterFilesPRA1 handles the DDL (12 carrier seeds,
            // CHECK constraints, demo-tenant IndustryVertical seed updates).
            // This fluent block wires the EF relationship side.
            // ============================================================

            modelBuilder.Entity<Carrier>(e =>
            {
                // UNIQUE (COALESCE(CompanyId, 0), Code) per audit
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .IsUnique()
                    .HasDatabaseName("uq_carriers_company_code");
                e.HasIndex(x => x.ScacCode)
                    .HasFilter("\"ScacCode\" IS NOT NULL")
                    .HasDatabaseName("ix_carriers_scac");
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AdvancedShippingNotice>(e =>
            {
                e.HasOne(x => x.CarrierRef)
                    .WithMany()
                    .HasForeignKey(x => x.CarrierId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.CarrierId)
                    .HasFilter("\"CarrierId\" IS NOT NULL")
                    .HasDatabaseName("ix_asn_carrierid");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.ShippingMethod>(e =>
            {
                e.HasOne(x => x.CarrierRef)
                    .WithMany()
                    .HasForeignKey(x => x.CarrierId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => x.CarrierId)
                    .HasFilter("\"CarrierId\" IS NOT NULL")
                    .HasDatabaseName("ix_shippingmethods_carrierid");
            });

            // Customers gets cockpit-sort partial indexes on default-inheritance
            // columns (drives the project-create form's inheritance hints).
            modelBuilder.Entity<Customer>(e =>
            {
                e.HasIndex(x => x.DefaultQualityProgram)
                    .HasFilter("\"DefaultQualityProgram\" IS NOT NULL")
                    .HasDatabaseName("ix_customers_defaultqualityprogram");
                e.HasIndex(x => x.DefaultExportControl)
                    .HasFilter("\"DefaultExportControl\" IS NOT NULL")
                    .HasDatabaseName("ix_customers_defaultexportcontrol");
            });

            // Vendors and Manufacturers get regulator-ID partial indexes
            // (operators search by these codes — make the lookup cheap).
            modelBuilder.Entity<Vendor>(e =>
            {
                e.HasIndex(x => x.CageCode)
                    .HasFilter("\"CageCode\" IS NOT NULL")
                    .HasDatabaseName("ix_vendors_cagecode");
                e.HasIndex(x => x.DunsNumber)
                    .HasFilter("\"DunsNumber\" IS NOT NULL")
                    .HasDatabaseName("ix_vendors_duns");
                e.HasIndex(x => x.FdaEstablishmentId)
                    .HasFilter("\"FdaEstablishmentId\" IS NOT NULL")
                    .HasDatabaseName("ix_vendors_fda");
                e.HasIndex(x => x.DeaRegistration)
                    .HasFilter("\"DeaRegistration\" IS NOT NULL")
                    .HasDatabaseName("ix_vendors_dea");
            });

            modelBuilder.Entity<Manufacturer>(e =>
            {
                e.HasIndex(x => x.CageCode)
                    .HasFilter("\"CageCode\" IS NOT NULL")
                    .HasDatabaseName("ix_manufacturers_cagecode");
            });

            // Companies — IndustryVertical lookup index for cockpit branching.
            modelBuilder.Entity<Company>(e =>
            {
                e.HasIndex(x => x.IndustryVertical)
                    .HasDatabaseName("ix_companies_industryvertical");
            });

            // ============================================================
            // Sprint 13.5 PR #1.75 — AS9102 FAI workflow.
            //
            // Raw-SQL migration 20260523_AddFaiWorkflow handles the DDL
            // (tables, CHECK constraints, indexes, status-regression
            // trigger, +3 nullable FK cols on Attachments). This block
            // wires the EF relationship side.
            // See docs/research/fai-workflow-schema.md.
            // ============================================================

            modelBuilder.Entity<Abs.FixedAssets.Models.Quality.FaiReport>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.FaiNumber }).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.CustomerProjectId)
                    .HasFilter("\"CustomerProjectId\" IS NOT NULL");
                e.HasIndex(x => x.ProductionOrderId)
                    .HasFilter("\"ProductionOrderId\" IS NOT NULL");
                e.HasIndex(x => x.BaselineFaiReportId)
                    .HasFilter("\"BaselineFaiReportId\" IS NOT NULL");

                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Tenant)
                    .WithMany()
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CustomerProject)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerProjectId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.StockReceipt)
                    .WithMany()
                    .HasForeignKey(x => x.StockReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.BaselineFaiReport)
                    .WithMany()
                    .HasForeignKey(x => x.BaselineFaiReportId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.SubmittedBy)
                    .WithMany()
                    .HasForeignKey(x => x.SubmittedById)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ApprovedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovedById)
                    .OnDelete(DeleteBehavior.SetNull);
                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Quality.FaiCharacteristic>(e =>
            {
                e.HasIndex(x => new { x.FaiReportId, x.BalloonNumber }).IsUnique();
                e.HasIndex(x => x.Conformance);
                e.HasIndex(x => x.MrbDispositionId)
                    .HasFilter("\"MrbDispositionId\" IS NOT NULL");

                // Drift fix 2026-05-25: name the parent collection so EF
                // doesn't create a shadow FaiReportId1 for the FaiReport.Characteristics
                // navigation (otherwise EF treats it as a second, unconfigured relationship).
                e.HasOne(x => x.FaiReport)
                    .WithMany(r => r.Characteristics)
                    .HasForeignKey(x => x.FaiReportId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Inspector)
                    .WithMany()
                    .HasForeignKey(x => x.InspectorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.MrbDisposition)
                    .WithMany()
                    .HasForeignKey(x => x.MrbDispositionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Quality.FaiProductAccountability>(e =>
            {
                e.HasIndex(x => new { x.FaiReportId, x.EntryType });
                e.HasIndex(x => x.HeatNumber)
                    .HasFilter("\"HeatNumber\" IS NOT NULL");

                // Drift fix 2026-05-25: name the parent collection so EF
                // doesn't create a shadow FaiReportId1 for the FaiReport.ProductAccountability
                // navigation.
                e.HasOne(x => x.FaiReport)
                    .WithMany(r => r.ProductAccountability)
                    .HasForeignKey(x => x.FaiReportId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ============================================================
            // Sprint 13.5 PR #337 — /Admin/AssetImport batch + row tables.
            // Configures the FK relationships and check constraints. The
            // typed mb.CreateTable in the migration handles the DDL; the
            // CHECK constraints are added in this block via HasCheckConstraint
            // so the snapshot reflects them (Lock 12).
            // ============================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.AssetImport.AssetImportBatch>(e =>
            {
                e.HasOne(b => b.Company)
                    .WithMany()
                    .HasForeignKey(b => b.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(b => b.Site)
                    .WithMany()
                    .HasForeignKey(b => b.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(b => b.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(b => b.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(b => b.CommittedByUser)
                    .WithMany()
                    .HasForeignKey(b => b.CommittedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "ck_assetimportbatches_status_range",
                        "\"Status\" BETWEEN 0 AND 4");
                    t.HasCheckConstraint(
                        "ck_assetimportbatches_rowcounts_nonneg",
                        "\"RowCount\" >= 0 AND \"ValidRowCount\" >= 0 AND \"ErrorRowCount\" >= 0");
                    t.HasCheckConstraint(
                        "ck_assetimportbatches_rowcounts_balanced",
                        "\"ValidRowCount\" + \"ErrorRowCount\" <= \"RowCount\"");
                });
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.AssetImport.AssetImportRow>(e =>
            {
                e.HasOne(r => r.Batch)
                    .WithMany(b => b.Rows)
                    .HasForeignKey(r => r.BatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "ck_assetimportrows_status_range",
                        "\"Status\" BETWEEN 0 AND 3");
                    t.HasCheckConstraint(
                        "ck_assetimportrows_rownumber_pos",
                        "\"RowNumber\" >= 2");
                });
            });

            // ============================================================
            // Sprint 13.5 PRA-2 — Country / Subdivision / WorkCalendar /
            // Holiday masters. Most DDL (CHECK constraints, partial
            // indexes, FK NOT VALID) lives in the migration itself; here
            // we just declare the unique-index shapes EF needs to know
            // about for query generation + FK relationships for nav
            // properties.
            // ============================================================

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.Country>(e =>
            {
                e.HasIndex(x => x.Alpha2)
                    .IsUnique()
                    .HasDatabaseName("uq_countries_alpha2");
                e.HasIndex(x => x.Alpha3)
                    .IsUnique()
                    .HasDatabaseName("uq_countries_alpha3");
                e.HasIndex(x => new { x.IsActive, x.SortOrder })
                    .HasDatabaseName("ix_countries_active_sort");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.Subdivision>(e =>
            {
                e.HasIndex(x => new { x.CountryId, x.Code })
                    .IsUnique()
                    .HasDatabaseName("uq_subdivisions_country_code");
                e.HasIndex(x => new { x.CountryId, x.IsActive })
                    .HasDatabaseName("ix_subdivisions_country_active");

                e.HasOne(x => x.Country)
                    .WithMany(c => c.Subdivisions)
                    .HasForeignKey(x => x.CountryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.WorkCalendar>(e =>
            {
                // UNIQUE (COALESCE(CompanyId,0), Code) — declared via raw
                // SQL in the migration; EF can't express the COALESCE
                // expression on a composite index. We declare a non-unique
                // index here so query planning gets the hint.
                e.HasIndex(x => new { x.CompanyId, x.IsActive })
                    .HasDatabaseName("ix_workcalendars_company_active");

                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.Holiday>(e =>
            {
                e.HasIndex(x => new { x.WorkCalendarId, x.ObservedDate })
                    .HasDatabaseName("ix_holidays_calendar_date")
                    .HasFilter("\"IsActive\" = TRUE");
                e.HasIndex(x => x.SubdivisionId)
                    .HasDatabaseName("ix_holidays_subdivision")
                    .HasFilter("\"SubdivisionId\" IS NOT NULL");

                e.HasOne(x => x.WorkCalendar)
                    .WithMany(c => c.Holidays)
                    .HasForeignKey(x => x.WorkCalendarId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Subdivision)
                    .WithMany()
                    .HasForeignKey(x => x.SubdivisionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ================================================================
            // Sprint 13.5 PRA-4 — UOM master config.
            //
            // Partial UNIQUE indexes per the Replit prod-validator convention
            // (PR #5c.1.1 lesson: NO COALESCE-in-index — use WHERE filters).
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.UomCategory>(e =>
            {
                // Two partial UNIQUEs on Code (system vs tenant).
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_uom_categories_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_uom_categories_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.CompanyId)
                    .HasDatabaseName("ix_uom_categories_company")
                    .HasFilter("\"CompanyId\" IS NOT NULL");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.UnitOfMeasureMaster>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_units_of_measure_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_units_of_measure_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.UomCategoryId)
                    .HasDatabaseName("ix_units_of_measure_category");
                e.HasIndex(x => x.UneceCode)
                    .HasDatabaseName("ix_units_of_measure_unece")
                    .HasFilter("\"UneceCode\" IS NOT NULL");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.UomConversion>(e =>
            {
                // (Company, From, To, ItemId-or-0) unique. Partial UNIQUEs for
                // company-wide vs per-item overrides.
                e.HasIndex(x => new { x.CompanyId, x.FromUomId, x.ToUomId })
                    .HasDatabaseName("ix_uom_conversions_company_wide")
                    .HasFilter("\"ItemId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.FromUomId, x.ToUomId, x.ItemId })
                    .HasDatabaseName("ix_uom_conversions_per_item")
                    .HasFilter("\"ItemId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.ItemId)
                    .HasDatabaseName("ix_uom_conversions_item")
                    .HasFilter("\"ItemId\" IS NOT NULL");
            });

            // ================================================================
            // Sprint 13.5 PRA-6 — Currency / PaymentTerm / TaxAuthority /
            // TaxCode masters. Partial UNIQUE indexes (NO COALESCE-in-index
            // per the Replit prod-validator lesson).
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.CurrencyMaster>(e =>
            {
                e.HasIndex(x => x.IsoCode)
                    .HasDatabaseName("ix_currency_masters_system_iso")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.IsoCode })
                    .HasDatabaseName("ix_currency_masters_company_iso")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.PaymentTermMaster>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_payment_term_masters_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_payment_term_masters_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.CurrencyId)
                    .HasDatabaseName("ix_payment_term_masters_currency")
                    .HasFilter("\"CurrencyId\" IS NOT NULL");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.TaxAuthority>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_tax_authorities_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_tax_authorities_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.CountryCode)
                    .HasDatabaseName("ix_tax_authorities_country");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.TaxCodeMaster>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_tax_code_masters_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_tax_code_masters_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.TaxAuthorityId)
                    .HasDatabaseName("ix_tax_code_masters_authority")
                    .HasFilter("\"TaxAuthorityId\" IS NOT NULL");

                e.HasOne(x => x.TaxAuthority)
                    .WithMany()
                    .HasForeignKey(x => x.TaxAuthorityId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ================================================================
            // Sprint 13.5 PRA-7 — Warehouse + Bin + Lot + Serial + ItemGroup
            // + PostingProfile. Partial UNIQUE indexes per the cross-tenant
            // CompanyId NULL pattern. No COALESCE-in-index (PR #5c.1.1 lesson).
            // See docs/ADR-019-wms-posting-profile-pattern.md.
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.WarehouseMaster>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_warehouse_masters_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_warehouse_masters_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("ix_warehouse_masters_site")
                    .HasFilter("\"SiteId\" IS NOT NULL");
                e.HasIndex(x => x.WarehouseType)
                    .HasDatabaseName("ix_warehouse_masters_type");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.BinMaster>(e =>
            {
                e.HasIndex(x => new { x.WarehouseId, x.Code })
                    .HasDatabaseName("ix_bin_masters_system_warehouse_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.WarehouseId, x.Code })
                    .HasDatabaseName("ix_bin_masters_company_warehouse_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.WarehouseId)
                    .HasDatabaseName("ix_bin_masters_warehouse");

                e.HasOne(x => x.Warehouse)
                    .WithMany()
                    .HasForeignKey(x => x.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.LotMaster>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.ItemId, x.LotNumber })
                    .HasDatabaseName("ix_lot_masters_company_item_lot")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.ItemId })
                    .HasDatabaseName("ix_lot_masters_company_item");
                e.HasIndex(x => x.ExpiryDate)
                    .HasDatabaseName("ix_lot_masters_expiry")
                    .HasFilter("\"ExpiryDate\" IS NOT NULL");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("ix_lot_masters_status");
                e.HasIndex(x => x.ParentLotId)
                    .HasDatabaseName("ix_lot_masters_parent")
                    .HasFilter("\"ParentLotId\" IS NOT NULL");

                e.HasOne(x => x.ParentLot)
                    .WithMany()
                    .HasForeignKey(x => x.ParentLotId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.SerialMaster>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.ItemId, x.SerialNumber })
                    .HasDatabaseName("ix_serial_masters_company_item_serial")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.ItemId })
                    .HasDatabaseName("ix_serial_masters_company_item");
                e.HasIndex(x => x.LotId)
                    .HasDatabaseName("ix_serial_masters_lot")
                    .HasFilter("\"LotId\" IS NOT NULL");
                e.HasIndex(x => x.CurrentWarehouseId)
                    .HasDatabaseName("ix_serial_masters_current_warehouse")
                    .HasFilter("\"CurrentWarehouseId\" IS NOT NULL");
                e.HasIndex(x => x.CurrentBinId)
                    .HasDatabaseName("ix_serial_masters_current_bin")
                    .HasFilter("\"CurrentBinId\" IS NOT NULL");
                e.HasIndex(x => x.LifecycleStatus)
                    .HasDatabaseName("ix_serial_masters_lifecycle");
                e.HasIndex(x => x.AssetId)
                    .HasDatabaseName("ix_serial_masters_asset")
                    .HasFilter("\"AssetId\" IS NOT NULL");

                e.HasOne(x => x.Lot)
                    .WithMany()
                    .HasForeignKey(x => x.LotId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CurrentWarehouse)
                    .WithMany()
                    .HasForeignKey(x => x.CurrentWarehouseId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CurrentBin)
                    .WithMany()
                    .HasForeignKey(x => x.CurrentBinId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.ItemGroup>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_item_groups_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_item_groups_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.GroupType)
                    .HasDatabaseName("ix_item_groups_type");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.PostingProfile>(e =>
            {
                // Composite UNIQUE across the resolution tuple. PostgreSQL
                // partial UNIQUEs with NULLs need explicit COALESCE in the
                // expression — we avoid that (PR #5c.1.1 quoting lesson) and
                // ship four partial indexes for the four NULL combos.
                e.HasIndex(x => new { x.ItemGroupId, x.TransactionType, x.WarehouseId })
                    .HasDatabaseName("ix_posting_profiles_system_full")
                    .HasFilter("\"CompanyId\" IS NULL AND \"WarehouseId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.ItemGroupId, x.TransactionType })
                    .HasDatabaseName("ix_posting_profiles_system_nowh")
                    .HasFilter("\"CompanyId\" IS NULL AND \"WarehouseId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.ItemGroupId, x.TransactionType, x.WarehouseId })
                    .HasDatabaseName("ix_posting_profiles_company_full")
                    .HasFilter("\"CompanyId\" IS NOT NULL AND \"WarehouseId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.ItemGroupId, x.TransactionType })
                    .HasDatabaseName("ix_posting_profiles_company_nowh")
                    .HasFilter("\"CompanyId\" IS NOT NULL AND \"WarehouseId\" IS NULL")
                    .IsUnique();

                e.HasIndex(x => x.WarehouseId)
                    .HasDatabaseName("ix_posting_profiles_warehouse")
                    .HasFilter("\"WarehouseId\" IS NOT NULL");

                e.HasOne(x => x.ItemGroup)
                    .WithMany()
                    .HasForeignKey(x => x.ItemGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ON DELETE RESTRICT — SetNull would collide with the partial
                // UNIQUE indexes on (CompanyId, ItemGroupId, TransactionType)
                // WHERE WarehouseId IS NULL. Nulling a warehouse-specific
                // override would create a duplicate of the fallback key and
                // fail the delete with a unique-constraint error. RESTRICT
                // forces tenant admins to explicitly delete or reassign
                // posting overrides before deleting a warehouse — the right
                // semantic. (Codex review catch on PR #317.)
                e.HasOne(x => x.Warehouse)
                    .WithMany()
                    .HasForeignKey(x => x.WarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ================================================================
            // Sprint 13.5 PRA-8 — Employee + WageGroup + LaborRateMaster.
            // Tenant-owned operational data (CompanyId NOT NULL on Employee
            // and LaborRateMaster); WageGroup carries system templates.
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.Employee>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.EmployeeNumber })
                    .HasDatabaseName("ix_employees_company_employeenumber")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.LastName, x.FirstName })
                    .HasDatabaseName("ix_employees_company_name");
                e.HasIndex(x => x.DepartmentId)
                    .HasDatabaseName("ix_employees_department")
                    .HasFilter("\"DepartmentId\" IS NOT NULL");
                e.HasIndex(x => x.ManagerId)
                    .HasDatabaseName("ix_employees_manager")
                    .HasFilter("\"ManagerId\" IS NOT NULL");
                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("ix_employees_site")
                    .HasFilter("\"SiteId\" IS NOT NULL");
                e.HasIndex(x => x.DefaultWageGroupId)
                    .HasDatabaseName("ix_employees_wagegroup")
                    .HasFilter("\"DefaultWageGroupId\" IS NOT NULL");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("ix_employees_status");

                // Self-ref manager — RESTRICT forces explicit reassignment.
                e.HasOne(x => x.Manager)
                    .WithMany()
                    .HasForeignKey(x => x.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.DefaultWageGroup)
                    .WithMany()
                    .HasForeignKey(x => x.DefaultWageGroupId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.WageGroup>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_wage_groups_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_wage_groups_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.GroupType)
                    .HasDatabaseName("ix_wage_groups_type");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.LaborRateMaster>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_labor_rate_masters_company_employee_effective")
                    .HasFilter("\"EmployeeId\" IS NOT NULL");
                e.HasIndex(x => new { x.CompanyId, x.WageGroupId, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_labor_rate_masters_company_wagegroup_effective")
                    .HasFilter("\"EmployeeId\" IS NULL");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_labor_rate_masters_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");

                e.HasOne(x => x.Employee)
                    .WithMany()
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.WageGroup)
                    .WithMany()
                    .HasForeignKey(x => x.WageGroupId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ================================================================
            // Sprint 13.5 PRA-9 — PriceListMaster + PriceListLine
            // + DiscountSchema + RebateAgreement. Customer-facing pricing
            // substrate. Lays the ground for ADR-027 (SalesOrder lines that
            // reference PriceListLineId for price provenance + DiscountSchemaId
            // for discount provenance).
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.PriceListMaster>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_price_list_masters_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_price_list_masters_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.CustomerId)
                    .HasDatabaseName("ix_price_list_masters_customer")
                    .HasFilter("\"CustomerId\" IS NOT NULL");
                e.HasIndex(x => x.CustomerTier)
                    .HasDatabaseName("ix_price_list_masters_tier")
                    .HasFilter("\"CustomerTier\" IS NOT NULL");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_price_list_masters_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");

                e.HasOne(x => x.Currency)
                    .WithMany()
                    .HasForeignKey(x => x.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.PriceListLine>(e =>
            {
                e.HasIndex(x => new { x.PriceListMasterId, x.ItemId, x.UomId, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_price_list_lines_list_item_uom_effective");
                e.HasIndex(x => x.ItemId)
                    .HasDatabaseName("ix_price_list_lines_item");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_price_list_lines_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");

                e.HasOne(x => x.PriceListMaster)
                    .WithMany()
                    .HasForeignKey(x => x.PriceListMasterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.DiscountSchema>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_discount_schemas_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_discount_schemas_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.AppliesToScope)
                    .HasDatabaseName("ix_discount_schemas_scope");
                e.HasIndex(x => new { x.AppliesToScope, x.AppliesToEntityId })
                    .HasDatabaseName("ix_discount_schemas_scope_entity")
                    .HasFilter("\"AppliesToEntityId\" IS NOT NULL");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_discount_schemas_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.RebateAgreement>(e =>
            {
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_rebate_agreements_company_code")
                    .IsUnique();
                e.HasIndex(x => x.CustomerId)
                    .HasDatabaseName("ix_rebate_agreements_customer");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("ix_rebate_agreements_status");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_rebate_agreements_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");
            });

            // ================================================================
            // Sprint 13.5 PRA-10 — TaxRateMaster (effective-dated tax rates).
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.TaxRateMaster>(e =>
            {
                // (CompanyId IS NULL, Code, EffectiveFromUtc) UNIQUE for
                // system templates — same code can recur across effective
                // periods (rate changes over time).
                e.HasIndex(x => new { x.Code, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_tax_rate_masters_system_code_effective")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_tax_rate_masters_company_code_effective")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.TaxCodeMasterId)
                    .HasDatabaseName("ix_tax_rate_masters_taxcode");
                e.HasIndex(x => new { x.CountryCode, x.SubdivisionCode, x.EffectiveFromUtc })
                    .HasDatabaseName("ix_tax_rate_masters_jurisdiction_effective");
                e.HasIndex(x => x.EffectiveToUtc)
                    .HasDatabaseName("ix_tax_rate_masters_effective_to")
                    .HasFilter("\"EffectiveToUtc\" IS NULL");
                e.HasIndex(x => x.AppliesToItemGroupId)
                    .HasDatabaseName("ix_tax_rate_masters_itemgroup")
                    .HasFilter("\"AppliesToItemGroupId\" IS NOT NULL");

                e.HasOne(x => x.TaxCodeMaster)
                    .WithMany()
                    .HasForeignKey(x => x.TaxCodeMasterId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.TaxAuthority)
                    .WithMany()
                    .HasForeignKey(x => x.TaxAuthorityId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ================================================================
            // Sprint 13.5 PRA-11 — PackLevel + ItemPackHierarchy.
            // ================================================================
            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.PackLevel>(e =>
            {
                e.HasIndex(x => x.Code)
                    .HasDatabaseName("ix_pack_levels_system_code")
                    .HasFilter("\"CompanyId\" IS NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.Code })
                    .HasDatabaseName("ix_pack_levels_company_code")
                    .HasFilter("\"CompanyId\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.LevelOrder)
                    .HasDatabaseName("ix_pack_levels_order");
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Masters.ItemPackHierarchy>(e =>
            {
                // Composite scan index (non-unique).
                e.HasIndex(x => new { x.CompanyId, x.ItemId, x.PackLevelId })
                    .HasDatabaseName("ix_item_pack_hierarchies_company_item_level");

                // Codex P1 catch on PR #321: two partial UNIQUEs cover
                // duplicate-prevention with-barcode and without-barcode.
                e.HasIndex(x => new { x.CompanyId, x.ItemId, x.PackLevelId, x.Gtin })
                    .HasDatabaseName("ix_item_pack_hierarchies_company_item_level_gtin")
                    .HasFilter("\"Gtin\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => new { x.CompanyId, x.ItemId, x.PackLevelId })
                    .HasDatabaseName("ix_item_pack_hierarchies_company_item_level_nogtin")
                    .HasFilter("\"Gtin\" IS NULL")
                    .IsUnique();

                // Barcode-scanner lookup path — UNIQUE when present.
                e.HasIndex(x => x.Gtin)
                    .HasDatabaseName("ix_item_pack_hierarchies_gtin")
                    .HasFilter("\"Gtin\" IS NOT NULL")
                    .IsUnique();
                e.HasIndex(x => x.ItemId)
                    .HasDatabaseName("ix_item_pack_hierarchies_item");

                e.HasOne(x => x.PackLevel)
                    .WithMany()
                    .HasForeignKey(x => x.PackLevelId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ──────────────────────────────────────────────────────────────
            // Sprint 15.1 PR-2 — ProductionSupplyDemand + ProductionSupplyAllocation
            // ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionSupplyDemand>(e =>
            {
                // Enum defaults per HARD LOCK feedback_b6_enum_defaults_must_match_model.md
                e.Property(x => x.SourceType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandSourceType.BomLine);
                e.Property(x => x.SupplyPolicy)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SupplyPolicy.BuyDirectToJob);
                e.Property(x => x.SourceStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandSourceStatus.NotDetermined);
                e.Property(x => x.SupplyStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandSupplyStatus.NotSupplied);
                e.Property(x => x.ShortageStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandShortageStatus.NoShortage);
                e.Property(x => x.CostStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandCostStatus.NotCommitted);
                e.Property(x => x.AlertStatus)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.DemandAlertStatus.None);
                // Sprint 15.3 PR-10 — buyer-workflow state machine default
                e.Property(x => x.BuyerActionState)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.BuyerActionState.Open);

                // Codex P1 fix: scope demand-number uniqueness by tenant. PRO
                // OrderNumber is only unique per company, so a global unique
                // index would block two tenants who happen to use the same
                // production order number.
                e.HasIndex(x => new { x.CompanyId, x.DemandNumber }).IsUnique();
                e.HasIndex(x => new { x.ProductionOrderId, x.BomLineId });
                e.HasIndex(x => x.SupplyStatus);
                e.HasIndex(x => x.ShortageStatus);
                e.HasIndex(x => x.AlertStatus);
                // Sprint 15.3 PR-10 — buyer-workflow queue indexing
                e.HasIndex(x => x.BuyerActionState);
                e.HasIndex(x => new { x.CompanyId, x.BuyerUserId, x.BuyerActionState });

                e.HasOne(x => x.ProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.BomLine)
                    .WithMany()
                    .HasForeignKey(x => x.BomLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ParentDemand)
                    .WithMany()
                    .HasForeignKey(x => x.ParentDemandId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Project)
                    .WithMany()
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.BuyerUser)
                    .WithMany()
                    .HasForeignKey(x => x.BuyerUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PlannerUser)
                    .WithMany()
                    .HasForeignKey(x => x.PlannerUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WorkCenter)
                    .WithMany()
                    .HasForeignKey(x => x.WorkCenterId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Warehouse)
                    .WithMany()
                    .HasForeignKey(x => x.WarehouseId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.BinLocation)
                    .WithMany()
                    .HasForeignKey(x => x.BinLocationId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.LinkedPurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedPurchaseOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedPurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedPurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedChildProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedChildProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedGoodsReceipt)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedGoodsReceiptId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedVendorInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.LinkedVendorInvoiceId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionSupplyAllocation>(e =>
            {
                e.Property(x => x.SupplyType)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.AllocationSupplyType.PurchaseOrderLine);
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.AllocationStatus.Proposed);

                e.HasIndex(x => x.ProductionSupplyDemandId);
                e.HasIndex(x => new { x.SupplyType, x.SupplyRecordId, x.SupplyRecordLineId });
                e.HasIndex(x => x.Status);

                e.HasOne(x => x.ProductionSupplyDemand)
                    .WithMany(d => d.Allocations)
                    .HasForeignKey(x => x.ProductionSupplyDemandId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ChildProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ChildProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ──────────────────────────────────────────────────────────────
            // Sprint 15.1 PR-3 — PurchaseOrderLine PRO/demand expansion + link
            // ──────────────────────────────────────────────────────────────

            // Add the new FKs on PurchaseOrderLine itself.
            modelBuilder.Entity<PurchaseOrderLine>(e =>
            {
                e.HasOne(x => x.ProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.BomLine)
                    .WithMany()
                    .HasForeignKey(x => x.BomLineId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ──────────────────────────────────────────────────────────────
            // Sprint 15.1 PR-4 — SubcontractOperation + SubcontractDemand
            // ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractOperation>(e =>
            {
                e.Property(x => x.Status).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractOperationStatus.NotReady);
                e.Property(x => x.PoCreationStatus).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractPoCreationStatus.NotCreated);
                e.Property(x => x.ShipmentStatus).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractShipmentStatus.NotShipped);
                e.Property(x => x.ReceiptStatus).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractReceiptStatus.NotReceived);
                e.Property(x => x.CostMethod).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractCostMethod.FixedPriceFromPo);
                e.Property(x => x.FreightResponsibility).HasDefaultValue(Abs.FixedAssets.Models.Production.FreightResponsibility.Us);

                e.HasIndex(x => new { x.CompanyId, x.ProductionOrderId, x.OperationSequence }).IsUnique();
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.SupplierId);

                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.WipItem).WithMany().HasForeignKey(x => x.WipItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorWipWarehouse).WithMany().HasForeignKey(x => x.VendorWipWarehouseId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ShipFromLocation).WithMany().HasForeignKey(x => x.ShipFromLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ReturnToLocation).WithMany().HasForeignKey(x => x.ReturnToLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ServicePurchaseOrderLine).WithMany().HasForeignKey(x => x.ServicePurchaseOrderLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractDemand>(e =>
            {
                e.Property(x => x.Status).HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractDemandStatus.Open);

                e.HasIndex(x => x.SubcontractOperationId).IsUnique();
                e.HasIndex(x => x.ProductionOrderId);
                e.HasIndex(x => x.Status);

                e.HasOne(x => x.SubcontractOperation).WithMany(s => s.Demands)
                    .HasForeignKey(x => x.SubcontractOperationId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ServicePurchaseDemand).WithMany().HasForeignKey(x => x.ServicePurchaseDemandId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WipMovementDemand).WithMany().HasForeignKey(x => x.WipMovementDemandId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.WipItem).WithMany().HasForeignKey(x => x.WipItemId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ──────────────────────────────────────────────────────────────
            // Sprint 15.1 PR-5 — Vendor WIP tracking (3 entities)
            // ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<VendorLocation>(e =>
            {
                e.Property(x => x.LocationType).HasDefaultValue(VendorLocationType.ProcessingPlant);

                e.HasIndex(x => new { x.CompanyId, x.SupplierId, x.LocationCode }).IsUnique();
                e.HasIndex(x => x.SupplierId);

                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.LinkedWarehouse).WithMany().HasForeignKey(x => x.LinkedWarehouseId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.LinkedBinLocation).WithMany().HasForeignKey(x => x.LinkedBinLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.DefaultReceivingLocation).WithMany().HasForeignKey(x => x.DefaultReceivingLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<VendorWipBalance>(e =>
            {
                e.Property(x => x.InventoryStatus).HasDefaultValue(VendorWipInventoryStatus.InTransitToVendor);
                e.Property(x => x.Ownership).HasDefaultValue(VendorWipOwnership.Us);
                e.Property(x => x.ValuationStatus).HasDefaultValue(VendorWipValuationStatus.Valued);
                e.Property(x => x.QualityStatus).HasDefaultValue(VendorWipQualityStatus.Unknown);

                e.HasIndex(x => new { x.ProductionOrderId, x.OperationSequence, x.SupplierId });
                e.HasIndex(x => x.SupplierId);
                e.HasIndex(x => x.InventoryStatus);

                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorLocation).WithMany(l => l.Balances).HasForeignKey(x => x.VendorLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.VendorWipWarehouse).WithMany().HasForeignKey(x => x.VendorWipWarehouseId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.SubcontractOperation).WithMany().HasForeignKey(x => x.SubcontractOperationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<VendorWipTransaction>(e =>
            {
                e.Property(x => x.TransactionType).HasDefaultValue(VendorWipTransactionType.ShipToVendor);

                e.HasIndex(x => new { x.CompanyId, x.TransactionNumber }).IsUnique();
                e.HasIndex(x => x.VendorWipBalanceId);
                e.HasIndex(x => new { x.ProductionOrderId, x.OperationSequence });
                e.HasIndex(x => x.TransactionType);

                e.HasOne(x => x.VendorWipBalance).WithMany(b => b.Transactions).HasForeignKey(x => x.VendorWipBalanceId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.SubcontractOperation).WithMany().HasForeignKey(x => x.SubcontractOperationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.VendorLocation).WithMany().HasForeignKey(x => x.VendorLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.PurchaseOrderLine).WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ReverseOfTransaction).WithMany().HasForeignKey(x => x.ReverseOfTransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<PurchaseOrderLineDemandLink>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(PoDemandLinkStatus.Proposed);

                e.HasIndex(x => x.PurchaseOrderLineId);
                e.HasIndex(x => x.ProductionSupplyDemandId);
                e.HasIndex(x => x.ProductionOrderId);
                e.HasIndex(x => x.Status);
                // Tenant-scoped uniqueness: same PO line + demand + release tuple,
                // active or not — duplicate guard. Allow re-creation after release
                // by including Status implicitly via service-layer idempotency.
                e.HasIndex(x => new { x.PurchaseOrderLineId, x.ProductionSupplyDemandId, x.PurchaseOrderReleaseId, x.Status });

                e.HasOne(x => x.PurchaseOrderLine)
                    .WithMany(p => p.DemandLinks)
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PurchaseOrderRelease)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderReleaseId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProductionSupplyDemand)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionSupplyDemandId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.ProductionOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.BomLine)
                    .WithMany()
                    .HasForeignKey(x => x.BomLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ──────────────────────────────────────────────────────────────
            // Sprint 15.2 PR-6 — SubcontractShipment + SubcontractReceipt
            //   Headers + lines for physical WIP shipment to vendor and
            //   receive-back. 10 §11 receipt scenarios captured per receipt
            //   line via the Scenario + Disposition enums.
            // ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractShipment>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractShipmentLifecycle.Draft);

                e.HasIndex(x => new { x.CompanyId, x.ShipmentNumber }).IsUnique();
                e.HasIndex(x => x.SubcontractOperationId);
                e.HasIndex(x => new { x.ProductionOrderId, x.OperationSequence });
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.SupplierId);

                e.HasOne(x => x.SubcontractOperation).WithMany().HasForeignKey(x => x.SubcontractOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.SubcontractDemand).WithMany().HasForeignKey(x => x.SubcontractDemandId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ServicePurchaseOrderLine).WithMany().HasForeignKey(x => x.ServicePurchaseOrderLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorLocation).WithMany().HasForeignKey(x => x.VendorLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ShipFromLocation).WithMany().HasForeignKey(x => x.ShipFromLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractShipmentLine>(e =>
            {
                e.HasIndex(x => x.SubcontractShipmentId);
                e.HasIndex(x => new { x.SubcontractShipmentId, x.LineNumber }).IsUnique();
                e.HasIndex(x => x.ItemId);

                e.HasOne(x => x.SubcontractShipment).WithMany(s => s.Lines)
                    .HasForeignKey(x => x.SubcontractShipmentId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorWipTransaction).WithMany().HasForeignKey(x => x.VendorWipTransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractReceipt>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractReceiptLifecycle.Draft);

                e.HasIndex(x => new { x.CompanyId, x.ReceiptNumber }).IsUnique();
                e.HasIndex(x => x.SubcontractOperationId);
                e.HasIndex(x => new { x.ProductionOrderId, x.OperationSequence });
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.SupplierId);

                e.HasOne(x => x.SubcontractOperation).WithMany().HasForeignKey(x => x.SubcontractOperationId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.SubcontractShipment).WithMany().HasForeignKey(x => x.SubcontractShipmentId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ServicePurchaseOrderLine).WithMany().HasForeignKey(x => x.ServicePurchaseOrderLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorLocation).WithMany().HasForeignKey(x => x.VendorLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ReceivingLocation).WithMany().HasForeignKey(x => x.ReceivingLocationId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<Abs.FixedAssets.Models.Production.SubcontractReceiptLine>(e =>
            {
                e.Property(x => x.Scenario)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractReceiptScenario.FullGoodReceipt);
                e.Property(x => x.Disposition)
                    .HasDefaultValue(Abs.FixedAssets.Models.Production.SubcontractReceiptDisposition.ReleaseToNextOp);

                e.HasIndex(x => x.SubcontractReceiptId);
                e.HasIndex(x => new { x.SubcontractReceiptId, x.LineNumber }).IsUnique();
                e.HasIndex(x => x.ItemId);
                e.HasIndex(x => x.Scenario);

                e.HasOne(x => x.SubcontractReceipt).WithMany(r => r.Lines)
                    .HasForeignKey(x => x.SubcontractReceiptId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SubcontractShipmentLine).WithMany().HasForeignKey(x => x.SubcontractShipmentLineId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.VendorWipTransaction).WithMany().HasForeignKey(x => x.VendorWipTransactionId).OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ─── Sprint 15.4 PR-16 — POAcknowledgment + POAcknowledgmentLine ────
            // Vendor confirmation of Purchase Orders. One IsCurrent ack per PO at
            // a time; history preserved for PR-17 vendor re-acknowledgment loop.
            modelBuilder.Entity<POAcknowledgment>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(POAcknowledgmentStatus.Requested);
                e.Property(x => x.Method)
                    .HasDefaultValue(POAcknowledgmentMethod.VendorPortal);
                e.Property(x => x.IsCurrent).HasDefaultValue(true);
                e.Property(x => x.AllLinesAcceptedAsOrdered).HasDefaultValue(false);

                // Tenant-unique acknowledgment number (POACK-YYYY-NNNNNN).
                // Two-phase numbering pattern means the placeholder Guid lives
                // here briefly during the first SaveChanges — the unique index
                // still holds because Guids never collide.
                // P2-5: filter on CompanyId IS NOT NULL since Postgres treats
                // NULLs as distinct in unique indexes (a null-CompanyId ack is
                // service-rejected anyway, but defensive on the schema).
                e.HasIndex(x => new { x.CompanyId, x.AcknowledgmentNumber })
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NOT NULL");
                e.HasIndex(x => x.PurchaseOrderId);

                // Codex P2 (PRRT_kwDOSSj3Wc6Fg48N): enforce the "one IsCurrent
                // ack per PO" invariant at the DB level. Two concurrent
                // RequestAcknowledgmentAsync calls would otherwise race and
                // both insert IsCurrent=true. Filtered unique index makes the
                // second commit fail with a 23505 unique violation, which the
                // service can surface to the caller for retry.
                e.HasIndex(x => x.PurchaseOrderId)
                    .HasDatabaseName("UX_POAcknowledgments_PurchaseOrderId_IsCurrent")
                    .IsUnique()
                    .HasFilter("\"IsCurrent\" = TRUE");
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ResponseDueByUtc);

                e.HasOne(x => x.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.RequestedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.RequestedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<POAcknowledgmentLine>(e =>
            {
                e.Property(x => x.ExceptionType)
                    .HasDefaultValue(PoAckLineExceptionType.None);
                e.Property(x => x.IsAccepted).HasDefaultValue(false);
                e.Property(x => x.ExceptionApproved).HasDefaultValue(false);

                e.HasIndex(x => x.POAcknowledgmentId);
                e.HasIndex(x => x.PurchaseOrderLineId);
                e.HasIndex(x => x.ExceptionType);
                e.HasIndex(x => new { x.POAcknowledgmentId, x.PurchaseOrderLineId })
                    .IsUnique();

                e.HasOne(x => x.POAcknowledgment)
                    .WithMany(a => a.Lines)
                    .HasForeignKey(x => x.POAcknowledgmentId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ExceptionApprovedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ExceptionApprovedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ─── Sprint 15.4 PR-17 — POChangeHistory + POChangeHistoryLine ──────
            // PO Amendment / Change Order entity. Header + per-line snapshot;
            // demand-link impact preview cached on the header; vendor re-ack
            // loop coupled via IPoAcknowledgmentService.
            modelBuilder.Entity<POChangeHistory>(e =>
            {
                e.Property(x => x.Status)
                    .HasDefaultValue(POAmendmentStatus.Draft);
                e.Property(x => x.Reason)
                    .HasDefaultValue(POChangeReason.BuyerRequested);
                e.Property(x => x.IsCurrent).HasDefaultValue(true);
                e.Property(x => x.VendorReAcknowledgmentRequired).HasDefaultValue(true);

                // Tenant-unique amendment number (POAMD-YYYY-NNNNNN).
                e.HasIndex(x => new { x.CompanyId, x.AmendmentNumber })
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NOT NULL");
                e.HasIndex(x => x.PurchaseOrderId);

                // One IsCurrent amendment per PO at the DB level
                // (mirrors the PR-16 Codex P2 fix pattern).
                e.HasIndex(x => x.PurchaseOrderId)
                    .HasDatabaseName("UX_POChangeHistories_PurchaseOrderId_IsCurrent")
                    .IsUnique()
                    .HasFilter("\"IsCurrent\" = TRUE");
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.Reason);

                e.HasOne(x => x.PurchaseOrder)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.DraftedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.DraftedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.ApprovedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<POChangeHistoryLine>(e =>
            {
                e.Property(x => x.ChangeType)
                    .HasDefaultValue(POAmendmentLineChangeType.Unchanged);

                e.HasIndex(x => x.POChangeHistoryId);
                e.HasIndex(x => x.PurchaseOrderLineId);
                e.HasIndex(x => x.ChangeType);

                e.HasOne(x => x.POChangeHistory)
                    .WithMany(a => a.Lines)
                    .HasForeignKey(x => x.POChangeHistoryId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.PurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ─── Sprint 15.4 PR-18 — SupplierPerformance scorecard snapshot ─────
            // Computed OTD % / quality PPM / price variance % / NCR count per
            // (Vendor, PeriodType) rolling window. One IsCurrent snapshot per
            // pair (filtered unique index); history preserved. Feeds §21 tab 13
            // and the PR-20 quote ranker.
            modelBuilder.Entity<SupplierPerformance>(e =>
            {
                // NB: PeriodType deliberately has NO HasDefaultValue. It is a
                // required discriminator dimension always set explicitly by the
                // service. A DB default would make EF treat the CLR sentinel
                // (Rolling30Days = 0) as "unset" and silently rewrite a
                // Rolling30Days recompute to the default — a data-corruption bug.
                e.Property(x => x.IsCurrent).HasDefaultValue(true);

                e.HasIndex(x => x.VendorId);
                e.HasIndex(x => new { x.VendorId, x.PeriodType });

                // One IsCurrent snapshot per (Vendor, PeriodType) at the DB
                // level — two concurrent RecomputeAsync calls for the same pair
                // would otherwise both insert IsCurrent=true. The filtered
                // unique index makes the second commit fail 23505, which the
                // service surfaces as a retry message (mirrors PR-16 pattern).
                e.HasIndex(x => new { x.VendorId, x.PeriodType })
                    .HasDatabaseName("UX_SupplierPerformances_Vendor_Period_IsCurrent")
                    .IsUnique()
                    .HasFilter("\"IsCurrent\" = TRUE");

                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            // ─── Sprint 15.4 PR-19 — InvoiceMatchResult + InvoiceMatchResultLine ─
            // Persisted 3-way match runs. One IsCurrent result per invoice
            // (filtered unique index); concurrent re-run loser hits 23505.
            modelBuilder.Entity<InvoiceMatchResult>(e =>
            {
                e.Property(x => x.Outcome).HasDefaultValue(InvoiceMatchOutcome.NotMatched);
                e.Property(x => x.IsCurrent).HasDefaultValue(true);
                e.Property(x => x.PostedOnMatch).HasDefaultValue(false);

                // Tenant-unique run number IM-YYYY-NNNNNN (two-phase numbered).
                e.HasIndex(x => new { x.CompanyId, x.MatchRunNumber })
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NOT NULL");
                e.HasIndex(x => x.VendorInvoiceId);
                e.HasIndex(x => x.Outcome);

                // One IsCurrent result per invoice at the DB level.
                e.HasIndex(x => x.VendorInvoiceId)
                    .HasDatabaseName("UX_InvoiceMatchResults_VendorInvoiceId_IsCurrent")
                    .IsUnique()
                    .HasFilter("\"IsCurrent\" = TRUE");

                e.HasOne(x => x.VendorInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.VendorInvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<InvoiceMatchResultLine>(e =>
            {
                e.Property(x => x.Outcome).HasDefaultValue(InvoiceMatchLineOutcome.Unlinked);

                e.HasIndex(x => x.InvoiceMatchResultId);
                e.HasIndex(x => x.VendorInvoiceLineId);
                e.HasIndex(x => x.Outcome);

                e.HasOne(x => x.InvoiceMatchResult)
                    .WithMany(r => r.Lines)
                    .HasForeignKey(x => x.InvoiceMatchResultId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.VendorInvoiceLine)
                    .WithMany()
                    .HasForeignKey(x => x.VendorInvoiceLineId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.PurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(x => x.GoodsReceiptLine)
                    .WithMany()
                    .HasForeignKey(x => x.GoodsReceiptLineId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ─── Sprint 15.4 PR-20 — RFQ / Quote Flow ───────────────────────────
            modelBuilder.Entity<SupplierRFQ>(e =>
            {
                e.Property(x => x.Status).HasDefaultValue(RfqStatus.Draft);

                // Tenant-unique RFQ number (two-phase numbered).
                e.HasIndex(x => new { x.CompanyId, x.RfqNumber })
                    .IsUnique()
                    .HasFilter("\"CompanyId\" IS NOT NULL");
                e.HasIndex(x => x.Status);

                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<SupplierRFQLine>(e =>
            {
                e.HasIndex(x => x.SupplierRFQId);
                e.HasOne(x => x.SupplierRFQ)
                    .WithMany(r => r.Lines)
                    .HasForeignKey(x => x.SupplierRFQId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Item)
                    .WithMany()
                    .HasForeignKey(x => x.ItemId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SupplierQuote>(e =>
            {
                e.Property(x => x.Status).HasDefaultValue(SupplierQuoteStatus.Invited);
                e.Property(x => x.IsWinner).HasDefaultValue(false);

                e.HasIndex(x => x.SupplierRFQId);
                e.HasIndex(x => x.VendorId);
                e.HasIndex(x => x.Status);
                // One quote per (RFQ, vendor).
                e.HasIndex(x => new { x.SupplierRFQId, x.VendorId }).IsUnique();

                e.HasOne(x => x.SupplierRFQ)
                    .WithMany(r => r.Quotes)
                    .HasForeignKey(x => x.SupplierRFQId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Company)
                    .WithMany()
                    .HasForeignKey(x => x.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.MapXminRowVersion(x => x.RowVersion);
            });

            modelBuilder.Entity<SupplierQuoteLine>(e =>
            {
                e.HasIndex(x => x.SupplierQuoteId);
                e.HasIndex(x => x.SupplierRFQLineId);
                e.HasOne(x => x.SupplierQuote)
                    .WithMany(q => q.Lines)
                    .HasForeignKey(x => x.SupplierQuoteId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SupplierRFQLine)
                    .WithMany()
                    .HasForeignKey(x => x.SupplierRFQLineId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        public override int SaveChanges()
        {
            CapitalizeStringProperties();
            EnforceJournalEntryBalanceOnInsert();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            CapitalizeStringProperties();
            EnforceJournalEntryBalanceOnInsert();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Best-In-Class persistence-boundary guard for journal-entry integrity.
        /// Refuses to save any newly-inserted JournalEntry whose Lines do not
        /// balance (Σdebits ≠ Σcredits). Surfaced by PR #82 verification of
        /// DEF-N02 — the system displayed "UNBALANCED" in the JE header but
        /// the write went through anyway, allowing $200 variance per disposal
        /// to leak into the GL silently.
        ///
        /// Scope: ADDED entries only. Existing-entry updates aren't validated
        /// here because reliably summing all current lines requires lazy-loading
        /// the full Lines navigation, which would change the perf profile of
        /// every SaveChanges. In the current codebase, JE lines are never
        /// updated in place — every posting event creates a new JournalEntry
        /// (matched by Source + Reference), and reversals go via a fresh
        /// contra-entry (see ApPostingService.PostVoidAsync). If that
        /// invariant ever changes, extend this method.
        ///
        /// Tolerance: strict decimal equality. .NET decimal is base-10, so
        /// debit/credit sums computed from the same line set don't drift.
        /// Any non-zero variance is a real bug to surface.
        ///
        /// Industrial baseline: SAP, Oracle EBS, Maximo all enforce this at
        /// the persistence boundary (usually via a DB-side trigger). We do it
        /// in EF SaveChanges so the user gets a clean InvalidOperationException
        /// in the same request rather than a generic DB error from a trigger.
        /// </summary>
        private void EnforceJournalEntryBalanceOnInsert()
        {
            var newEntries = ChangeTracker.Entries<JournalEntry>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            foreach (var je in newEntries)
            {
                if (je.Lines == null || je.Lines.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"JournalEntry refused on save: entry has no lines. " +
                        $"Source={je.Source ?? "(unset)"}, Batch={je.Batch}. " +
                        $"This is almost certainly a code path that built a JE " +
                        $"header without adding its lines (cf. DEF-N01).");
                }

                decimal debit = 0m, credit = 0m;
                foreach (var line in je.Lines)
                {
                    debit  += line.Debit;
                    credit += line.Credit;
                }

                if (debit != credit)
                {
                    var variance = debit - credit;
                    throw new InvalidOperationException(
                        $"JournalEntry refused on save: unbalanced. " +
                        $"Σdebits={debit:C} Σcredits={credit:C} variance={variance:C}. " +
                        $"Source={je.Source ?? "(unset)"}, Batch={je.Batch}, " +
                        $"lines={je.Lines.Count}. " +
                        $"This is the persistence-boundary guard added in #84 " +
                        $"after DEF-N02 — the JE header says 'BALANCED' if D=C " +
                        $"but the same write used to slide through with D≠C.");
                }
            }
        }

        private void CapitalizeStringProperties()
        {
            // PR #118.1 + PR #119.5.1 — namespace exemptions.
            //
            // Telemetry entities (SensorEvent, AssetSensorLatest, SensorAlarm,
            // AlarmRationalization, UnitConversion, etc.) carry case-sensitive
            // identifiers — Sparkplug B metric names, NE 107 quality labels,
            // ISO 8000 source codes, UNECE Recommendation 20 unit codes,
            // ISA-18.2 alarm keys. Auto-uppercasing would corrupt the value
            // and break exact-match queries the same way the PR #117.5–117.7
            // saga corrupted the EquipmentModel.ModelNumber lookup. Skip the
            // entire Telemetry namespace.
            //
            // WorkOrders entities (WorkOrderFieldVisibility,
            // WorkOrderStatusProfile/Label/Transition, WorkOrderApproval,
            // NumberSequence) carry case-sensitive config keys:
            //   - StatusKey ("PssrRequired", "SubstantialComplete") used as
            //     code-side sentinels.
            //   - GuardServiceName ("PssrCompletionGuard",
            //     "CipCapitalizationGuard") used as DI keys — case-sensitive
            //     lookup at runtime.
            //   - DisplayColor ("gray","blue","amber","green","red") used as
            //     Tailwind class names — must be lowercase.
            //   - Stage (WorkOrderApproval) used to match required-approval
            //     gates exactly.
            //   - FieldName (WorkOrderFieldVisibility) is a C# property
            //     name in PascalCase.
            // Same exemption pattern as Telemetry.
            const string telemetryNamespace = "Abs.FixedAssets.Models.Telemetry";
            const string workOrdersNamespace = "Abs.FixedAssets.Models.WorkOrders";

            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var entityNamespace = entry.Entity.GetType().Namespace;
                if (entityNamespace != null && (
                        entityNamespace.StartsWith(telemetryNamespace)
                     || entityNamespace.StartsWith(workOrdersNamespace)))
                    continue;

                var properties = entry.Properties
                    .Where(p => p.Metadata.ClrType == typeof(string) && p.CurrentValue != null);

                foreach (var property in properties)
                {
                    var propertyName = property.Metadata.Name.ToLower();
                    if (propertyName.Contains("email") || propertyName.Contains("password") || 
                        propertyName.Contains("url") || propertyName.Contains("path") ||
                        propertyName.Contains("hash") || propertyName.Contains("token") ||
                        propertyName.Contains("secret") || propertyName.Contains("eventtype") ||
                        propertyName.Contains("payload") || propertyName.Contains("json") ||
                        propertyName.Contains("correlation") || propertyName.Contains("idempotency") ||
                        propertyName.Contains("integrationkey") || propertyName.Contains("externalid") ||
                        propertyName.Contains("externalentityid") || propertyName.Contains("headers") ||
                        propertyName == "name" || propertyName == "changereason" ||
                        propertyName.Contains("extendeddescription") ||
                        propertyName.Contains("username") || propertyName.Contains("startedby") ||
                        propertyName.Contains("completedby") || propertyName.Contains("closedby") ||
                        // Sprint 14.1 PR-1 — case-preserve user identifier on
                        // ProductionMaterialStructure.CapturedBy +
                        // ProductionOrder.SnapshotCapturedBy. Same convention as
                        // CompletedBy/StartedBy/ClosedBy above.
                        propertyName.Contains("capturedby") ||
                        // Sprint 14.2 PR-1 — case-preserve user identifiers on
                        // DocumentVersion.ApprovedBy / ReleasedBy and
                        // ItemDocumentLink.LinkedBy. Same convention.
                        propertyName.Contains("approvedby") ||
                        propertyName.Contains("releasedby") ||
                        propertyName.Contains("linkedby") ||
                        // Sprint 14.3 PR-1 — case-preserve user identifiers on
                        // ECR.DecidedBy/ReviewedBy/RequestedBy, ECO.Implemented
                        // By/ClosedBy, EcoApproval.DecidedBy, EcoApproval.Required
                        // Approver, ECO.RequiredApprover. Same convention.
                        propertyName.Contains("decidedby") ||
                        propertyName.Contains("reviewedby") ||
                        propertyName.Contains("requestedby") ||
                        propertyName.Contains("implementedby") ||
                        propertyName.Contains("closedby") ||
                        propertyName.Contains("requiredapprover") ||
                        // Sprint 14.3 PR-1 — case-preserve free-form human-text
                        // fields on the ECR/ECO substrate. Same convention as
                        // notes / description / changereason (already in the
                        // allowlist). These contain sentences like "Rev A → Rev B"
                        // and "Cost > expected benefit; revisit Q3" that need
                        // their case preserved for readability.
                        propertyName.Contains("rejectionreason") ||
                        propertyName.Contains("beforevalue") ||
                        propertyName.Contains("aftervalue") ||
                        propertyName.Contains("decisionnotes") ||
                        // EcoApproval.ApprovalRole is a human-readable role
                        // label ("Engineering Lead" / "Quality Manager" /
                        // "Customer Liaison") — case preserved.
                        propertyName.Contains("approvalrole") ||
                        propertyName.Contains("holdreason") || propertyName.Contains("lessonslearned") ||
                        propertyName.Contains("resolution") || propertyName.Contains("notes") ||
                        propertyName.Contains("entitytype") || propertyName.Contains("action") ||
                        propertyName.Contains("description") ||
                        // ADR-022 / Sprint 12D PR #2 — chain-of-custody graph
                        // ChainNodes.NodeType, ChainNodes.Label, ChainEdges.EdgeType
                        // are case-preserving (mixed-case business labels +
                        // PascalCase polymorphic type tags).
                        propertyName.Contains("nodetype") || propertyName.Contains("edgetype") ||
                        propertyName == "label" ||
                        propertyName == "key" || propertyName == "code" || propertyName == "metadata")
                        continue;
                    
                    property.CurrentValue = ((string)property.CurrentValue!).ToUpperInvariant();
                }
            }
        }
    }
}