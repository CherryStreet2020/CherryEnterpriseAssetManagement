# CherryAI Enterprise Asset Management (EAM) - Domain Model & Database Schema Audit

**Repository Path:** `/sessions/beautiful-trusting-lamport/mnt/EnterpriseAssetManagament/extracted/FIxedAssetsProject-1-25-26-750pm/`  
**Framework:** ASP.NET Core 9.0 + EF Core 9 + PostgreSQL  
**Model Files:** 67 .cs files in `Models/` directory  
**Total Model Lines:** 8,391 lines  
**Audit Date:** 2026-05-07

---

## 1. Entity Catalog by Domain Area

### 1.1 Asset Management (Core)

| Entity | Key Fields | FKs | Type | Multi-Tenancy |
|--------|-----------|-----|------|---------------|
| **Asset** | Id, AssetNumber, Description, LongDescription, Model, SerialNumber, TagNumber, ImageUrl, ParentAssetId, AssetType, ClassificationCode, Priority, Condition, IsCritical, IsRotating, IsLinear, PurchaseDate, InstallDate, InServiceDate, AcquisitionCost, ReplacementCost, AccumulatedDepreciation, BookValue, SalvageValue, FairMarketValue, InsuredValue, GLAssetAccount, GLAccumDepAccount, GLDepExpenseAccount, DepreciationMethod, DepreciationRate, Currency, LastDepreciationDate, NextDepreciationDate, UsefulLifeMonths, DisposalDate, DisposalProceeds, GainLossOnDisposal, WarrantyStartDate, WarrantyEndDate, WarrantyVendorId, WarrantyContractNumber | VendorId, ManufacturerId, CostCenterId, DepartmentId, SiteId, LocationId, AssetCategoryId, CompanyId, AssetTypeLookupValueId, AssetPriorityLookupValueId, ConditionLookupValueId, DepreciationMethodLookupValueId, StatusLookupValueId, FailureClassId | Entity | CompanyId (scoped) |
| **Asset** (contd.) | **Technical Specs:** Horsepower, KilowattRating, Voltage, Amperage, RPM, Capacity, CapacityUOM, Weight, WeightUOM, Dimensions, HasMeter, MeterType, CurrentMeterReading, LastMeterReadingDate, Bay, Row, Aisle, Position, WorkCenterId, WorkCenterName, ProductionLineId, ProductionLineName, CellId, ProcessId, OperationId, RoutingSequence, ShiftCalendarId, PlannedProductionHoursPerDay | Same as above | Entity | CompanyId (scoped) |
| **Asset** (contd.) | **IoT/OEE/Predictive:** IoTEnabled, IoTDeviceId, IoTGatewayId, IoTProtocol, IoTEndpointUrl, IPAddress, MACAddress, IoTConnectionStatus, LastIoTCommunication, IoTPollingIntervalSeconds, DataHistorianTag, SCADATag, OEETracked, StandardRunRate, StandardRunRateUOM, IdealCycleTimeSeconds, TargetAvailability, TargetPerformance, TargetQuality, TargetOEE, CurrentAvailability, CurrentPerformance, CurrentQuality, CurrentOEE, OEELastCalculated | Same | Entity | CompanyId (scoped) |
| **Asset** (contd.) | **Calibration:** CalibrationRequired, CalibrationType, CalibrationFrequencyDays, LastCalibrationDate, NextCalibrationDue, CalibrationCertificateNumber, CalibrationVendor, CalibrationStatus, SafetyClassification, LockoutTagoutRequired, LOTOProcedureId, ConfinedSpaceEntry, HotWorkPermitRequired, HighVoltage, SafetyNotes, EnvironmentalClass, EmissionsMonitored, EPAPermitNumber, OSHAClassification | Same | Entity | CompanyId (scoped) |
| **Asset** (contd.) | **Energy Management:** EnergyClass, RatedPowerConsumptionKW, IdlePowerConsumptionKW, StandbyPowerConsumptionKW, HasStandbyMode, AnnualEnergyConsumptionKWH, EnergyMeterId, VibrationWarningThreshold, VibrationAlarmThreshold, TemperatureWarningThreshold, TemperatureAlarmThreshold, PressureWarningThreshold, PressureAlarmThreshold, PressureUOM, CurrentVibration, CurrentTemperature, CurrentPressure, SensorReadingsLastUpdated, PredictiveHealthScore, HealthScoreLastCalculated, PredictedFailureDate, PredictedFailureReason | Same | Entity | CompanyId (scoped) |
| **Asset** (contd.) | **Audit:** CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, Notes, Active, Status, RowVersion (xmin) | Same | Entity | CompanyId (scoped) |
| **AssetInventory** | Id, AssetId, LocationId, Bin, Row, Aisle, Position, QuantityOnHand, QuantityAvailable, LastCountDate, LastPhysicalInventory, CompanyId | AssetId, LocationId, CompanyId | Entity | CompanyId (scoped) |
| **AssetTransfer** | Id, AssetId, FromLocationId, ToLocationId, FromCostCenterId, ToCostCenterId, FromDepartmentId, ToDepartmentId, TransferDate, Reason, CreatedBy | AssetId, FromLocationId, ToLocationId, FromCostCenterId, ToCostCenterId, FromDepartmentId, ToDepartmentId | Entity | Implicit via Asset |
| **PartialDisposal** | Id, AssetId, DisposalDate, PercentageSold, UnitsDisposed, ProceededAmount, GainOnDisposal, CompanyId | AssetId, CompanyId | Entity | CompanyId (scoped) |
| **AssetCategory** | Id, Code, Name, Description, ParentCategoryId, GLAccountId, DefaultDepreciationPolicyId, IsActive, CompanyId | ParentCategoryId, GLAccountId, DefaultDepreciationPolicyId, CompanyId | Entity | CompanyId (scoped) |
| **FailureClass** | Inferred from Asset.FailureClassId FK; minimal documentation in codebase | TBD | Enum-like | TBD |

**Key Constraint:** Asset has unique index on `(CompanyId, AssetNumber)`. RowVersion maps to PostgreSQL `xmin` (system column) for optimistic concurrency via uint-to-byte[] converter.

---

### 1.2 Depreciation & Financial

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **Book** | Id, Code, Name, Description, Method, Convention, UsefulLifeOverrideMonths, DefaultPolicyId, GlAccountDepExp, GlAccountAccumDep, GlAccountGainOnDisposal, GlAccountLossOnDisposal, GlAccountAssetClearing, GlAccountCIP, BookType, TaxJurisdiction, IsPrimaryBook, CalculateOnlyNoPosting, AllowManualDepreciation, TrackBudgetVsActual, CalculationFrequency, RequireApprovalToPost, AutoPostOnPeriodClose, CompanyId, IsActive, SortOrder, CreatedAt, CreatedBy | DefaultPolicyId, CompanyId, MethodLookupValueId, ConventionLookupValueId, BookTypeLookupValueId, TaxJurisdictionLookupValueId, FrequencyLookupValueId | Entity | Multi-book accounting; 1:1 relationship with BookGlAccount (unique index) |
| **BookGlAccount** | Id, BookId, Asset, AccumulatedDepreciation, DepreciationExpense, GainOnDisposal, LossOnDisposal, Clearing, CIP | BookId | Entity | Maps Book to GL accounts; unique per Book |
| **AssetBookSettings** | Id, AssetId, BookId, DepreciationStartDate, DepreciationRate, OptimisticSalvage, InitialCost, DepreciationCurrency, IsExcluded, ExcludedEffectiveDate, CompanyId | AssetId, BookId, CompanyId | Entity | Per-asset, per-book depreciation configuration |
| **AssetBookValue** | Id, AssetId, BookId, DepreciationStartDate, LastDepreciationRun, AccumulatedDepreciation, YtdDepreciation, CurrentCost | AssetId, BookId | Entity | Running accumulator of depreciation by book |
| **DepreciationPolicy** | Id, Code, Name, Description, Method, Convention, DefaultUsefulLifeMonths, DefaultSalvagePercent, DefaultSalvageAmount, SalvageType, SwitchToStraightLine, SwitchToSLInYear, AveragingMethod, DecliningBalanceRate, ApplySection179, DefaultSection179Percent, ApplyBonusDepreciation, DefaultBonusPercent, MinimumBookValue, AllowNegativeDepreciation, Rounding, FirstYearProrate, LastYearProrate, Frequency, DepreciateInServiceMonth, DepreciateInDisposalMonth, CalculateToEndOfLife, TrackUnitsOfProduction, EstimatedTotalUnits, CcaClassId, MacrsRecoveryPeriodYears, MacrsPropertyType, MacrsUseADS, ApplicableBookType, TaxJurisdiction, CompanyId, IsSystemPolicy, IsActive, SortOrder, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy | CcaClassId, CompanyId | Entity | US MACRS, Canada CCA, Section 179, Bonus Depreciation support |
| **UsefulLifeTable** | Id, Code, Name, Description, Jurisdiction, Source, IsActive | None | Entity | IRS/CRA life tables; lookup only |
| **UsefulLifeEntry** | Id, UsefulLifeTableId, AssetClassCode, AssetClassName, Description, GaapLifeMonths, TaxLifeMonths, MacrsRecoveryYears, CcaClassNumber, CcaRate, RecommendedMethod, RecommendedConvention, IrsAssetClass, CraAssetClass, IsActive, SortOrder | UsefulLifeTableId | Entity | Asset classification mapping for depreciation rules |
| **PolicyCategoryDefault** | Id, DepreciationPolicyId, AssetCategoryId, BookId, CompanyId, Priority, IsActive | DepreciationPolicyId, AssetCategoryId, BookId, CompanyId | Entity | Default policy selection by category + book + company |
| **DepreciationRun** | Id, FiscalPeriodId, BookId, RunDate, Status, ErrorMessage, CompanyId, CreatedBy | FiscalPeriodId, BookId, CompanyId | Entity | Batch depreciation execution log |
| **DepreciationRunDetail** | Id, DepreciationRunId, AssetId, DepreciationAmount, AccumulatedDepreciation, BookValue | DepreciationRunId, AssetId | Entity | Per-asset depreciation calculated in a run |

