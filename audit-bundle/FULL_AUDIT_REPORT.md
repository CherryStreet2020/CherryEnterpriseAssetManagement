# CherryAI Enterprise Asset Management — FULL AUDIT REPORT
**Generated:** February 15, 2026
**Smoke Tests:** 77/77 PASSING

---

## 1. PROJECT OVERVIEW

| Metric | Value |
|--------|-------|
| Framework | ASP.NET Core 9.0 (Razor Pages) |
| Language | C# with .cshtml views |
| Database | PostgreSQL (Neon-backed via Replit) |
| ORM | Entity Framework Core 9.0 |
| Target | net9.0 |
| Total Source Files | ~1,156 (excluding bin/obj/git) |
| Total Lines of Code | ~810,930 |
| Database Tables | 116 |
| Smoke Tests | 77 (all passing) |
| Documentation Files | 74 markdown files |
| NuGet Packages | 6 |

---

## 2. NUGET DEPENDENCIES

| Package | Version | Purpose |
|---------|---------|---------|
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4 | PostgreSQL EF Core provider |
| Microsoft.EntityFrameworkCore.Tools | 9.0.0 | EF migrations tooling |
| Microsoft.EntityFrameworkCore.Design | 9.0.0 | EF design-time services |
| ClosedXML | 0.104.2 | Excel export generation |
| QuestPDF | 2024.10.0 | PDF report generation |
| ZXing.Net.Bindings.SkiaSharp | 0.16.14 | Barcode generation/scanning |

---

## 3. LINES OF CODE BY DIRECTORY

| Directory | Lines |
|-----------|-------|
| Pages/ | 54,875 |
| Services/ | 28,423 |
| wwwroot/ (CSS/JS) | 99,260 |
| Models/ | 7,984 |
| Migrations/ | 173,741 |
| Data/ | 1,645 |
| Controllers/ | 725 |
| Middleware/ | 161 |
| Helpers/ | 32 |

---

## 4. FILE COUNTS BY TYPE

| Extension | Count |
|-----------|-------|
| .cshtml | 374 |
| .cs | 288 |
| .md | 76 |
| .css | 51 |
| .js | 24 |
| .json | 8 |
| .jpg/.jpeg | 10 |
| .csv | 5 |
| .sh | 4 |
| .py | 4 |
| .sql | 1 |

---

## 5. APPLICATION ARCHITECTURE

### 5.1 Frontend Hosting
- Bound to `0.0.0.0:5000`
- AllowedHosts: `*`
- Razor Pages with `_ModernLayout.cshtml` (975 lines) as primary layout
- Design system: Custom tokens.css + modern.css (no Tailwind runtime)

### 5.2 Tenant Configuration
```json
{
  "DeploymentMode": "SingleTenant",
  "DefaultTenantId": 1,
  "DefaultCompanyId": 1,
  "DefaultSiteId": 1
}
```

### 5.3 Authentication
- Cookie-based with ASP.NET Core Identity
- Roles: Admin, Accountant, Viewer
- Replit iframe compatibility mode

---

## 6. MODELS (60 files, 7,984 lines)

| Model | Lines | Key Fields |
|-------|-------|------------|
| Item.cs | 1,021 | 99 DB columns — fullest model |
| Asset.cs | 518 | 158 DB columns — core entity |
| GlAccount.cs | 424 | Chart of accounts |
| PMTemplate.cs | 385 | Preventive maintenance templates |
| PurchaseRequisition.cs | 294 | Procurement workflow |
| WorkOrderOperation.cs | 281 | Work execution |
| WorkOrderCodes.cs | 268 | Reference code enums |
| PurchaseOrder.cs | 261 | Purchase management |
| SystemConfig.cs | 258 | System configuration |
| DepreciationPolicy.cs | 244 | Depreciation rules |
| AssetMaintenance.cs | 235 | Maintenance tracking |
| FiscalPeriod.cs | 190 | Period management |
| VendorInvoice.cs | 180 | AP invoices |
| Company.cs | 167 | Multi-company |
| Site.cs | 169 | Site management |
| LaborConfig.cs | 159 | Labor tracking |
| ConstructionInProgress.cs | 145 | CIP projects |
| Vendor.cs | 138 | Vendor master |
| PMSchedule.cs | 134 | PM scheduling |
| PMTemplateRevision.cs | 135 | Revision control |
| ItemRevisionEnhanced.cs | 131 | Item revisions |
| AssetInventory.cs | 131 | Physical inventory |
| PartialDisposal.cs | 103 | Partial disposals |
| UsTaxSettings.cs | 105 | US tax rules |
| WorkRequest.cs | 103 | Work requests |
| GoodsReceipt.cs | 118 | Receiving |
| Enums.cs | 92 | Shared enumerations |
| Book.cs | 78 | Depreciation books |
| Attachment.cs | 81 | Universal attachments |
| WebhookSubscription.cs | 79 | Webhook config |
| IntegrationEndpoint.cs | 71 | Integration endpoints |
| AssetBookSettings.cs | 70 | Per-book settings |
| OutboxEvent.cs | 65 | Outbox pattern |
| InboundEvent.cs | 60 | Inbound webhooks |
| CcaTransaction.cs | 62 | Canadian CCA |
| JournalEntry.cs | 60 | Journal entries |
| User.cs | 56 | User accounts |
| AuditLog.cs | 55 | Audit trail |
| CcaClassBalance.cs | 54 | CCA balances |
| ItemApprovedVendor.cs | 52 | AVL entries |
| Manufacturer.cs | 48 | Manufacturers |
| ItemAlternate.cs | 47 | Alternate parts |
| AssetTaxSettings.cs | 47 | Tax settings |
| AssetTransfer.cs | 46 | Asset transfers |
| IntegrationMapping.cs | 46 | ID mapping |
| BookGlAccount.cs | 46 | Book GL links |
| CapitalImprovement.cs | 41 | Capital improvements |
| Technician.cs | 41 | Technicians |
| ProjectManager.cs | 39 | Project managers |
| Tenant.cs | 39 | Tenants |
| LessonLearned.cs | 37 | Lessons learned |
| ApiKey.cs | 36 | API keys |
| PageTitle.cs | 35 | Page titles |
| ItemSupersession.cs | 35 | Part supersession |
| WebhookDeliveryLog.cs | 34 | Delivery logs |
| CcaClass.cs | 33 | CCA classes |
| JournalLine.cs | 33 | Journal lines |
| AssetBookValue.cs | 31 | Book values |
| ExchangeRate.cs | 29 | Exchange rates |
| RevisionStatus.cs | 9 | Status enum |

