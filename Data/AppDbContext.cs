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
            // OrderNumber UNIQUE — one human-facing identifier per order.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrder>(e =>
            {
                e.HasIndex(x => x.OrderNumber).IsUnique();
                e.HasIndex(x => x.Type);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.ScheduledStart);
                e.HasIndex(x => x.ScheduledEnd);
                e.HasIndex(x => new { x.MasterProductionOrderId, x.Revision });
                e.HasOne(x => x.MasterProductionOrder)
                    .WithMany()
                    .HasForeignKey(x => x.MasterProductionOrderId)
                    .OnDelete(DeleteBehavior.SetNull);
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
            // BatchNumber UNIQUE. Type / Status / BatchPoolCode all indexed
            // for queue + dashboard filtering. PrimaryEquipment SET NULL on
            // equipment delete (the batch record outlives the machine).
            // RecipeRevision SET NULL on delete (stub table; full revisioning
            // in PR #119.14). MrbDisposition SET NULL on delete.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionBatch>(e =>
            {
                e.HasIndex(x => x.BatchNumber).IsUnique();
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
            // ShopCode UNIQUE per shop. AstmDesignation indexed for
            // cross-shop analytics joins.
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MaterialMaster>(e =>
            {
                e.HasIndex(x => x.ShopCode).IsUnique();
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
            // StructureNumber UNIQUE. StructureType / Status indexed for
            // queue/dashboard filtering. Revision-chain self-FK mirrors
            // RecipeRevision pattern (SET NULL on master delete).
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.MaterialStructure>(e =>
            {
                e.HasIndex(x => x.StructureNumber).IsUnique();
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
            modelBuilder.Entity<Abs.FixedAssets.Models.Production.ProductionOrder>(e =>
            {
                e.HasIndex(x => x.MaterialStructureId);
                e.HasOne(x => x.MaterialStructure)
                    .WithMany()
                    .HasForeignKey(x => x.MaterialStructureId)
                    .OnDelete(DeleteBehavior.SetNull);
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
                e.HasOne(x => x.Item)
                    .WithMany()
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
                e.HasOne(x => x.Program)
                    .WithMany()
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
                e.HasOne(x => x.Project)
                    .WithMany()
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
                e.HasOne(x => x.Project)
                    .WithMany()
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
                e.HasOne(x => x.Project)
                    .WithMany()
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

                e.HasOne(x => x.FaiReport)
                    .WithMany()
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

                e.HasOne(x => x.FaiReport)
                    .WithMany()
                    .HasForeignKey(x => x.FaiReportId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Vendor)
                    .WithMany()
                    .HasForeignKey(x => x.VendorId)
                    .OnDelete(DeleteBehavior.SetNull);
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