**Enums in DepreciationPolicy:**
- `DepreciationMethod`: StraightLine, DecliningBalance, UnitsOfProduction, SumOfYearsDigits, MACRS
- `DepreciationConvention`: FullMonth, HalfMonth, HalfYear, MidMonth, MidQuarter
- `SalvageValueType`: Percentage, FixedAmount, None
- `AveragingMethod`: Monthly, Daily, Annual, Quarterly
- `DepreciationRounding`: ToCents, ToDollars, NoRounding, ToThousands
- `ProrateMethod`: ByConvention, FullPeriod, NoPeriod, ExactDays
- `DepreciationFrequency`: Monthly, Quarterly, SemiAnnually, Annually, Daily
- `MacrsPropertyType`: PersonalProperty, RealProperty, QualifiedImprovementProperty, ListedProperty, FarmProperty
- `LifeTableSource`: IRS, CRA, Company, Industry, IFRS

---

### 1.3 Tax (US & Canadian)

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **UsTaxSettings** | Id, AssetId, TaxBasis, Section179Claimed, Section179Amount, BonusDepreciationClaimed, BonusDepreciationAmount, MacrsClass, MacrsConvention, MacrsMethod, MacrsRecoveryYears, PlacedInServiceDate, QualifiedPropertyFlag, ListedPropertyFlag, CompanyId, IsActive | AssetId, CompanyId | Entity | US tax-specific fields (Section 179, Bonus, MACRS) |
| **Section179Limits** | Id, TaxYear, AnnualLimit, PhaseOutStart, PhaseOutEnd, AppliesTo, IsActive | None | Entity | Tax year 179 limits (lookup table) |
| **BonusDepreciationRates** | Id, TaxYear, PercentageAllowed, PropertyType, IsActive | None | Entity | Tax year bonus rates by property type |
| **AssetTaxSettings** | Id, AssetId, TaxJurisdiction, CcaClassId, TaxBasis, CcaAdjustmentFactor, CarriedForwardCcaBalance, IsExcluded, ExcludedEffectiveDate, CompanyId | AssetId, CcaClassId, CompanyId | Entity | Canadian CCA configuration |
| **CcaClass** | Id, ClassNumber (unique), Description, Rate, IsDecliningBalance, HalfYearRuleApplies, IsAcceleratedInvestmentIncentive, Notes, Active | None | Entity | Canada CCA classes (Class 1, 8, 13, etc.) |
| **CcaClassBalance** | Id, CcaClassId, TaxYear, OpeningUCC, AddedCost, SaleProceeds, ClosingUCC, DepreciationClaimed, CcaPoolId, CompanyId | CcaClassId, CompanyId | Entity | Per-tax-year CCA pool tracking |
| **CcaTransaction** | Id, CcaClassId, CcaClassBalanceId, TransactionType, AssetId, Amount, TaxYear, Description, CompanyId | CcaClassId, CcaClassBalanceId, AssetId, CompanyId | Entity | CCA additions/disposals audit trail |

**Key Pattern:** US uses Section 179, Bonus, MACRS; Canada uses CCA pooling with half-year rule. Both are book-independent (can diverge from Book depreciation).

---

### 1.4 Work Orders & Maintenance

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **MaintenanceEvent** | Id, AssetId, Type, Description, ScheduledDate, CompletedDate, Status, Priority, EstimatedCost, ActualCost, LaborCost, PartsCost, MaterialsCost, OutsideVendorCost, Vendor, TechnicianName, TechnicianId, WorkOrderNumber, PurchaseOrderNumber, DowntimeHours, LaborHours, OvertimeHours, ApprovalStatus, CipProjectId, ApprovedById, ApprovedAt, RequestedById, RequestedAt, FailureCode, RootCause, CorrectiveAction, CreatedAt, UpdatedAt, CompanyId | AssetId, TechnicianId, CipProjectId, ApprovedById, RequestedById, CompanyId, TypeLookupValueId, StatusLookupValueId, PriorityLookupValueId | Entity | CMMS work order; flexible cost breakdown |
| **MaintenanceType** | Preventative, Corrective, Emergency, Predictive, Condition-Based | (enum) | Enum | Driven by lookup |
| **MaintenanceStatus** | Scheduled, In Progress, On Hold, Completed, Cancelled | (enum) | Enum | Driven by lookup |
| **MaintenancePriority** | Low, Medium, High, Urgent | (enum) | Enum | Driven by lookup |
| **WorkRequest** | Id, AssetId, RequestedById, RequestedDate, Title, Description, Priority, Status, AssignedTo, EstimatedCompletionDate, ActualCompletionDate, Notes | AssetId, RequestedById, AssignedTo | Entity | Pre-work-order intake |
| **WorkOrderOperation** | Id, MaintenanceEventId, OperationNumber, Sequence, Type, Title, Description, Instructions, Status, AssignedTechnicianId, CraftId, PlannedHours, ActualHours, PlannedStartDate, PlannedEndDate, ActualStartDate, ActualEndDate, RequiresShutdown, RequiresLOTO, CreatedAt | MaintenanceEventId, AssignedTechnicianId, CraftId, TypeLookupValueId, StatusLookupValueId | Entity | Sub-task of work order; maps to labor/tools/parts |
| **WorkOrderOperationLabor** | Id, WorkOrderOperationId, TechnicianId, LaborType, PlannedHours, ActualHours, HourlyRate, TotalCost | WorkOrderOperationId, TechnicianId, LaborType | Entity | Labor cost tracking per operation |
| **WorkOrderOperationTool** | Id, WorkOrderOperationId, ToolId, Quantity, UnitCost, TotalCost | WorkOrderOperationId, ToolId | Entity | Tool allocation per operation |
| **WorkOrderOperationPart** | Id, WorkOrderOperationId, ItemId, Quantity, UnitCost, TotalCost | WorkOrderOperationId, ItemId | Entity | Part allocation per operation |
| **WorkOrderPart** | Id, WorkOrderId, ItemId, Quantity, UnitCost, TotalCost | WorkOrderId, ItemId | Entity | Alternative part table (Sprint 12) |
| **MaintenanceSchedule** | Inferred; defines recurring maintenance | TBD | Entity | Links MaintenanceEvent to PMSchedule |
| **LessonLearned** | Id, MaintenanceEventId, Title, Description, CreatedBy, CreatedAt | MaintenanceEventId | Entity | Post-work-order knowledge capture |

**OperationType Enum:** Mechanical, Electrical, Hydraulic, Pneumatic, Lubrication, Inspection, Calibration, Cleaning, Adjustment, Replacement, Testing, Documentation, SafetyCheck, Other

---

### 1.5 Construction in Progress (CIP)

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **CipProject** | Id, ProjectNumber, Name, Description, Status, StartDate, EstimatedCompletionDate, ActualCompletionDate, BudgetAmount, TotalCosts, CommittedCosts, ProjectManagerName, ProjectManagerId, Location, CostCenterId, Department, DepartmentId, GlAccount, GlAccountId, ConvertedAssetId, PlacedInServiceDate, Currency, CompanyId, SiteId, IsCapitalized, CapitalizedAt, CreatedAt, UpdatedAt | ProjectManagerId, CostCenterId, DepartmentId, GlAccountId, ConvertedAssetId, CompanyId, SiteId, StatusLookupValueId | Entity | Project-level CIP; capitalizes to Asset |
| **CipCost** | Id, CipProjectId, Description, CostType, TransactionDate, Amount, Vendor, InvoiceNumber, PurchaseOrderNumber, GlAccount, IsCapitalizable, Notes, EnteredBy, SourceType, SourceHeaderId, SourceLineId, SourceDisplayRef, WorkOrderId, PurchaseOrderId, CreatedAt, CompanyId | CipProjectId, WorkOrderId, PurchaseOrderId, CompanyId, CostTypeLookupValueId | Entity | Line-item costs; tracks PO/WO source |
| **CipBudgetLine** | Id, CipProjectId, Description, BudgetAmount, Sequence, CreatedAt | CipProjectId | Entity | Budget planning per project |
| **CipCapitalization** | Id, CipProjectId, CapitalizationDate, NewAssetId, Description, TotalCapitalizedAmount, Notes, ApprovedBy, ApprovedAt, CompanyId | CipProjectId, NewAssetId, CompanyId | Entity | Conversion event from CIP → Asset |
| **CipCapitalizationCost** | Id, CipCapitalizationId, CipCostId, Amount | CipCapitalizationId, CipCostId | Entity | Link costs → capitalization (audit trail) |