---

## 7. SERVICES (66 files, 28,423 lines)

### 7.1 Core Services

| Service | Lines | Function |
|---------|-------|----------|
| SmokeTestRunner.cs | 11,258 | 77 deterministic smoke tests |
| DemoPackV2Pipeline.cs | 1,348 | Demo data seeding v2 |
| SeedPackExecutor.cs | 951 | Seed package execution |
| MasterDataBootstrapService.cs | 901 | Master data setup |
| SystemReferenceSeedPipeline.cs | 758 | System reference data |
| MaintenanceService.cs | 686 | Maintenance CRUD |
| DemoPackV1Pipeline.cs | 576 | Demo data v1 |
| PMSchedulerService.cs | 509 | PM schedule generation |
| AiAssistantService.cs | 497 | AI chat assistant |
| SmartAssistService.cs | 477 | Work request analysis |
| WorkRequestConversionService.cs | 395 | WR → WO conversion |
| CloseoutService.cs | 386 | WO closeout intelligence |
| DepreciationService.cs | 376 | Depreciation calculations |
| ExportService.cs | 372 | Excel/PDF exports |
| CcaService.cs | 362 | Canadian CCA calculations |
| ItemCrossReferenceService.cs | 340 | Part number resolution |
| SeedPipelineExecutor.cs | 327 | Pipeline orchestration |
| PMTemplateRevisionService.cs | 317 | PM revision control |
| ItemRevisionService.cs | 284 | Item revision management |
| InboundEventProcessorHostedService.cs | 284 | Inbound webhook processing |
| ItemStockingService.cs | 279 | Inventory stocking |
| OrgAndFinanceSeedPipeline.cs | 275 | Org/finance seeding |
| CatalogMetadataEnrichmentService.cs | 266 | Catalog intelligence |
| ReportBuilderService.cs | 263 | Report generation |
| EffectiveProcurementService.cs | 248 | Procurement value cascade |
| JournalGenerator.cs | 235 | Journal entry generation |
| WebhookDispatcherHostedService.cs | 232 | Outbox webhook dispatch |
| WorkOrderOriginService.cs | 209 | WO origin tracking |
| BuyabilityScoreService.cs | 207 | Buyability tier system |
| UsTaxService.cs | 207 | US tax calculations |
| ImportService.cs | 202 | Data import |
| ItemImageService.cs | 197 | Image upload/management |
| VendorsAndPartsFoundationSeedPipeline.cs | 191 | Vendor/parts seeding |
| InventoryService.cs | 189 | Inventory management |
| ItemSupersessionService.cs | 179 | Part supersession |
| TenantContext.cs | 178 | Multi-tenant context |
| BulkOperationsService.cs | 177 | Bulk operations |
| AttachmentService.cs | 175 | File attachments |
| InboundWebhookService.cs | 174 | Inbound webhook handling |
| SeedGuardService.cs | 173 | Seed safety guards |
| OutboxWriter.cs | 168 | Event outbox |
| TenantContextMiddleware.cs | 161 | Tenant resolution |
| SmokeTestDataFactory.cs | 159 | Test data factory |
| SmokeTestRunStore.cs | 159 | Test run persistence |
| ItemSourcingService.cs | 158 | Item sourcing |
| BaseSeedStep.cs | 151 | Seed base class |
| ApiService.cs | 151 | API service |
| BarcodeService.cs | 149 | Barcode gen/scan |
| CipService.cs | 142 | CIP management |
| ISeedPipeline.cs | 140 | Pipeline interface |
| AuditService.cs | 132 | Audit logging |
| SmokeTestBackgroundService.cs | 128 | Background test runner |
| ReturnUrlHelper.cs | 123 | Safe return URL handling |
| ItemAlternateService.cs | 119 | Alternate parts |
| AuthService.cs | 110 | Authentication |
| CompanyService.cs | 109 | Company management |
| IntegrationMappingService.cs | 102 | Integration mapping |
| SeedPack.cs | 96 | Seed packaging |
| PreferredVendorCatalogResolver.cs | 83 | Vendor catalog resolution |
| ModuleGuardService.cs | 69 | Module activation |
| EamExecutionMastersSeedPipeline.cs | 65 | EAM execution data |
| DemoScenarioSeedPipeline.cs | 111 | Demo scenario seeding |
| SmokeTestRunQueue.cs | 43 | Test queue |
| DepreciationMath.cs | 40 | Math helpers |
| NbvHelper.cs | 25 | Net book value |
| SeedingServiceExtensions.cs | 22 | DI extensions |
| SmartAssistConstants.cs | 9 | Constants |

