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
        public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
        public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
        public DbSet<WorkRequest> WorkRequests => Set<WorkRequest>();
        public DbSet<LessonLearned> LessonsLearned => Set<LessonLearned>();

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

        // Purchasing & Accounts Payable
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
        public DbSet<PurchaseOrderRelease> PurchaseOrderReleases => Set<PurchaseOrderRelease>();
        public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
        public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
            modelBuilder.Entity<MaintenanceEvent>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.ScheduledDate);
                e.HasIndex(x => x.Status);
                e.HasOne(x => x.Asset)
                    .WithMany(a => a.MaintenanceEvents)
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

            // Attachments
            modelBuilder.Entity<Attachment>(e =>
            {
                e.HasIndex(x => x.AssetId);
                e.HasIndex(x => x.MaintenanceEventId);
                e.HasIndex(x => x.CipProjectId);
                e.HasOne(x => x.Asset)
                    .WithMany(a => a.Attachments)
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.MaintenanceEvent)
                    .WithMany()
                    .HasForeignKey(x => x.MaintenanceEventId)
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
                e.HasMany(x => x.MaintenanceEvents)
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
                e.HasIndex(x => new { x.MaintenanceEventId, x.ItemId });
                e.HasOne(x => x.MaintenanceEvent)
                    .WithMany()
                    .HasForeignKey(x => x.MaintenanceEventId)
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
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
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
                        propertyName == "key" || propertyName == "code" || propertyName == "metadata")
                        continue;
                    
                    property.CurrentValue = ((string)property.CurrentValue!).ToUpperInvariant();
                }
            }
        }
    }
}