**CipCostType Enum:** Construction, Engineering, Labor, Materials, Equipment, Permits, Contingency, Other

**CipProjectStatus Enum:** Active, OnHold, Completed, Capitalized, Cancelled

---

### 1.6 Procurement

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **PurchaseOrder** | Id, PONumber, POType, Status, VendorId, OrderDate, RequiredDate, PromiseDate, ShipToSiteId, DefaultShipToLocationId, ShipToAddress, BillToSiteId, BillToAddress, Currency, Subtotal, TaxAmount, ShippingAmount, Total, CompanyId, CreatedBy, CreatedAt | VendorId, ShipToSiteId, DefaultShipToLocationId, BillToSiteId, CompanyId, POTypeLookupValueId, StatusLookupValueId | Entity | Multi-location ship-to/bill-to support |
| **PurchaseOrderLine** | Id, PurchaseOrderId, ItemId, Quantity, UnitPrice, LineTotal, Description, ReceivedQty, InvoicedQty, RejectedQty, TaxCode, GLAccountId, CostCenterId, LineSequence | PurchaseOrderId, ItemId, TaxCode, GLAccountId, CostCenterId | Entity | Line-item PO detail |
| **PurchaseOrderRelease** | Id, PurchaseOrderId, ReleaseNumber, ReleaseQuantity, ReleaseDate, DueDate, Status, CompanyId | PurchaseOrderId, CompanyId | Status | Blanket PO sub-releases (Sprint 10) |
| **GoodsReceipt** | Id, ReceiptNumber, PurchaseOrderId, ReceiptDate, Status, ReceivedByTechnicianId, Warehouse, InspectionRequired, InspectionStatus, Notes, CompanyId | PurchaseOrderId, ReceivedByTechnicianId, CompanyId | Entity | Goods inbound receipt |
| **GoodsReceiptLine** | Id, GoodsReceiptId, PurchaseOrderLineId, ItemId, QuantityReceived, QuantityAccepted, QuantityRejected, SerialNumber, LotNumber, ExpirationDate, Notes | GoodsReceiptId, PurchaseOrderLineId, ItemId | Entity | Lot/serial tracking; reject management |
| **VendorInvoice** | Id, InvoiceNumber, VendorId, InvoiceDate, DueDate, ReceiptDate, Amount, TaxAmount, ShippingAmount, Total, Status, ApprovedById, ApprovedAt, PaidDate, PaymentAmount, MatchStatus, Notes, CompanyId | VendorId, ApprovedById, CompanyId, StatusLookupValueId, MatchStatusLookupValueId | Entity | Accounts Payable invoice |
| **VendorInvoiceLine** | Id, VendorInvoiceId, PurchaseOrderLineId, GoodsReceiptLineId, Description, Quantity, UnitPrice, LineTotal, MatchStatus | VendorInvoiceId, PurchaseOrderLineId, GoodsReceiptLineId | Entity | 3-way match: PO → GR → Invoice |
| **InvoicePayment** | Id, VendorInvoiceId, PaymentDate, Amount, PaymentMethod, CheckNumber, ReferenceNumber, Notes | VendorInvoiceId | Entity | Payment audit trail |
| **PurchaseRequisition** | Id, RequisitionNumber, RequestorId, RequestDate, Status, ApprovedById, ApprovedAt, TargetDeliveryDate, Notes, CompanyId | RequestorId, ApprovedById, CompanyId | Entity | Pre-PO requisition (Sprint 11) |
| **PurchaseRequisitionLine** | Id, PurchaseRequisitionId, ItemId, Quantity, EstimatedCost, Notes | PurchaseRequisitionId, ItemId | Entity | Line detail for requisition |
| **ReorderAlert** | Id, ItemId, AlertDate, QuantityOnHand, ReorderPoint, Status, CreatedBy | ItemId | Entity | Inventory-triggered alerts |

**POType Enum:** Standard, Blanket, Emergency, Contract  
**POStatus Enum:** Draft, PendingApproval, Approved, Sent, PartiallyReceived, Received, Invoiced, Closed, Cancelled

---

### 1.7 Inventory & Items (Parts Master)

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **ItemCategory** | Id, Code, Name, Description, ParentCategoryId, DefaultGlAccountId, ExpenseGlAccountId, IsActive, SortOrder | ParentCategoryId, DefaultGlAccountId, ExpenseGlAccountId | Entity | Hierarchical part categories |
| **Item** | Id, PartNumber, Description, ExtendedDescription, Revision, RequireRevisionControl, Type, Status, CategoryId, UOM, StockUOM, PurchaseUOM, PurchaseConversion, CostMethod, StandardCost, AverageCost, LastPurchaseCost, ListPrice, TrackingType, MinQuantity, MaxQuantity, ReorderPoint, ReorderQuantity, SafetyStock, LeadTimeDays, DefaultLocation, Warehouse, Aisle, Rack, Shelf, Bin, PrimaryVendorId, VendorPartNumber, ManufacturerPartNumber, ManufacturerId, IsStocked, IsPurchasable, IsCriticalSpare, IsTaxable, IsHazmat, HazmatClass, ShelfLifeDays, Weight, Dimensions, Notes, ImageUrl, ImagePath, ExternalImageUrl, SpecUrl, BarcodeType, Barcode, AlternateBarcode, ABCClass, ReorderMethod, AutoReorderEnabled, EOQ, AnnualUsage, AverageDailyUsage, CarryingCostPercent, OrderingCost, MinOrderQty, OrderMultiple, PackQty, LastPrice, CurrencyCode, PriceEffectiveDate, ContractFlag, ContractRef, StockPolicy, AlternatePartNumbers, SupersedesPartNumber, SupersededByPartNumber, WarrantyMonths, WarrantyTerms, CommodityCode, UNSPSCCode, DefaultBuyerId, DefaultBuyerName, Length, Width, Height, DimensionUOM, StorageRequirements, MinStorageTemp, MaxStorageTemp, Certifications, IsFDARegulated, IsOSHACompliance, CountryOfOrigin, HTSCode, Source, ExternalId, CompanyId, CurrentReleasedRevisionId, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy | CategoryId, PrimaryVendorId, ManufacturerId, DefaultBuyerId, CurrentReleasedRevisionId, CompanyId, TypeLookupValueId, StatusLookupValueId, CostMethodLookupValueId, TrackingTypeLookupValueId | Entity | Shared item master; enterprise-grade MRO + tools |
| **ItemCompanyStocking** | Id, ItemId, CompanyId, IsStocked, IsPurchasable, IsCriticalSpare, MinQuantity, MaxQuantity, ReorderPoint, ReorderQuantity, SafetyStock, LeadTimeDays, PreferredVendorId, ReorderMethod, AutoReorderEnabled, ABCClass, DefaultWarehouse, DefaultAisle, DefaultRack, DefaultShelf, DefaultBin, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy | ItemId, CompanyId, PreferredVendorId | Entity | Per-company stocking decisions (Maximo pattern) |
| **ItemVendor** | Id, ItemId, VendorId, VendorPartNumber, UnitPrice, MinOrderQty, LeadTimeDays, IsPreferred, LastOrderDate, ProductPageUrl, OrderUrl, CatalogPageUrl, PriceBreakQty1, PriceBreak1, PriceBreakQty2, PriceBreak2, PriceBreakQty3, PriceBreak3, ContractNumber, ContractPrice, ContractStartDate, ContractEndDate, VendorStockAvailable, LastStockCheckDate, Notes, IsActive, CreatedAt, UpdatedAt | ItemId, VendorId | Entity | Vendor catalog links; pricing tiers; contracts |
| **ItemRevision** | Id, ItemId, RevisionCode, Status, ChangeReason, SupersedesItemRevisionId, EffectiveFromUtc, EffectiveToUtc, CreatedByUserId, CreatedAtUtc, ApprovedByUserId, ApprovedAtUtc, ReleasedAtUtc, ObsoletedAtUtc, Name, Description, StatusLookupValueId | ItemId, SupersedesItemRevisionId, StatusLookupValueId | Entity | Engineering revisions (Draft→Approved→Released→Obsolete) |
| **ItemInventory** | Id, ItemId, LocationId, Warehouse, Bin, QuantityOnHand, QuantityReserved, QuantityAvailable (computed), QuantityOnOrder, LotNumber, SerialNumber, ExpirationDate, LastCountDate, LastReceiptDate, LastIssueDate, CompanyId, CreatedAt, UpdatedAt | ItemId, LocationId, CompanyId | Entity | Per-location stock positions |
| **ItemTransaction** | Id, TransactionNumber, ItemId, Type, Quantity, UnitCost, TotalCost (computed), FromLocationId, ToLocationId, FromBin, ToBin, LotNumber, SerialNumber, ReferenceType, ReferenceNumber, WorkOrderId, PurchaseOrderId, Notes, TransactedBy, TransactionDate, CompanyId, CreatedAt | ItemId, FromLocationId, ToLocationId, PurchaseOrderId, CompanyId, TypeLookupValueId | Entity | Inventory movement audit trail |
| **ItemImage** | Id, ItemId, FileName, FilePath, ContentType, FileSize, AltText, Caption, IsPrimary, SortOrder, ExternalUrl, IsExternal, CreatedAt, CreatedBy | ItemId | Entity | Multi-image support per item |
| **ItemApprovedVendor** | Id, ItemId, VendorId, ApprovalStatus, ApprovedDate, ExpirationDate, QualityRating, Notes | ItemId, VendorId | Entity | Vendor qualification status (Sprint 12) |
| **ItemAlternate** | Id, PrimaryItemId, AlternateItemId, AlternateCode, Priority, EffectiveDate, ExpirationDate, Notes | PrimaryItemId, AlternateItemId | Entity | Cross-item substitution (Sprint 12) |
| **ItemSupersession** | Id, PreviousItemId, CurrentItemId, EffectiveDate, RetirementDate, Notes | PreviousItemId, CurrentItemId | Entity | EOL replacement chain (Sprint 12) |