---

## 8. PAGES BY MODULE

### 8.1 Admin Section (largest, ~100 pages)

| Page | .cshtml Lines | .cs Lines | Function |
|------|---------------|-----------|----------|
| Items.cshtml | 773 | 339 | Item master list |
| SeedData.cshtml | 147 | 675 | Seed data management |
| DataImport.cshtml | 324 | 773 | Data import tool |
| Sites.cshtml | 515 | 170 | Site management (IN MIGRATION) |
| Company.cshtml | 493 | 128 | Company settings |
| Locations.cshtml | 414 | 115 | Location management (MIGRATED) |
| PMTemplateEdit.cshtml | 405 | 225 | PM template editor |
| DemoData.cshtml | 426 | 352 | Demo data admin |
| SmokeTests.cshtml | 397 | 317 | Smoke test runner |
| Vendors.cshtml | 384 | 166 | Vendor master (MIGRATED) |
| AssetCategories.cshtml | 311 | 100 | Asset categories |
| Requisitions.cshtml | 289 | 519 | Purchase requisitions |
| Users.cshtml | 278 | 140 | User management |
| PMSchedules.cshtml | 251 | 104 | PM schedules |
| CostCenters.cshtml | 250 | 96 | Cost centers |
| Manufacturers.cshtml | 244 | 129 | Manufacturers |
| PMScheduleEdit.cshtml | 242 | 263 | PM schedule editor |
| Technicians.cshtml | 248 | 117 | Technician management |
| Departments.cshtml | 222 | 90 | Departments |
| Companies.cshtml | 200 | 110 | Company list |
| Inventory.cshtml | 342 | 281 | Inventory management |
| AuditLog.cshtml | 194 | 80 | Audit log viewer |
| Barcodes.cshtml | 177 | 50 | Barcode tools |
| PMTemplates.cshtml | 165 | 32 | PM template list |
| Tenants.cshtml | 160 | 92 | Tenant management |
| GlAccounts.cshtml | 257 | 96 | GL accounts |
| SystemSettings.cshtml | 175 | 66 | System settings |
| ExchangeRates.cshtml | 149 | 66 | Exchange rates |
| ProjectManagers.cshtml | 232 | 113 | Project managers |
| Diagnostics.cshtml | 283 | 272 | System diagnostics |
| StockLevels.cshtml | 143 | 100 | Stock levels |
| Kits.cshtml | 153 | 66 | Kit management |

### 8.2 Assets Module

| Page | .cshtml Lines | .cs Lines | Function |
|------|---------------|-----------|----------|
| Asset.cshtml | 1,270 | 444 | Asset detail/edit (LARGEST PAGE) |
| Index.cshtml | 240 | 74 | Asset list |
| Dispose.cshtml | 239 | 210 | Asset disposal |
| Transfer.cshtml | 197 | 115 | Asset transfer |
| Improve.cshtml | 193 | 118 | Capital improvement |
| Schedule.cshtml | 64 | 60 | Depreciation schedule |
| Delete.cshtml | 38 | 62 | Asset deletion |

### 8.3 Materials / Inventory Module

| Page | .cshtml Lines | .cs Lines | Function |
|------|---------------|-----------|----------|
| ItemEdit.cshtml | 1,751 | 774 | Item edit (2ND LARGEST PAGE) |
| Items.cshtml | 400 | 268 | Item list |
| List.cshtml (Inventory) | 241 | 108 | Inventory list |
| Index.cshtml (Inventory) | 269 | 54 | Inventory dashboard |

### 8.4 Maintenance Module

| Page | .cshtml Lines | .cs Lines | Function |
|------|---------------|-----------|----------|
| Details.cshtml | 842 | 293 | Maintenance event detail |
| Index.cshtml | 496 | 170 | Maintenance list |
| Schedules.cshtml | 203 | 92 | Schedule list |
| Assignments/Index.cshtml | 219 | 176 | PM assignments |
| WorkRequests/Create.cshtml | 398 | 500 | Work request creation |
| WorkRequests/Index.cshtml | 336 | 89 | Work request list |
| WorkRequests/Details.cshtml | 119 | 77 | Work request detail |

### 8.5 Work Orders Module

| Page | .cshtml Lines | .cs Lines | Function |
|------|---------------|-----------|----------|
| Details.cshtml | 687 | 427 | Work order detail |

### 8.6 Finance Modules