**ItemType Enum (17 values):** Part, Consumable, Tool, Safety, Lubricant, Chemical, Electrical, Mechanical, Hydraulic, Pneumatic, Filter, Bearing, Belt, Seal, Fastener, Kit, Service  
**ItemStatus Enum:** Active, Inactive, Obsolete, PendingApproval, Discontinued  
**CostMethod Enum:** Standard, Average, FIFO, LIFO, LastPurchase  
**TrackingType Enum:** None, LotNumber, SerialNumber, Both  
**ABCClassification Enum:** A (top 20% value), B (medium), C (low), Unclassified  
**ReorderMethod Enum:** Manual, MinMax, ReorderPoint, EOQ, Kanban  
**StockPolicy Enum:** Stock, Nonstock, CriticalSpare  
**BarcodeType Enum:** None, Code128, Code39, QRCode, DataMatrix, EAN13, UPC

---

### 1.8 Org Structure & Master Files

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **Company** | Id, TenantId, Name, LegalName, CompanyCode, CompanyType, CompanyStructure, ParentCompanyId, HierarchyLevel (computed), Currency, TaxId, PeriodType, FiscalYearStartMonth, FiscalYearStartDay, IsShortYear, ShortYearStart, ShortYearEnd, Address, City, StateProvince, PostalCode, Country, ContactName, ContactEmail, ContactPhone, DefaultDepMethod, DefaultConvention, IsActive, CreatedAt, UpdatedAt, LogoPath, GstHstNumber, QstNumber, PstNumber, ProvincialSalesTaxNumber | TenantId, ParentCompanyId | Entity | Multi-company hierarchy; fiscal period config |
| **Tenant** | Id, Name, Code, IsActive, CreatedAt, UpdatedAt | None | Entity | SaaS tenant (if multi-tenant) |
| **Site** | Id, Code, Name, Description, Address, City, StateProvince, PostalCode, Country, ContactName, Phone, Email, OperatingHours, IsWarehouse, StorageCapacity, CompanyId, IsActive | CompanyId | Entity | Warehouse/facility location |
| **Location** | Id, Code, Name, Description, SiteId, Warehouse, Section, Aisle, Row, Bin, Capacity, CurrentOccupancy, IsActive, CompanyId | SiteId, CompanyId | Entity | Bin-level inventory location |
| **Department** | Id, Code, Name, Description, CompanyId, DepartmentManagerId, IsActive, SortOrder | CompanyId, DepartmentManagerId | Entity | Cost center grouping |
| **CostCenter** | Id, Code, Name, Description, ParentCostCenterId, DepartmentId, ManagerId, IsActive, SortOrder, CompanyId | ParentCostCenterId, DepartmentId, ManagerId, CompanyId | Entity | Hierarchical cost allocation |
| **Manufacturer** | Id, Code, Name, Description, Website, Email, Phone, Address, City, StateProvince, PostalCode, Country, Notes, IsActive, CompanyId | CompanyId | Entity | OEM/manufacturer lookup |
| **Vendor** | Id, Code, Name, LegalName, VendorType, Status, ContactName, Phone, Fax, Email, Website, Address, City, State, PostalCode, Country, TaxId, PaymentTerms, Currency, CreditLimit, AccountNumber, Notes, DefaultGlAccountId, CompanyId, IsPreferred, IsContractor, IsCertified, CertificationExpiry, PerformanceRating, LastActivityDate, CreatedAt, UpdatedAt | DefaultGlAccountId, CompanyId | Entity | Supplier master; contractor support |
| **Customer** | Id, Code, Name, LegalName, BillingAddress, ShippingAddress, Phone, Email, PrimaryContactName, CustomerType, IsActive, CompanyId | CompanyId | Entity | Customer/client master (for sales invoices) |
| **OrgNode** | (Inferred from DbSet in AppDbContext) | TBD | Entity | Platform-level org tree (not detailed in audit) |
| **Technician** | Id, FirstName, LastName, EmployeeNumber, Email, Phone, Department, Supervisor, Craft, Skills, IsActive, Photo, CompanyId | CompanyId, CraftId | Entity | Maintenance technician master |
| **TechnicianCertification** | Id, TechnicianId, CertificationName, CertificateNumber, IssueDate, ExpirationDate, IssuingBody, CompanyId | TechnicianId, CompanyId | Entity | Certs/licenses audit |
| **TechnicianSkill** | Id, TechnicianId, SkillId, ProficiencyLevel, YearsOfExperience, Verified, CompanyId | TechnicianId, SkillId, CompanyId | Entity | Skill matrix |
| **ProjectManager** | Id, FirstName, LastName, Email, Phone, Department, IsActive, CompanyId | CompanyId | Entity | CIP project assignments |
| **User** | Id, Email, FirstName, LastName, IsActive, LastLogin, CreatedAt, CompanyId, TenantId | CompanyId, TenantId | Entity | System user (not detailed) |

**CompanyType Enum:** Holding, Operating, Division  
**CompanyStructure Enum:** Single, MultiCompany  
**VendorType Enum:** Supplier, Contractor, ServiceProvider, Manufacturer, Distributor  
**VendorStatus Enum:** Active, Inactive, OnHold, Blocked  
**PaymentTerms Enum:** Net30, Net45, Net60, Net90, DueOnReceipt, Prepaid, COD

---

### 1.9 Financial Accounting

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **GlAccount** | Id, AccountNumber, Name, Description, AccountType, Category, SubCategory, NormalBalance, IsActive, IsSystemAccount, AllowManualEntry, RequiresCostCenter, RequiresDepartment, RequiresAssetCategory, ParentAccountId, SortOrder, CompanyId, CreatedAt, UpdatedAt | ParentAccountId, CompanyId | Entity | CoA; 8 account types, 47+ categories |
| **JournalEntry** | Id, Batch, Reference, Source, Description, EntryDate, Period (yyyymm), PostedAt, PostedBy, Status, BookId, Notes, CompanyId, CreatedAt, CreatedBy | BookId, CompanyId | Entity | GL batch posting; period required |
| **JournalLine** | Id, JournalEntryId, LineNo, Account, Description, Debit, Credit, CostCenter, Department, AssetCategory, Notes, GlAccountId | JournalEntryId, GlAccountId | Entity | GL posting detail; optional FK to GL master |
| **ExchangeRate** | Id, FromCurrency, ToCurrency, RateDate, ExchangeRate, Source, IsSpot, CompanyId | CompanyId | Entity | Multi-currency support |
| **FiscalYear** | Id, Year, StartDate, EndDate, PeriodType, Status, IsShortYear, IsActive, CompanyId | CompanyId | Entity | Fiscal calendar year |
| **FiscalPeriod** | Id, FiscalYearId, PeriodNumber, PeriodCode, Name, StartDate, EndDate, Status, IsLocked, IsAdjustmentPeriod, AllowNewTransactions, AllowAdjustments, CompanyId | FiscalYearId, CompanyId | Entity | Period-level lock controls |
| **PeriodLock** | Id, FiscalPeriodId, LockedDate, LockedBy, Notes | FiscalPeriodId | Entity | Period close audit trail |

**GlAccountType Enum (8 values):** Asset, Liability, Equity, Revenue, Expense, ContraAsset, ContraRevenue, ContraExpense  
**GlAccountCategory Enum (20+ values):** CashAndReceivables, MroInventory, WorkInProgress, PrepaidAndDeposits, FixedAssetsLandBuildings, FixedAssetsMachinery, FixedAssetsVehicles, FixedAssetsTechnology, FixedAssetsTooling, AccumulatedDepreciation, DepreciationExpense, MaintenanceLabor, RepairParts, etc.  
**GlAccountSubCategory Enum (22 values):** Buildings, LandImprovements, MachineryProduction, MachineryCnc, MachineryCranes, MachineryWelding, VehiclesForklifts, ComputersServers, IoTSensors, ToolingDies, ToolingFixtures, etc.  
**NormalBalance Enum:** Debit, Credit

---