| Page Group | Pages | Key Function |
|------------|-------|-------------|
| Books/ | 6 pages | Depreciation book CRUD |
| Journals/ | 3 pages | Journal entry management |
| Reports/ | 9 pages | Financial reports |
| CCA/ | 2 pages | Canadian CCA |
| CIP/ | 5 pages | Capital projects |
| UsTax/ | 1 page | US tax management |
| BulkOperations/ | 2 pages | Bulk asset operations |

### 8.7 Other Modules

| Page Group | Pages | Key Function |
|------------|-------|-------------|
| Purchasing/ | 2 pages | PO management |
| Receiving/ | 1 page | Goods receiving |
| AccountsPayable/ | 2 pages | Invoice management |
| AI/ | 1 page | AI assistant |
| API/ | 2 pages | API documentation |
| Help/ | 4 pages | Help center |
| Account/ | 3 pages | Login/logout |

---

## 9. CONTROLLERS (6 files, 725 lines)

| Controller | Lines | Function |
|-----------|-------|----------|
| BarcodeApiController.cs | 294 | Barcode generation/scanning API |
| AssetsApiController.cs | 191 | Asset REST API |
| BackupController.cs | 111 | Database backup API |
| IntegrationWebhookController.cs | 56 | Inbound webhook receiver |
| ItemsApiController.cs | 40 | Items API |
| AuthController.cs | 33 | Auth API |

---

## 10. CSS DESIGN SYSTEM (32 files, ~14,000 lines)

| File | Lines | Purpose |
|------|-------|---------|
| modern.css | 3,003 | Main theme + legacy variable aliases |
| premium-components.css | 1,932 | Premium UI components |
| help-enterprise.css | 1,570 | Help center styling |
| item-master.css | 1,390 | Item master page styles |
| tour.css | 491 | Guided tour |
| work-order-details.css | 531 | WO detail page |
| sidebar-nav.css | 417 | Sidebar navigation |
| admin.css | 415 | Admin section |
| assets.css | 392 | Assets module |
| forms.css | 369 | Premium Form System |
| dashboard.css | 313 | Dashboard |
| auth.css | 306 | Authentication pages |
| workorders.css | 280 | Work orders module |
| ai.css | 244 | AI assistant |
| work-requests.css | 232 | Work requests |
| tokens.css | 223 | Design tokens (9+ families) |
| site.css | 211 | Base site styles |
| reports.css | 181 | Reports module |
| headers.css | 180 | Screen headers |
| bulk-operations.css | 186 | Bulk operations |
| help.css | 155 | Help module |
| tabs.css | 151 | Tab navigation |
| layout-components.css | 149 | Layout components |
| cip.css | 148 | CIP module |
| purchasing.css | 117 | Purchasing |
| inventory.css | 111 | Inventory |
| api.css | 101 | API pages |
| base.css | 96 | CSS reset/base |
| finance.css | 79 | Finance module |
| maintenance.css | 70 | Maintenance |
| overrides.css | 40 | Overrides |
| tax.css | 27 | Tax module |

---

## 11. JAVASCRIPT (8 files, 2,612 lines)

| File | Lines | Purpose |
|------|-------|---------|
| tour.js | 970 | Interactive guided tour |
| enhanced-grid.js | 946 | Premium DataGrid controls |
| implementation-guide.js | 227 | Implementation guide |
| work-requests-create.js | 153 | WR creation logic |
| tabs.js | 117 | Tab system |
| sidebar-nav.js | 107 | Sidebar navigation |
| modal.js | 48 | Modal system |
| site.js | 44 | Global site JS |

---

## 12. SHARED PARTIALS (13 files)

| Partial | Lines | Purpose |
|---------|-------|---------|
| _ModernLayout.cshtml | 975 | Primary layout (sidebar, header, footer) |
| _ScreenHeader.cshtml | 151 | Unified screen header |
| _TabNav.cshtml | 121 | Tab navigation component |
| _FormField.cshtml | 107 | Premium form field partial |
| _AssetMaintenanceHeader.cshtml | 101 | Asset/maintenance header |
| _SectionCard.cshtml | 93 | Section card component |
| _QuickStatIcon.cshtml | 84 | KPI stat with icon |
| _Layout.cshtml | 79 | Base layout |
| _EmptyState.cshtml | 64 | Empty state display |
| _QuickStat.cshtml | 35 | Quick stat component |
| _BackLink.cshtml | 19 | Back navigation link |
| _KpiStrip.cshtml | 18 | KPI strip component |
| _ValidationScriptsPartial.cshtml | 2 | Validation scripts |

---

## 13. DATABASE SCHEMA (116 tables)

### 13.1 Core Entity Tables

| Table | Columns | Purpose |
|-------|---------|---------|
| Assets | 158 | Core asset records (MES, IoT, Safety, Energy, PdM fields) |
| Items | 99 | Item master with procurement, revision, cross-reference |
| MaintenanceEvents | 54 | Work orders / maintenance events |
| Companies | 47 | Multi-company support |
| Sites | 43 | Site management |
| DepreciationPolicies | 43 | Depreciation rules |
| PMTemplateRevisions | 42 | PM template revision control |
| PMTemplates | 38 | PM templates |
| PurchaseRequisitions | 37 | Procurement requests |
| Locations | 36 | Location hierarchy |
| Vendors | 30 | Vendor master |
| WorkOrderOperations | 29 | Work execution operations |
| Books | 28 | Depreciation books |
| PurchaseOrders | 28 | Purchase orders |
| ItemVendors | 28 | Vendor-item relationships |
| VendorItemParts | 26 | Vendor part numbers |
| ItemCompanyStockings | 25 | Per-company stocking |
| CipProjects | 24 | Capital improvement projects |
| PurchaseOrderLines | 24 | PO line items |
| PurchaseRequisitionLines | 23 | PR line items |
| VendorInvoices | 23 | Vendor invoices |
| WorkRequests | 22 | Work requests |
| PMSchedules | 21 | PM schedules |
| ItemTransactions | 21 | Item transaction history |
| AssetBookSettings | 19 | Per-book asset settings |
| GlAccounts | 19 | Chart of accounts |

### 13.2 Reference/Config Tables

| Table | Columns | Purpose |
|-------|---------|---------|
| NumberingSequences | 17 | Auto-numbering |
| CcaClassBalances | 17 | Canadian CCA |
| CcaTransactions | 17 | CCA transactions |
| UsTaxSettings | 17 | US tax settings |
| ItemInventories2 | 17 | Item inventory |
| Attachments | 16 | Universal attachments |
| ItemRevisions | 16 | Item revision tracking |
| WebhookSubscriptions | 16 | Webhook config |
| PartialDisposals | 16 | Partial disposals |
| WorkOrderParts | 16 | WO parts usage |
| AssetCategories | 16 | Asset categories |
| UsefulLifeEntries | 16 | Useful life data |
| CostCenters | 15 | Cost centers |
| MaintenanceSchedules | 15 | Maintenance schedules |
| FiscalPeriods | 15 | Fiscal periods |
| DepreciationRuns | 15 | Depreciation runs |
| InboundEvents | 15 | Inbound webhook events |
| OutboxEvents | 15 | Outbox events |
| IntegrationEndpoints | 14 | Integration endpoints |
| GoodsReceipts | 14 | Goods receipts |
| GoodsReceiptLines | 14 | Receipt lines |
| ItemImages | 14 | Item images |
| FiscalYears | 14 | Fiscal years |
| Users | 14 | User accounts |
| CipCosts | 14 | CIP costs |
| Manufacturers | 13 | Manufacturers |
| AssetInventories | 13 | Physical inventory |
| AssetTransfers | 13 | Asset transfers |
| InventoryLists | 13 | Inventory lists |
| LaborRates | 13 | Labor rates |
| PMTemplateAssets | 13 | PM template-asset links |
| ReorderAlerts | 13 | Reorder alerts |
| Skills | 13 | Worker skills |
| WorkOrderOperationLabors | 13 | Labor tracking |
| WorkOrderOperationTools | 13 | Tool tracking |
| CapitalImprovements | 12 | Capital improvements |
| PMOccurrences | 12 | PM occurrences |
| VendorInvoiceLines | 12 | Invoice lines |
| Technicians | 12 | Technicians |
| DepreciationRunDetails | 11 | Depreciation details |
| AssetTaxSettings | 11 | Asset tax settings |
| Departments | 11 | Departments |
| ItemApprovedVendors | 11 | Approved vendor list |
| ItemManufacturerParts | 11 | MPN cross-reference |
| InventoryScans | 11 | Barcode scans |
| LessonsLearned | 11 | Lessons learned |
| PriorityLevels | 11 | Priority levels |
| ProjectManagers | 11 | Project managers |
| BulkOperations | 11 | Bulk operations |
| WorkOrderOperationParts | 15 | Operation parts |
| Kits | 10 | Kit assemblies |
| ApiKeys | 10 | API keys |
| PaymentTerms | 10 | Payment terms |
| ShippingMethods | 10 | Shipping methods |
| UOMDefinitions | 10 | Unit of measure |
| PurchaseOrderReleases | 10 | PO releases |
| WorkOrderTypes | 10 | WO types |
| AuditLogs | 10 | Audit trail |
| Tenants | 9 | Tenant records |
| BookGlAccounts | 9 | Book-GL links |
| CcaClasses | 9 | CCA classes |
| IntegrationMappings | 9 | Integration ID maps |
| ItemCategories | 9 | Item categories |
| LaborTypes | 9 | Labor types |
| ExchangeRates | 9 | Exchange rates |
| WebhookDeliveryLogs | 9 | Delivery logs |
| PMTemplateRevisionOperations | 9 | Rev. operations |
| ApprovalWorkflows | 13 | Approvals |
| ActionCodes | 8 | Action codes |
| CauseCodes | 8 | Cause codes |
| Currencies | 8 | Currencies |
| FailureCodes | 8 | Failure codes |
| InvoicePayments | 8 | Invoice payments |
| MaintenanceTypeCodes | 8 | Maintenance types |
| PMTemplateItems | 8 | PM template items |
| ProblemCodes | 8 | Problem codes |
| ItemAlternates | 10 | Alternate parts |
| ItemSupersessions | 8 | Part supersessions |
| Crafts | 10 | Worker crafts |
| TaxCodes | 11 | Tax codes |
| UsefulLifeTables | 7 | Useful life tables |
| KitItems | 7 | Kit items |
| JournalLines | 7 | Journal lines |
| JournalEntries | 9 | Journal entries |
| PolicyCategoryDefaults | 7 | Policy defaults |
| Section179Limits | 7 | Section 179 limits |
| BonusDepreciationRates | 4 | Bonus depreciation |
| PeriodLocks | 6 | Period locks |