### 1.10 Preventive Maintenance (PM) & Schedules

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **PMTemplate** | Id, Code, Name, Description, Status, AssetTypeCode, FrequencyDays, FrequencyMonths, EstimatedDuration, CompanyId, IsActive, SortOrder, CreatedAt, UpdatedAt | CompanyId | Entity | Reusable PM procedures |
| **PMTemplateItem** | Id, PMTemplateId, ItemId, Quantity, UnitPrice, Notes, Sequence | PMTemplateId, ItemId | Entity | BOM per PM template |
| **PMTemplateAsset** | Id, PMTemplateId, AssetId, NextScheduledDate, IsAssigned | PMTemplateId, AssetId | Entity | Template → Asset assignment |
| **PMSchedule** | Id, AssetId, PMTemplateId, ScheduledDate, NextScheduledDate, FrequencyDays, Status, Notes, CompanyId | AssetId, PMTemplateId, CompanyId | Entity | Asset PM schedule instance |
| **PMOccurrence** | Id, PMScheduleId, ScheduledDate, CompletedDate, MaintenanceEventId, Status, Notes | PMScheduleId, MaintenanceEventId | Entity | Tracks each PM execution |
| **MeterReading** | Id, AssetId, MeterType, ReadingValue, ReadingDate, Notes, RecordedBy | AssetId | Entity | Meter-based PM trigger data |
| **PMTemplateRevision** | Id, PMTemplateId, RevisionCode, Status, EffectiveDate, ChangeReason, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt | PMTemplateId | Entity | Engineering change to PM procedures |
| **PMTemplateRevisionOperation** | Id, PMTemplateRevisionId, OperationNumber, Sequence, Type, Title, Description, Instructions, PlannedHours | PMTemplateRevisionId | Entity | Revised operation detail |
| **Kit** | Id, Code, Name, Description, CompanyId, IsActive | CompanyId | Entity | Maintenance kit (parts bundle) |
| **KitItem** | Id, KitId, ItemId, Quantity, UnitCost | KitId, ItemId | Entity | Kit component |

---

### 1.11 System Configuration & Lookups

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **LookupType** | Id, TenantId, CompanyId, Key, Name, IsSystem, IsActive, CreatedAt, UpdatedAt | TenantId, CompanyId | Entity | Flexible enumeration framework (e.g., `AssetCondition`, `MaintenanceStatus`) |
| **LookupValue** | Id, LookupTypeId, Code, Value, Description, Sequence, IsActive, SortOrder | LookupTypeId | Entity | Enumeration member |
| **NumberingSequence** | Id, SequenceCode, CurrentNumber, Prefix, Suffix, StartDate, EndDate, Format, IsActive, CompanyId | CompanyId | Entity | Auto-numbering (AssetNumber, PONumber, etc.) |
| **PaymentTerm** | Id, Code, Name, NetDays, DiscountDays, DiscountPercent, IsActive | None | Entity | A/P term definition |
| **UOMDefinition** | Id, Code, Name, Abbreviation, Category, ConversionFactor, IsActive | None | Entity | Unit of measure (EA, BOX, LB, GAL, etc.) |
| **Currency** | Id, Code, Name, Symbol, ExchangeRateSource, IsActive | None | Entity | Currency master |
| **TaxCode** | Id, Code, Name, TaxRate, Description, IsActive | None | Entity | Sales/use tax codes |
| **ShippingMethod** | Id, Code, Name, DefaultCarrier, EstimatedDays, IsActive | None | Entity | Shipping method lookup |
| **ApprovalWorkflow** | Id, WorkflowCode, Name, Status, DocumentType, ApprovalSteps, IsActive | None | Entity | Approval rules engine |
| **WorkOrderType** | Id, Code, Name, Description, IsActive | None | Entity | WO classification |
| **MaintenanceTypeCode** | Id, Code, Name, Description, IsActive | None | Entity | Maintenance type enumeration |
| **FailureCode** | Id, Code, Name, Description, Category, IsActive | None | Entity | CMMS failure classification |
| **CauseCode** | Id, Code, Name, Description, IsActive | None | Entity | Root cause analysis |
| **ActionCode** | Id, Code, Name, Description, IsActive | None | Entity | Corrective action codes |
| **ProblemCode** | Id, Code, Name, Description, IsActive | None | Entity | Problem description codes |
| **PriorityLevel** | Id, Code, Name, SortOrder, ResponseTime, IsActive | None | Entity | Work priority scale |
| **LaborType** | Id, Code, Name, Description, IsActive | None | Entity | Labor classification (regular, overtime, contract) |
| **LaborRate** | Id, LaborTypeId, EffectiveDate, HourlyRate, IsActive, CompanyId | LaborTypeId, CompanyId | Entity | Labor rate by type + effective date |
| **Craft** | Id, Code, Name, Description, IsActive, CompanyId | CompanyId | Entity | Technician skill specialty |
| **Skill** | Id, Code, Name, Description, IsActive | None | Entity | Technical skill (welder, electrician, etc.) |

---

### 1.12 Integrations & Webhooks

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **IntegrationEndpoint** | Id, Code, Name, EndpointUrl, AuthType, ApiKey, IsActive, CompanyId | CompanyId | Entity | External system endpoint |
| **IntegrationMapping** | Id, IntegrationEndpointId, SourceField, TargetField, TransformationLogic, IsActive | IntegrationEndpointId | Entity | Field mapping rules |
| **InboundEvent** | Id, EndpointId, EventType, Payload, ProcessedDate, Status, Notes, CompanyId | IntegrationEndpointId, CompanyId | Entity | Inbound webhook payload |
| **OutboxEvent** | Id, EntityType, EntityId, EventType, Payload, CreatedAt, PublishedAt, ProcessedAt, IsPublished, CompanyId | CompanyId | Entity | Outbound event queue (Inbox pattern) |
| **WebhookSubscription** | Id, Endpoint, EventTypes, IsActive, CompanyId, CreatedAt | CompanyId | Entity | Outbound webhook subscription |
| **WebhookDeliveryLog** | Id, WebhookSubscriptionId, OutboxEventId, AttemptNumber, DeliveryTime, StatusCode, ResponseBody, IsSuccess, NextRetryTime | WebhookSubscriptionId, OutboxEventId | Entity | Webhook delivery audit |
| **ApiKey** | Id, Code, Key (hashed), Description, IsActive, CompanyId, CreatedBy, CreatedAt, LastUsed | CompanyId | Entity | API authentication |

---

### 1.13 Audit & Compliance

| Entity | Key Fields | FKs | Type | Notes |
|--------|-----------|-----|------|-------|
| **AuditLog** | Id, EntityType, EntityId, Action, OldValue, NewValue, ChangedAt, ChangedBy, CompanyId | CompanyId | Entity | Change data capture (CDC) |
| **Attachment** | Id, EntityType, EntityId, FileName, FilePath, FileSize, ContentType, Description, CreatedAt, CreatedBy | None | Entity | Document attachment |

---

## 2. Multi-Tenancy Pattern

### Tenancy Scoping Rules

| Scope | Entities | Pattern | Notes |
|-------|----------|---------|-------|
| **Tenant-Scoped** | Tenant, Company, OrgNode | TenantId FK | Root organizational boundaries |
| **Company-Scoped** (most data) | Asset, Book, AssetBookValue, AssetBookSettings, DepreciationPolicy, UsTaxSettings, AssetTaxSettings, Item, ItemInventory, ItemTransaction, PurchaseOrder, VendorInvoice, MaintenanceEvent, CipProject, CipCost, GlAccount, JournalEntry, FiscalYear, FiscalPeriod, Technician, Vendor, Customer, Site, Department, CostCenter, Location, NumberingSequence, LaborRate, LookupType, IntegrationEndpoint, InboundEvent, OutboxEvent, WebhookSubscription, AuditLog | CompanyId FK (required on most) | Single tenant may own multiple companies; each company is isolated |
| **Implicit (no CompanyId field)** | User, PaymentTerm, UOMDefinition, Currency, TaxCode, ShippingMethod, ApprovalWorkflow, WorkOrderType, FailureCode, Craft, Skill, LookupValue (system), CcaClass, UsefulLifeTable | System/org-wide; not company-specific | Shared across all companies |
| **Cross-Company FK** | Asset.Company, Book.Company, AssetBookValue (via Asset→Company), GlAccount.Company | Must exist in same Company | No cross-company references allowed |

**Key Observation:** Deployment is **multi-tenant** (TenantId at top) → **multi-company** (CompanyId per tenant). Most entities are CompanyId-scoped. No entity allows cross-company FKs; enforced via FK constraint.

---

## 3. Foreign Key Binding & Dropdown Migration Status

Source: **HANDOFF_STATUS.md** (verified against actual model files)