### 13.3 Row Count Status
**All tables show 0 rows** in pg_stat_user_tables (approximate count). This means either:
- Data is populated via demo seed and hasn't been vacuum-analyzed yet
- OR the database is empty awaiting seed execution

---

## 14. DATA LAYER

| File | Lines | Purpose |
|------|-------|---------|
| AppDbContext.cs | 1,173 | EF Core DbContext with all 116 table mappings |
| Seed.cs | 426 | Base seed data |
| CcaClassSeeder.cs | 46 | CCA class reference data |

---

## 15. MIGRATIONS (19 migrations)

| Migration | Key Changes |
|-----------|-------------|
| AddMultiLocationPurchasing | PO/PurchaseRequisition tables |
| AddPurchaseOrderReleases | PO release tracking |
| AddFiscalCalendar | FiscalYear/FiscalPeriod tables |
| AddCIPToBookGlAccount | CIP GL account link |
| AddMesIotOeeFields | MES, IoT, OEE, Safety, Energy fields on Assets |
| AddAssetImageUrl | Asset image support |
| AddWorkRequests | Work request system |
| Sprint3CloseoutIntelligence | WO closeout with heuristics |
| Sprint4WebhooksIntegrationHub | Outbox webhooks |
| Sprint5WebhooksProductization | Webhook subscriptions |
| Sprint6InboundWebhooks | Inbound webhook receiver |
| Sprint6InboundWebhooksV2 | Enhanced inbound events |
| AddPMTemplateRevisions | PM template revision control |
| Sprint11ItemCrossReference | MPN/VPN cross-reference |
| Sprint12ProcurementGradeParts | AVL, alternates, supersession |
| AddImageUrlToVendorItemPart | Vendor part images |
| Sprint13CatalogIntelligence | Catalog metadata |
| Sprint14ProcurementV2Lite | Extended procurement fields |
| RequireTenantIdOnTenantScopedEntities | Tenant isolation |
| AddWorkOrderExecutionFields | WO execution fields |

---

## 16. PREMIUM FORM MIGRATION STATUS

### 16.1 Completed Pages
| Page | Status | Notes |
|------|--------|-------|
| Locations.cshtml (Create) | COMPLETE | 12/12 fields migrated |
| Sites.cshtml (Create modal) | COMPLETE | 21/21 fields migrated |
| Sites.cshtml (Edit modal) | IN PROGRESS | 17/21 fields migrated |
| Books/Edit.cshtml | COMPLETE | Boolean controls as Bootstrap switches |
| Vendors.cshtml | COMPLETE | Boolean controls standardized |

### 16.2 Sites Edit Modal Remaining (4 fields)
- editSquareFootage (numeric)
- editNumberOfBuildings (numeric)
- editEmployeeCount (numeric)
- editIsPrimarySite (checkbox → Bootstrap switch)

### 16.3 Pages NOT YET Migrated
All remaining pages need Premium Form migration:
- Asset.cshtml (1,270 lines)
- ItemEdit.cshtml (1,751 lines)
- Maintenance/Details.cshtml (842 lines)
- WorkOrders/Details.cshtml (687 lines)
- All Admin CRUD pages with forms
- All CIP, Purchasing, Reports forms

---

## 17. DOCUMENTATION INDEX (74 files)

### Architecture Decision Records (ADRs)
- ADR-001: PMSchedule Canonical Model
- ADR-002: DemoPackV2 Canonical Seed
- ADR-003: SmokeTest Transaction Rollback
- ADR-004: UI Hygiene No Inline Styles
- ADR-005: DataGrid Premium Contract
- ADR-006: ReturnUrl Security Hardening
- ADR-007: Unified Tab System
- ADR-008: Unified Screen Header System
- ADR-010: Design Tokens

### Key Documentation
- Developer Getting Started (303 lines)
- Architecture Overview (174 lines)
- Database Schema (350 lines)
- UX Standards (664 lines)
- Premium UX Alignment Plan (666 lines)
- Operations Runbook (344 lines)
- Release Checklist (189 lines)
- Route Registry (270 lines)
- Testing & Smoke Suite (303 lines)
- Support Playbook (479 lines)
- Security Response (279 lines)
- Brand Guardrails (407 lines)
- Decision Log (525 lines)
- Seed Packages (377 lines)
- Navigation Audit (372 lines)
- Domain Model (261 lines)

---

## 18. KEY FEATURES STATUS

| Feature | Status | Notes |
|---------|--------|-------|
| Asset CRUD | FUNCTIONAL | Create, edit, view, delete |
| Asset Transfers | FUNCTIONAL | With audit trail |
| Asset Disposal | FUNCTIONAL | Including partial disposal |
| Capital Improvements | FUNCTIONAL | Add/track improvements |
| Multi-Book Depreciation | FUNCTIONAL | GAAP + Tax books, 22 methods |
| US Tax Engine | FUNCTIONAL | Section 179, Bonus depreciation |
| Canadian CCA | FUNCTIONAL | CCA class calculations |
| Work Orders | FUNCTIONAL | Full CMMS workflow |
| Work Requests | FUNCTIONAL | Smart Assist analysis |
| PM Templates | FUNCTIONAL | With revision control |
| PM Schedules | FUNCTIONAL | Auto WO generation |
| Item Master | FUNCTIONAL | 99-column full model |
| Item Revisions | FUNCTIONAL | Draft/release workflow |
| Cross-Reference | FUNCTIONAL | MPN/VPN/Internal |
| AVL (Approved Vendor List) | FUNCTIONAL | Multi-vendor sourcing |
| Purchase Orders | FUNCTIONAL | PO lifecycle |
| Purchase Requisitions | FUNCTIONAL | Approval workflow |
| Goods Receiving | FUNCTIONAL | Receipt processing |
| Accounts Payable | FUNCTIONAL | Invoice management |
| Vendor Management | FUNCTIONAL | Full vendor master |
| Barcode System | FUNCTIONAL | 5 formats + scanning |
| AI Assistant | FUNCTIONAL | OpenAI-powered chat |
| Webhooks (Outbound) | FUNCTIONAL | Outbox pattern |
| Webhooks (Inbound) | FUNCTIONAL | With signature verification |
| Integration Endpoints | FUNCTIONAL | Admin management |
| Bulk Operations | FUNCTIONAL | Transfer, status, disposal |
| Multi-Tenant | FUNCTIONAL | Header-based resolution |
| RBAC | FUNCTIONAL | Admin/Accountant/Viewer |
| Audit Trail | FUNCTIONAL | All entity changes |
| Reports | FUNCTIONAL | Excel, PDF, Form 4562 |
| Help Center | FUNCTIONAL | Tasks, topics, glossary |
| DataGrid Controls | FUNCTIONAL | Sort, filter, export |
| Premium UI System | IN PROGRESS | Form migration ongoing |
| Mobile API | PARTIAL | REST endpoints prepared |
| Fiscal Calendar | STRUCTURAL | Tables exist, limited UI |

---

## 19. KNOWN TECHNICAL DEBT / GAPS

1. **Premium Form Migration**: ~85% of form pages still need migration to Premium form classes
2. **Database appears empty**: Row counts show 0 — need to run seed/demo data
3. **SmokeTestRunner.cs**: 11,258 lines — very large single file, could benefit from splitting
4. **_ModernLayout.cshtml**: 975 lines — large layout file
5. **ItemEdit.cshtml**: 1,751 lines — largest page, could benefit from partial extraction
6. **Asset.cshtml**: 1,270 lines — second largest page
7. **Help/Tasks.cshtml.cs**: 1,914 lines — very large code-behind (hard-coded content)
8. **Help/Topic.cshtml.cs**: 994 lines — similar hard-coded content
9. **Fiscal Calendar UI**: Tables exist but limited front-end
10. **Mobile API**: Endpoints exist but no mobile app yet
11. **IoT Integration**: Schema fields exist on Asset model but no IoT connectivity

---

## 20. SMOKE TEST SUMMARY

```
Total: 77 tests
Passed: 77
Failed: 0
Status: ALL PASSING
```

---

## 21. HOW TO RUN

```bash
# Start the application
dotnet run --project Abs.FixedAssets.csproj
# App binds to http://0.0.0.0:5000

# Run smoke tests
curl -s --max-time 120 http://localhost:5000/api/smoke/run

# Environment variables needed:
# PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE (PostgreSQL)
# OPENAI_API_KEY (for AI Assistant - managed via Replit integration)
```

---

## 22. KEY INTEGRATION POINTS

| Integration | Method | Notes |
|-------------|--------|-------|
| PostgreSQL | EF Core via Npgsql | Replit-managed Neon database |
| OpenAI | Replit AI Integration | Chat assistant, managed API key |
| Webhook Outbound | HTTP POST with HMAC-SHA256 | Outbox pattern + background dispatcher |
| Webhook Inbound | POST /api/webhooks/inbound | Signature verification + idempotency |
| Barcode Scanner | REST API | Code128, QR, DataMatrix, EAN-13, UPC |
| Mobile API | REST endpoints | Prepared for iPhone app |

---

## 23. ROUTE MAP (Key Routes)