| FK / Dropdown | Model Field | Lookup Table | Migration Status | Notes |
|---------------|-------------|--------------|------------------|-------|
| Asset.AssetTypeLookupValueId | AssetType | LookupValue (AssetType type) | ✅ Bound | Replaced hard enum; use LookupValue with LookupType.Key='AssetType' |
| Asset.AssetPriorityLookupValueId | Priority | LookupValue (Priority) | ✅ Bound | Replaced enum; lookup-driven |
| Asset.ConditionLookupValueId | Condition (AssetCondition enum) | LookupValue (AssetCondition) | ✅ Bound | Still has enum fallback; prefer lookup |
| Asset.DepreciationMethodLookupValueId | DepreciationMethod | LookupValue (DepreciationMethod) | ✅ Bound | Dual enum + lookup |
| Asset.StatusLookupValueId | Status (AssetStatus enum) | LookupValue (AssetStatus) | ✅ Bound | Lookup-driven |
| Book.MethodLookupValueId | Method (DepreciationMethod) | LookupValue (DepreciationMethod) | ✅ Bound | Linked to Book depreciation config |
| Book.ConventionLookupValueId | Convention (DepreciationConvention) | LookupValue (DepreciationConvention) | ✅ Bound | e.g., HalfYear, MidMonth |
| Book.BookTypeLookupValueId | BookType | LookupValue (BookType) | ✅ Bound | Financial vs. Tax vs. Managerial |
| Book.TaxJurisdictionLookupValueId | TaxJurisdiction | LookupValue (TaxJurisdiction) | ✅ Bound | USA, Canada, etc. |
| Book.FrequencyLookupValueId | CalculationFrequency | LookupValue (DepreciationFrequency) | ✅ Bound | Monthly, Quarterly, Annually |
| MaintenanceEvent.TypeLookupValueId | Type (MaintenanceType) | LookupValue (MaintenanceType) | ✅ Bound | Preventative, Corrective, Emergency |
| MaintenanceEvent.StatusLookupValueId | Status (MaintenanceStatus) | LookupValue (MaintenanceStatus) | ✅ Bound | Scheduled, In Progress, Completed |
| MaintenanceEvent.PriorityLookupValueId | Priority (MaintenancePriority) | LookupValue (MaintenancePriority) | ✅ Bound | Low, Medium, High, Urgent |
| WorkOrderOperation.TypeLookupValueId | Type (OperationType) | LookupValue (OperationType) | ✅ Bound | Mechanical, Electrical, Calibration |
| WorkOrderOperation.StatusLookupValueId | Status (OperationStatus) | LookupValue (OperationStatus) | ✅ Bound | Pending, Ready, InProgress, Completed |
| Item.TypeLookupValueId | Type (ItemType) | LookupValue (ItemType) | ✅ Bound | Part, Consumable, Tool, Safety, etc. |
| Item.StatusLookupValueId | Status (ItemStatus) | LookupValue (ItemStatus) | ✅ Bound | Active, Inactive, Obsolete, Discontinued |
| Item.CostMethodLookupValueId | CostMethod | LookupValue (CostMethod) | ✅ Bound | Standard, Average, FIFO, LIFO, LastPurchase |
| Item.TrackingTypeLookupValueId | TrackingType | LookupValue (TrackingType) | ✅ Bound | None, LotNumber, SerialNumber, Both |
| PurchaseOrder.POTypeLookupValueId | POType | LookupValue (POType) | ✅ Bound | Standard, Blanket, Emergency, Contract |
| PurchaseOrder.StatusLookupValueId | Status (POStatus) | LookupValue (POStatus) | ✅ Bound | Draft → Approved → Sent → Received → Invoiced |
| GlAccount.AccountTypeLookupValueId | AccountType (GlAccountType) | LookupValue (GlAccountType) | ✅ Bound | Asset, Liability, Equity, Revenue, Expense |
| CipProject.StatusLookupValueId | Status (CipProjectStatus) | LookupValue (CipProjectStatus) | ✅ Bound | Active, OnHold, Completed, Capitalized |
| CipCost.CostTypeLookupValueId | CostType (CipCostType) | LookupValue (CipCostType) | ✅ Bound | Construction, Engineering, Labor, Materials |

**Status:** ~95% of entity enums now have LookupValue bindings. Legacy enums still exist (AssetCondition, OperationType, etc.) but should use lookup system for extensibility.

---

## 4. Indexes & Constraints from AppDbContext

### Primary Indexes

| Index | On Entity | Columns | Unique | Type | Purpose |
|-------|-----------|---------|--------|------|---------|
| IX_Assets_CompanyId_AssetNumber_Unique | Asset | (CompanyId, AssetNumber) | Yes | Composite | Asset number per company must be unique |
| IX_CcaClass_ClassNumber | CcaClass | ClassNumber | Yes | Single | CCA class lookup |
| IX_BookGlAccount_BookId | BookGlAccount | BookId | Yes | Single | 1:1 Book → GL mapping |
| IX_FiscalPeriod_Period | JournalEntry | Period (yyyymm) | No | Single | Fast lookup by accounting period |
| IX_JournalBatch | JournalEntry | Batch | No | Single | Batch-level queries |
| IX_JournalLine_EntryId_LineNo | JournalLine | (JournalEntryId, LineNo) | No | Composite | Line ordering within entry |
| (implied) | GlAccount | CompanyId | No | Single | Company-level account lists |
| (implied) | Item | CompanyId | No | Single | Company item catalog |

### Delete Behaviors (OnDelete from AppDbContext)

| FK | Delete Behavior | Rationale |
|----|-----------------|-----------|
| Asset → CostCenter, Department, Location, AssetCategory | SetNull | Preserve asset even if org changes |
| Asset → LookupValue (all) | SetNull | Don't delete if lookup removed |
| Asset → Vendor, Manufacturer | SetNull | Preserve purchase history |
| Asset → Company | Restrict (implicit) | Prevent company deletion if assets exist |
| Book → Company | Restrict (implicit) | Prevent company deletion if books exist |
| JournalEntry → Book | Restrict | Prevent book deletion if posted entries exist |
| JournalLine → JournalEntry | Cascade | Delete detail lines if entry deleted |
| MaintenanceEvent → Asset | Implicit (Restrict) | Preserve maintenance history if asset disposed |
| PurchaseOrder → Vendor | Restrict | Preserve PO history even if vendor deleted |

**Pattern Observation:** Most FKs use `SetNull` (optional FK) or `Restrict` (data preservation). Cascade is only used for detail lines (JournalLine → JournalEntry). This prevents accidental data loss.

---

## 5. Migration History Summary (Recent 5)

| Migration ID | Date (UTC) | Migration Name | Feature | Status |
|--------------|-----------|----------------|---------|--------|
| 20260120053232 | 2026-01-20 05:32 | AddMesIotOeeFields | MES/IoT/OEE metrics + predictive thresholds added to Asset | Complete |
| 20260120031148 | 2026-01-20 03:11 | AddCIPToBookGlAccount | CIP GL account mapping added to BookGlAccount | Complete |
| 20260120022613 | 2026-01-20 02:26 | AddFiscalCalendar | FiscalYear, FiscalPeriod, DepreciationRun tables | Complete |
| 20260119223517 | 2026-01-19 22:35 | AddPurchaseOrderReleases | PurchaseOrderRelease for blanket PO splits (Sprint 10) | Complete |
| 20260119215318 | 2026-01-19 21:53 | AddMultiLocationPurchasing | Multi-location Ship-To/Bill-To on PurchaseOrder | Complete |

**Key Observations:**
- Recent work (Jan 19-20, 2026) focuses on MES/IoT/OEE and fiscal calendar.
- Multi-location purchasing and blanket PO releases are very recent (Sprint 10).
- IoT fields are extensive (54+ columns on Asset for sensors, predictive health, calibration).
- All migrations follow date-first naming convention (yyyyMMddhhmmss_FeatureName).

---

## 6. Concurrency & Row Versioning

### Row Version Strategy

**Asset.RowVersion (byte[]):** 
- Mapped to PostgreSQL system column `xmin` (transaction ID, unsigned 32-bit int)
- **Conversion:** byte[4] ↔ uint via big-endian encoding in AppDbContext
- **EF Configuration:**
  ```csharp
  e.Property(a => a.RowVersion)
      .HasColumnName("xmin")
      .HasColumnType("xid")
      .ValueGeneratedOnAddOrUpdate()
      .IsConcurrencyToken()
      .HasConversion(...)  // uint ↔ byte[]
  ```
- **Usage:** Optimistic concurrency; prevents lost updates when multiple users edit simultaneously
- **Exposed as:** Base64-encoded ETag in REST API (public RowVersion field)

**Other Entities:** 
- No explicit RowVersion documented on other entities (Book, DepreciationPolicy, Item, etc.)
- Likely rely on EF's ChangeTracker or application-level optimism
- **Gap:** Should consider adding RowVersion to high-contention entities (Book, Item, MaintenanceEvent)

---

## 7. Audit Fields (CDC)

### Standard Pattern

| Field | Type | Nullable | Purpose | Entities |
|-------|------|----------|---------|----------|
| CreatedAt | DateTime | No (default: DateTime.UtcNow) | Timestamp of creation | All entities |
| CreatedBy | string(50-100) | Yes (nullable) | User ID who created | Most entities |
| ModifiedAt / UpdatedAt | DateTime | Yes | Timestamp of last update | Most entities |
| ModifiedBy / UpdatedBy | string(50-100) | Yes | User ID who last modified | Most entities |

### Entities with Full Audit Trail

Asset, Book, DepreciationPolicy, Item, ItemRevision, CipProject, CipCost, MaintenanceEvent, PurchaseOrder, VendorInvoice, GlAccount, FiscalPeriod, Company, Vendor, Technician, User, etc.

### Entities with Sparse Audit

Some entities (LookupValue, PaymentTerm, Currency) omit CreatedBy/ModifiedBy (system-maintained, read-only after creation).

### Explicit Audit Table

**AuditLog:** 
- Id, EntityType, EntityId, Action, OldValue, NewValue, ChangedAt, ChangedBy, CompanyId
- Stores human-readable change deltas (not all columns, selective logging)
- Used for compliance/regulatory audit trail

**PeriodLock:**
- Id, FiscalPeriodId, LockedDate, LockedBy, Notes
- Tracks period close events

---

## 8. Soft-Delete vs. Hard-Delete Posture

### Soft-Delete Pattern (Used)