| Route | Page | Function |
|-------|------|----------|
| / | Index.cshtml | Dashboard |
| /Assets | Assets/Index | Asset list |
| /Assets/Asset/{id} | Assets/Asset | Asset detail |
| /Assets/Transfer/{id} | Assets/Transfer | Transfer asset |
| /Assets/Dispose/{id} | Assets/Dispose | Dispose asset |
| /Books | Books/Index | Book list |
| /Books/Edit/{id} | Books/Edit | Edit book |
| /Maintenance | Maintenance/Index | Maintenance list |
| /Maintenance/Details/{id} | Maintenance/Details | WO detail |
| /Maintenance/WorkRequests | WorkRequests/Index | WR list |
| /Maintenance/WorkRequests/Create | WorkRequests/Create | New WR |
| /Materials/Items | Materials/Items | Item list |
| /Materials/ItemEdit/{id} | Materials/ItemEdit | Item edit |
| /WorkOrders/Details/{id} | WorkOrders/Details | WO detail |
| /Purchasing | Purchasing/Index | PO list |
| /Purchasing/Details/{id} | Purchasing/Details | PO detail |
| /Reports | Reports/Index | Report hub |
| /CIP | CIP/Index | CIP projects |
| /CCA | CCA/Index | Canadian CCA |
| /UsTax | UsTax/Index | US tax |
| /Admin | Admin/Index | Admin dashboard |
| /Admin/Sites | Admin/Sites | Site management |
| /Admin/Locations | Admin/Locations | Locations |
| /Admin/Vendors | Admin/Vendors | Vendors |
| /Admin/Companies | Admin/Companies | Companies |
| /Admin/Users | Admin/Users | User management |
| /Admin/SystemSettings | Admin/SystemSettings | System config |
| /Admin/PMTemplates | Admin/PMTemplates | PM templates |
| /Admin/PMSchedules | Admin/PMSchedules | PM schedules |
| /Admin/SmokeTests | Admin/SmokeTests | Smoke test runner |
| /Admin/Integrations | Integrations/Index | Integration hub |
| /Admin/Webhooks | Webhooks/Index | Webhook management |
| /AI | AI/Index | AI assistant |
| /Help | Help/Index | Help center |
| /Account/Login | Account/Login | Login page |

---

## 24. DATABASE RELATIONSHIP SUMMARY

### Core Entity Relationships
- **Tenant** → Companies → Sites → Locations → Assets
- **Asset** → AssetBookSettings → Books → DepreciationPolicies
- **Asset** → AssetTransfers, CapitalImprovements, PartialDisposals
- **Asset** → MaintenanceEvents (Work Orders) → WorkOrderOperations
- **PMTemplate** → PMTemplateRevisions → PMTemplateRevisionOperations
- **PMTemplate** → PMTemplateAssets → PMSchedules → PMOccurrences
- **Item** → ItemRevisions, ItemManufacturerParts, VendorItemParts
- **Item** → ItemAlternates, ItemSupersessions, ItemApprovedVendors
- **Vendor** → VendorItemParts, VendorInvoices, PurchaseOrders
- **WorkRequest** → MaintenanceEvents (conversion)
- **PurchaseRequisition** → PurchaseRequisitionLines → PurchaseOrders
- **IntegrationEndpoint** → WebhookSubscriptions → OutboxEvents
- **IntegrationEndpoint** → InboundEvents → IntegrationMappings
- **GlAccount** → BookGlAccounts → Books
- **Company** → FiscalYears → FiscalPeriods

### Key Foreign Key Count by Table
| Table | FK Count |
|-------|----------|
| Assets | 5+ (Company, Site, Location, Category, Policy) |
| MaintenanceEvents | 8+ (Asset, Site, Company, Technician, etc.) |
| WorkOrderOperations | 3 (MaintenanceEvent, Craft, Technician) |
| Items | 3 (Category, Company, Manufacturer) |
| PurchaseOrders | 3 (Vendor, Company, Site) |

---

## 25. SCRIPTS & TOOLS

| File | Purpose |
|------|---------|
| scripts/SchemaSnapshot.sh | Database schema export script |

---

## 26. CONFIGURATION FILES

| File | Purpose |
|------|---------|
| Abs.FixedAssets.csproj | .NET 9.0 project with 6 NuGet packages |
| appsettings.json | Base config: AllowedHosts, TenantSettings |
| appsettings.Development.json | Dev environment overrides |
| Program.cs | 305-line startup: DI, EF, auth, middleware, routing |

---

## 27. DESIGN SYSTEM SUMMARY

### Token Architecture (tokens.css)
- Brand colors (primary blue #2563eb, secondary, accent)
- Surface colors (page, card, sidebar)
- Border styles
- Text colors (primary, secondary, muted)
- Semantic colors (success, warning, danger, info)
- Typography scale
- Spacing scale
- Border radius scale
- Shadow scale

### Component Library
- **_ScreenHeader**: Unified page headers with breadcrumbs, KPIs, actions
- **_TabNav**: Tab navigation with link and panel modes
- **_FormField**: Premium form field wrapper
- **_SectionCard**: Grouped content sections
- **_EmptyState**: Empty state displays
- **_KpiStrip / _QuickStat**: Dashboard KPI components
- **_BackLink**: Return navigation with security validation
- **enhanced-grid.js**: Premium DataGrid with sort, filter, export
- **modal.js**: Global modal system
- **sidebar-nav.js**: Adaptive sidebar navigation

### Premium Form Classes
- `.form-field` — Field wrapper
- `.form-field__label` — Label styling
- `.form-field__input` — Input/select/textarea styling
- `.form-field--full` — Full-width variant
- `.form-section` / `.form-section__title` / `.form-section__body` — Section grouping
- `.form-grid` — Grid layout
- `.form-actions` — Action button area

---

## END OF AUDIT REPORT