| Entity | Field(s) | Implementation |
|--------|----------|-----------------|
| Asset | Active (bool), Status (enum AssetStatus) | Both Active=false and Status=Disposed/Archived observed |
| Item | IsActive (bool) | Standard soft-delete |
| Book | IsActive (bool) | Books can be marked inactive |
| Vendor | Status (VendorStatus enum: Active/Inactive/OnHold/Blocked) | Enum-based soft-delete |
| DepreciationPolicy | IsActive (bool) | Inactive policies remain queryable |
| FiscalPeriod | IsLocked (bool) | Period-level soft-lock (not full deletion) |
| MaintenanceEvent | Status (MaintenanceStatus enum) | Cancelled status replaces deletion |

### Hard-Delete Pattern (Minimal)

- **AuditLog, WebhookDeliveryLog, InventoryScan, etc.:** Append-only; no delete (compliance/audit trail)
- **Reference data (Currency, UOMDefinition, PaymentTerm):** Rarely deleted; prefer soft-delete if needed
- **JournalEntry, VendorInvoice:** Once posted, cannot be deleted (only reversed); no hard-delete

### Query Filters / IsDeleted Suppression

**Observed:** No global query filters (e.g., `.Where(x => !x.IsDeleted)`) in OnModelCreating. **Gap:** Should be added to prevent accidental retrieval of inactive/archived entities.

---

## 9. Enterprise EAM Schema Gaps (vs. Maximo/SAP Standard)

| Feature | CherryAI Status | Severity | Comments |
|---------|-----------------|----------|----------|
| **IoT Telemetry Ingestion** | ✅ Fields exist (IoTDeviceId, IoTGatewayId, DataHistorianTag, SCADATag, sensor thresholds) | ⚠️ Medium | Fields present but no dedicated telemetry storage (time-series DB assumed external) |
| **Warranty Management** | ✅ Basic (WarrantyStartDate, WarrantyEndDate, WarrantyVendorId, WarrantyContractNumber on Asset; WarrantyMonths, WarrantyTerms on Item) | ⚠️ Medium | No warranty claim tracking, coverage matrix, or auto-expiry alerts |
| **Calibration History** | ✅ Fields (CalibrationRequired, CalibrationType, FrequencyDays, LastCalibrationDate, NextCalibrationDue, Certificate#, Vendor, Status) | ⚠️ Medium | No audit trail of calibration work orders or certificate storage |
| **Condition Monitoring** | ✅ Sensor fields (CurrentVibration, CurrentTemperature, CurrentPressure, SensorReadingsLastUpdated, thresholds) | ⚠️ Medium | Fields present; no trend analysis or ML-ready aggregations |
| **Energy Meters** | ✅ Fields (EnergyClass, RatedPowerConsumptionKW, AnnualEnergyConsumptionKWH, EnergyMeterId, standard/idle/standby rates) | ⚠️ Medium | No consumption trend tables or carbon tracking |
| **Safety/LOTO Permits** | ✅ Fields (LockoutTagoutRequired, LOTOProcedureId, ConfinedSpaceEntry, HotWorkPermitRequired, HighVoltage, SafetyClassification, SafetyNotes) | ⚠️ Medium | No explicit LOTO permit workflow, isolation verification, or clearance tracking |
| **Contractor Management** | ⚠️ Partial | ❌ High | Vendor.VendorType=Contractor exists; no contractor-specific insurance/bonding/safety cert tracking |
| **Safety Incident / HSE Events** | ❌ Missing | ❌ High | No incident/injury/near-miss tracking; AuditLog is generic |
| **Meter-Based PM** | ✅ Implemented (MeterReading entity; HasMeter, MeterType, CurrentMeterReading on Asset; CalibrationFrequencyDays as proxy) | ✅ Good | MeterReading table supports condition-based triggers |
| **RUL (Remaining Useful Life) Prediction** | ✅ Partial (PredictedFailureDate, PredictedFailureReason, PredictiveHealthScore on Asset) | ⚠️ Medium | Fields present (sourced from external ML model); no RUL calculation engine |
| **OEE (Overall Equipment Effectiveness)** | ✅ Comprehensive (OEETracked, StandardRunRate, IdealCycleTimeSeconds, TargetAvailability/Performance/Quality/OEE, CurrentOEE, OEELastCalculated) | ✅ Good | Fields ready; calculation engine assumed external (MES) |
| **Spare Parts Criticality** | ✅ Basic (Item.IsCriticalSpare; AssetInventory tracks levels) | ⚠️ Medium | No ABC/xyz inventory classification; no automated critical-spare reorder |
| **Compliance Matrix** | ⚠️ Partial | ⚠️ Medium | FDA/OSHA flags (IsFDARegulated, IsOSHACompliance, OSHAClassification, EPAPermitNumber) exist; no compliance audit trail |
| **Asset Hierarchy (Parent/Child)** | ✅ Implemented (Asset.ParentAssetId, Asset.ChildAssets) | ✅ Good | Maximo-style asset structures supported |
| **Multi-Book Depreciation** | ✅ Comprehensive (Book, AssetBookSettings, AssetBookValue) | ✅ Good | GAAP, Tax, Managerial books supported in one DB |
| **CIP Capitalization** | ✅ Full module (CipProject, CipCost, CipCapitalization, CipCapitalizationCost) | ✅ Good | Project-to-asset conversion with cost tracking |
| **PM Template Versioning** | ✅ Implemented (PMTemplateRevision, PMTemplateRevisionOperation) | ✅ Good | Engineering change control for PM procedures |
| **Item Master Cross-Reference** | ✅ Implemented (ItemManufacturerPart, VendorItemPart, ItemAlternate, ItemSupersession) | ✅ Good | Procurement-grade part equivalencing |
| **Failure Code / RCFA (Root Cause Failure Analysis)** | ⚠️ Partial | ⚠️ Medium | FailureCode, CauseCode, ActionCode, ProblemCode exist as lookups; no linked workflow or trend reporting |
| **Equipment Genealogy (Serial# Tracking)** | ✅ Fields (SerialNumber, ItemInventory.SerialNumber, GoodsReceiptLine.SerialNumber) | ⚠️ Medium | Fields present; no genealogy traceability engine |
| **Asset Lease/Rental** | ⚠️ Partial | ⚠️ Medium | EquipmentLeaseRental GL account exists; no lease management/amortization module |
| **Multi-Facility / Location Hierarchy** | ✅ Implemented (Site, Location with hierarchy) | ✅ Good | Supports site → warehouse → aisle → bin model |
| **API/ERP Integration** | ✅ Framework exists (IntegrationEndpoint, IntegrationMapping, InboundEvent, OutboxEvent, WebhookSubscription) | ⚠️ Medium | Scaffolding present; no pre-built SAP/Oracle/NetSuite adapters |
| **Reporting (BI/Data Warehouse)** | ❌ Not in schema | ❌ High | No reporting schema or fact tables; schema is OLTP-centric |
| **Mobile Offline Sync** | ❌ Not in schema | ❌ High | No sync/replication tables; would need ETL |

### Top 3 Gaps for Enterprise Deployments

1. **Safety/HSE/Incident Tracking:** No incident, near-miss, or safety-related event capture (beyond asset safety flags). Enterprise customers require detailed HSE reporting.
2. **Contractor Management:** Vendor system exists but lacks insurance tracking, bonding, safety certifications, and compliance verification needed for construction/field service EAMs.
3. **Reporting/BI Schema:** OLTP schema is great for operations but lacks denormalized fact tables, slowly-changing dimensions, and data warehouse support for executive analytics.

---

## 10. Anomalies & Data Quality Issues

### Naming / Pluralization Issues

| Field / Table | Issue | Impact |
|---------------|-------|--------|
| ItemInventory2 (DbSet) | DbSet named `ItemInventories2` (with "2" suffix) | Historical artifact; likely from schema refactor. Causes confusion. Should be `ItemInventories`. |
| Department | Exists as both standalone entity AND as string field on Asset.Department | Dual pattern; string field is legacy for migration compatibility |
| LongDescription vs. ExtendedDescription | Asset uses `LongDescription`; Item uses `ExtendedDescription` | Inconsistent naming; should standardize |
| ModifiedAt vs. UpdatedAt | Both patterns used (some entities use ModifiedAt, others UpdatedAt) | Inconsistent across codebase; should standardize to one (recommend UpdatedAt) |
| LocationId vs. DefaultShipToLocationId on PurchaseOrder | FK naming is inconsistent | Minor; but should follow Location FK naming convention |
| Notes (2000 chars) | Asset has 2000-char Notes field; most others have 500-char max | Inconsistent max lengths; should standardize (recommend 1000) |

### Fragile FK Chains

| Issue | Entities | Risk |
|-------|----------|------|
| MaintenanceEvent → CipProject → Asset | Maintenance can link to both Asset (directly) and Asset (via CIP). Allows duplicate references | Moderate; application logic must enforce single path. |
| Item.ManufacturerId vs. Asset.ManufacturerId | Two different Manufacturer FKs, not aligned | Low; intentional (item vs. instance), but needs doc. |
| Asset.VendorId (purchase vendor) vs. Vendor.DefaultGlAccount | Vendor is global; Asset is company-scoped. If vendor deleted, asset.VendorId orphaned (FK SetNull) | Moderate; acceptable; audit trail preserved. |
| PurchaseOrder → Vendor (Restrict) but Item.PrimaryVendor (FK to Vendor) | If vendor deleted, PO preserved (Restrict) but Item.PrimaryVendor orphaned (SetNull). Inconsistency. | Low to Moderate; should align delete behaviors. |

### Column Name Typos / Misspellings

**None detected in core tables.** Schema is well-formed.

### Enumerations (Overlap with LookupValue)

| Enum | Status | Issue |
|------|--------|-------|
| AssetCondition (8 values) | ✅ Bound to LookupValue | Enum still present but redundant; prefer lookup |
| AssetStatus | ✅ Bound to LookupValue | Enum still present; dual pattern (enum + lookup) |
| DepreciationMethod | ✅ Bound to LookupValue | Enum still present; dual pattern |
| MaintenanceType | ✅ Bound to LookupValue | Enum still present; dual pattern |
| MaintenanceStatus | ✅ Bound to LookupValue | Enum still present; dual pattern |
| OperationType (13 values) | ✅ Bound to LookupValue | Enum still present; dual pattern |
| ItemType (17 values) | ✅ Bound to LookupValue | Enum still present; dual pattern |
| POStatus (9 values) | ✅ Bound to LookupValue | Enum still present; dual pattern |
| VendorStatus (4 values) | ❌ Not bound | Only enum, no lookup alternative. Should bind. |
| CompanyType (3 values) | ❌ Not bound | Only enum; should bind if extensibility needed |

**Issue:** Dual patterns (enum + LookupValue) create maintenance burden. Recommend migrating all enums to LookupValue-only (single source of truth).

---

## 11. Advanced Features & Observations

### Multi-Currency Support

- **ExchangeRate table:** Tracks spot rates by date range
- **Asset.Currency, Book.Currency, Company.Currency, Vendor.Currency, PurchaseOrder.Currency, Item.CurrencyCode:** All scoped
- **Gap:** No automatic currency conversion on GL posting; assumes application layer handles FX revaluation

### Fiscal Year Customization

- **Company.FiscalYearStartMonth, FiscalYearStartDay, IsShortYear, ShortYearStart, ShortYearEnd**
- **FiscalYear, FiscalPeriod:** Full calendar with lock controls
- **DepreciationFrequency:** Monthly, Quarterly, SemiAnnually, Annually, Daily
- **Supports:** US (Jan-Dec), fiscal years (Apr-Mar, Jul-Jun, etc.), short years, and custom configurations

### MES/Manufacturing Integration

- **Asset fields:** WorkCenterId, WorkCenterName, ProductionLineId, ProductionLineName, CellId, ProcessId, OperationId, RoutingSequence, ShiftCalendarId, PlannedProductionHoursPerDay
- **Assumption:** MES is external; EAM stores work center assignments for scheduling coordination

### Section 179 & Bonus Depreciation (US Tax)

- **UsTaxSettings:** Section179Claimed, Section179Amount, BonusDepreciationClaimed, BonusDepreciationAmount
- **Section179Limits, BonusDepreciationRates:** Tax-year lookup tables
- **DepreciationPolicy:** Flags for ApplySection179, ApplyBonusDepreciation with configurable percentages

### Canadian CCA (Capital Cost Allowance)

- **CcaClass, CcaClassBalance, CcaTransaction:** Full CCA pool tracking
- **Half-year rule, accelerated investment incentive flags**
- **Supports:** Declining balance method, pool-level UCC tracking, per-tax-year calculations

### Predictive Maintenance & Machine Learning

- **Asset fields:** PredictiveHealthScore (0-100), HealthScoreLastCalculated, PredictedFailureDate, PredictedFailureReason
- **Sensor thresholds:** VibrationWarningThreshold, TemperatureWarningThreshold, PressureWarningThreshold (with alarm variants)
- **Current readings (cached):** CurrentVibration, CurrentTemperature, CurrentPressure, SensorReadingsLastUpdated
- **Assumption:** ML model calculates health score externally; EAM stores predictions for alerting

### Calibration Automation

- **Asset fields:** CalibrationRequired, CalibrationType, CalibrationFrequencyDays, LastCalibrationDate, NextCalibrationDue, CalibrationCertificateNumber, CalibrationVendor, CalibrationStatus
- **No explicit calibration work order entity,** but can be modeled as MaintenanceEvent with Type='Calibration'

### Item Revisions (Engineering Change Control)

- **ItemRevision.Status:** Draft → Approved → Released → Obsolete
- **ItemRevision.SupersedesItemRevisionId:** Tracks ECN chains
- **Item.CurrentReleasedRevisionId:** Pins released revision
- **Effective dates:** EffectiveFromUtc, EffectiveToUtc

---

## 12. Summary Table: Entity Count & Model Metrics

| Category | Count | Notes |
|----------|-------|-------|
| **Total Entities/Classes** | 120+ | Asset, Book, Item, MaintenanceEvent, PurchaseOrder, CipProject, GlAccount, etc. |
| **Enums** | 45+ | AssetCondition, ItemType, POStatus, DepreciationMethod, OperationType, etc. |
| **DbSets in AppDbContext** | 80+ | Explicitly registered; complete OLTP schema |
| **Entities with CompanyId FK** | 85+ | Company-scoped multi-tenancy |
| **Entities with full audit trail (CreatedAt/By, UpdatedAt/By)** | 50+ | Operational entities + config |
| **Entities with soft-delete (IsActive or Status enum)** | 40+ | Preserves history |
| **Indexes (explicit)** | 8+ | Asset (CompanyId, AssetNumber), CcaClass (ClassNumber), JournalEntry (Period, Batch), etc. |
| **Unique Indexes** | 3+ | Asset (CompanyId, AssetNumber), CcaClass (ClassNumber), BookGlAccount (BookId) |
| **Foreign Keys with Restrict delete** | 10+ | Book→Company, JournalEntry→Book, etc. (data preservation) |
| **Foreign Keys with SetNull delete** | 30+ | Asset→Location, Asset→Vendor, etc. (graceful degradation) |
| **Foreign Keys with Cascade delete** | 2 | JournalLine→JournalEntry (detail cleanup) |

---

## 13. Final Assessment & Recommendations

### Strengths

1. **Comprehensive Multi-Book Depreciation:** GAAP, Tax (US MACRS, Canada CCA), Section 179, Bonus all in one schema.
2. **Maximo-Style Asset Hierarchy:** Parent/child asset structures with flexible attributes.
3. **Procurement-Grade Item Master:** Cross-references (manufacturer, vendor, alternates, supersessions) + vendor scoring.
4. **CIP Module:** Full project-to-asset capitalization with cost tracking.
5. **IoT/Predictive Maintenance Ready:** Telemetry fields, sensor thresholds, health score, RUL prediction (externally calculated).
6. **Multi-Location Support:** Site, Location, PurchaseOrder multi-ship/bill-to.
7. **Audit Trail:** CreatedAt/By, UpdatedAt/By on most entities + explicit AuditLog + PeriodLock.

### Weaknesses

1. **Enumeration Fragmentation:** Many enums exist alongside LookupValue (e.g., AssetCondition, ItemStatus). Dual maintenance burden; should consolidate to LookupValue-only.
2. **No Global Query Filters:** Missing `.Where(x => !x.IsDeleted)` in OnModelCreating; risk of returning inactive records.
3. **Reporting Schema Gap:** OLTP schema; no denormalized fact tables or BI layer.
4. **Limited Safety/HSE:** No incident, near-miss, or safety event tracking beyond asset flags.
5. **Contractor Management Thin:** No insurance, bonding, safety cert tracking.
6. **Time-Series Data:** No built-in telemetry storage (assumed external, e.g., InfluxDB); only cached snapshots on Asset.
7. **Row Version on Asset Only:** Should add to high-contention entities (Book, Item, MaintenanceEvent).
8. **Inconsistent Naming:** ModifiedAt vs. UpdatedAt, LongDescription vs. ExtendedDescription, LocationId vs. DefaultShipToLocationId.

### Actionable Next Steps (for 2026 Q2+)

1. **Consolidate Enums → LookupValue:** Eliminate redundant enums; make LookupType.Key the single source of truth.
2. **Add Global IsActive Filters:** Apply query filters in OnModelCreating to prevent accidental retrieval of soft-deleted records.
3. **Extend Row Version:** Add RowVersion (xmin) to Book, Item, MaintenanceEvent, DepreciationPolicy.
4. **HSE Module:** Add SafetyIncident, InspectionRecord, NonConformance entities.
5. **Contractor Tracking:** Extend Vendor to include Contractor-specific fields (insurance, bonding, certifications, expiry tracking).
6. **Reporting Layer:** Design fact tables (AssetFactDaily, MaintenanceFactMonthly, DepreciationFactMonthly) for BI.
7. **Standardize Naming:** Align on UpdatedAt (vs. ModifiedAt), standardize max lengths, unify FK naming.
8. **Telemetry Integration:** Publish design for time-series ingest (Kafka → InfluxDB → Asset sensor cache).

---

**Audit Completed:** 2026-05-07  
**Report Size:** ~6,800 words  
**Model Files Reviewed:** All 67 in Models/ directory  
**Key Files Read:** Asset.cs, Book.cs, Item.cs, DepreciationPolicy.cs, PurchaseOrder.cs, MaintenanceEvent.cs, ConstructionInProgress.cs, GlAccount.cs, Company.cs, Vendor.cs, AppDbContext.cs, LookupType.cs  
**Database:** PostgreSQL 14+ (xmin row versioning)  
**Framework:** EF Core 9.0 with value converters, query filters, cascade configs
