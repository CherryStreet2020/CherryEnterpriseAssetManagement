# Item Master Expansion & Multi-Dimensional Inventory — Research Report

**Date:** 2026-05-18
**Author:** Research subagent for Dean
**Scope:** Item master field expansion + multi-dimensional inventory architecture + ItemEdit page restructure
**Status:** Captured as future scope — MASTER_PLAN Sprints 7, 8, 9. Not blocking current Sprint 4 Phase F Wave 1.

---

## Executive Summary

CherryAI already has a strong **Item Master backbone** (~90 fields on `Item`, plus `ItemCompanyStocking`, `ItemApprovedVendor`, `ItemAlternate`, `ItemVendor`, `ItemRevision`). The biggest gaps vs SAP MM / Oracle Cloud / D365 / NetSuite are:

1. **Regulatory / compliance fields** — REACH, RoHS, ITAR, Conflict Minerals, SDS, UDI
2. **MRP / planning sophistication** — PlanningMethod, LotSizingRule, time fences, ServiceLevelTarget, demand/forecast
3. **Deeper quality fields** — InspectionPlan FK, AQL, sampling plan, MTR-required, CoC-required
4. **Specs / classification scaffolding** — UNSPSC tree, eCl@ss, polymorphic Item-class specs
5. **Multi-dimensional inventory schema** — current `ItemInventory` is a single flat row keyed on Location+Lot+Serial; cannot represent two batches at same location, no Status / Owner / Quality / Variant dims

### What CherryAI has vs what's missing (12 categories)

| Category | CherryAI has | Missing | Priority | Effort |
|---|---|---|---|---|
| Identification | PartNumber, Description, ExtendedDescription, Revision, RequireRevisionControl, ItemRevision lifecycle | UPC/EAN/GTIN, ECCN, NDC, CAS#, drawing#, ECN cross-link, multi-language descriptions | P1 (id codes), P2 (i18n) | M / L |
| Classification | ItemCategory tree, CommodityCode, UNSPSCCode (free text), ABCClass, Type enum, StockPolicy | XYZ class (demand variability), Make/Buy flag, Plannable Y/N, Product Family, Brand, eCl@ss, polymorphic Item-class specs | P1 (XYZ/Make-Buy/Plannable), P2 (eCl@ss + specs) | S / L |
| UOM | UOM, StockUOM, PurchaseUOM, PurchaseConversion (decimal), Weight, L/W/H, DimensionUOM | Sales UOM, Reporting UOM, full N-to-N UOM conversion table, tare weight, pack hierarchy (each→inner→case→pallet), Volume | P1 | M |
| Sourcing | PrimaryVendor, ItemApprovedVendor (AVL), ItemVendor with price breaks + contract + LTA, VendorPartNumber, MinOrderQty, OrderMultiple, LeadTimeDays, LastPurchaseCost | Buyer FK, Last PO# / Date denorm, Incoterms, vendor-rated lead-time, dual-sourcing share %, requalification dates | P2 | M |
| Inventory & Storage | IsStocked, IsCriticalSpare, ShelfLifeDays, IsHazmat, HazmatClass, MinStorageTemp/MaxStorageTemp, StorageRequirements, Warehouse/Aisle/Rack/Shelf/Bin (text), DefaultLocationRef FK | FIFO/LIFO/FEFO picking-order, ESD-sensitive, cycle-count frequency tied to ABC, count tolerance %, putaway / picking strategy | **P1** (FIFO/FEFO + count freq) | M |
| Planning & MRP | MinQuantity, MaxQuantity, ReorderPoint, ReorderQuantity, SafetyStock, LeadTimeDays, ReorderMethod, EOQ, AnnualUsage, AverageDailyUsage, CarryingCostPercent, OrderingCost, AutoReorderEnabled | **PlanningMethod** (MRP/MPS/Kanban/MTO/ETO), **LotSizingRule**, **PlanningTimeFence**, **DemandTimeFence**, **ServiceLevelTarget**, ForecastModel, past-due-demand behavior | **P1** (biggest single gap vs SAP/D365) | L |
| Manufacturing | Polymorphic Bom / Recipe / MaterialStructure / ProductionOrder / ProductionBatch link FROM those tables TO Item | Default BOM FK on Item, default Routing/Recipe FK, default Work Center FK, Phantom Y/N, Backflush Y/N, Yield %, Scrap %, ECO/ECN cross-ref | P2 | M |
| Quality | Certifications (free text), IsFDARegulated, IsOSHACompliance | **InspectionPlanId FK, AqlLevel, SamplingPlanCode, MtrRequired, CocRequired, FaiRequired, Cpk target** — table stakes for aerospace / ASME / FDA verticals | **P1** | M |
| Costing & Financial | CostMethod, StandardCost, AverageCost, LastPurchaseCost, ListPrice, CurrencyCode, ItemCategory.DefaultGlAccount | Landed-cost component breakdown, cost rollup (Material/Labor/OH/OS), variance account, tax-category enum, markup/margin, per-item GL override | P2 | M |
| Sales & Demand | Limited (ListPrice) | IsSalable, SalesUom, DropShipAllowed, EolDate, CustomerItemXref, ATP/CTP, substitution rules | Maybe (only if sales-side is sprint-scope) | M |
| Lifecycle | Status enum, IsActive, SupersedesPartNumber/SupersededByPartNumber, ItemSupersession | **LifecycleStage** enum (Concept/Design/Prototype/Pre-Production/Production/Phase-In/Phase-Out/EOL/Obsolete), PhaseOutDate, ReleasedToProduction Date | P2 | S |
| Regulatory | IsFDARegulated, IsOSHACompliance, IsHazmat, HazmatClass, CountryOfOrigin, HTSCode | **Conflict-Minerals 3TG declaration, REACH SVHC, RoHS, ITAR controlled, EAR/ECCN, FDA UDI, SDS document FK, GHS hazard class, Prop-65, WEEE recycling code** | **P1** (mandatory for regulated industries) | L |

### Inventory: what we have vs what we need

CherryAI's `ItemInventory` is keyed on `(Item, Location, LotNumber, SerialNumber)` as flat columns on the same row. Two batches of the same item at the same location cannot be represented — PK collides. There's no Status, Owner, Quality, Variant, or Project dim.

**Recommendation: OnHandQuantity fact table** keyed by (Item × Site × Warehouse × Bin × Lot × Serial × Status × Owner × Quality × Variant × Project), quantity per dim combo. Same pattern as SAP / Oracle / D365.

---

## Part 1 — Industry-Standard Item Master Fields (canonical references)

Survey of the canonical reference implementations: **SAP MM Material Master** (~20 views via MM01/MM02/MM03 — Basic Data 1/2, Classification, Sales Org 1/2, Sales General/Plant, Foreign Trade Export, Purchasing, Foreign Trade Import, MRP 1/2/3/4, Forecasting, Work Scheduling, Production Resources/Tools, General Plant/Storage 1/2, Warehouse 1/2, Quality Management, Accounting 1/2, Costing 1/2, Plant Stock, Storage Location Stock); **Oracle Cloud Inventory Item Master** (Item Specifications tab — Material Control, Lot, Lot Expiration, Child Lot, Lot Split/Merge, Grade, Serial, Cycle Count, Status, Locator — plus Item Class, Item Catalog Categories, Cross-References, Trading Partner Items, Item Relationships); **D365 F&O Released Products** (FastTabs — General, Product details, Manage inventory, Manage costs, Engineer, Plan, Manage projects, Manage sales, Manage purchases, Foreign trade, Warehouse, Transportation, Set up); **NetSuite Item Record** (subtabs — Purchasing/Inventory with Vendors & Locations & Bin Numbers sublists, Manufacturing, Accounting Books, Pricing, Subscription, Translation); **IBM Maximo Item Master** (Rotating, Condition-Enabled, Capitalized, Lot Type, Inspection on Receipt, Order Unit, Issue Unit, Commodity Group); **Infor EAM Part record** (Commercial vs Spec, ABC, Critical, Rotable, Repair-Lead-Time); **Plex Smart Manufacturing Part Master** (Part Spec rev, Make-Buy, Operation Routing); and the **NIST PLM Handbook / ISO 8000-110:2021** (master-data quality — characteristic-data exchange, syntax+semantic+conformance, with -120 provenance and -140 completeness as companions).

Findings grouped by **functional category** (not by ERP):

### 1. Identification
Top fields: **Item Number**, Short Description (40-60 chars), Long / Marketing Description, **UPC / EAN / GTIN**, MPN (1:N with Manufacturer), VPN (N:N with vendor), **CAS#** (chem), NDC# (FDA drug), **ECCN** (US export 14C001-style), HTS/HS code (customs 10-digit), Country of Origin, Hazmat code (UN#), Drawing # + Revision #, ECN log. **CherryAI:** has Part#, Description, ExtendedDescription, Revision, HTSCode, CountryOfOrigin, ManufacturerPartNumber, VendorPartNumber. **Missing:** UPC/EAN/GTIN, ECCN, NDC, CAS, drawing#, structured ECN log. **Recommend:** Add UPC, GTIN, ECCN, CAS, drawing#. NDC only if pharma is a target.

### 2. Classification
Top: Item Type (Stock/Service/Phantom/Tool/Kit), Item Group, **Item Class** (Oracle's polymorphic spec driver), ABC class, **XYZ class** (demand-variability — X stable, Z erratic), Commodity Code (UNSPSC / eCl@ss), Product Family / Brand, **Make-Buy flag**, **Plannable Y/N**, **Rotating Y/N** (Maximo's killer feature for repairable spares). **CherryAI:** has ItemCategory tree, Type enum, ABCClass, CommodityCode, UNSPSCCode, StockPolicy, IsCriticalSpare, IsStocked. **Missing:** XYZ class, Make/Buy, Plannable, Rotating, Product Family, eCl@ss. **Recommend Yes:** XYZ, Make/Buy, Plannable, Rotating.

### 3. Units of Measure
Top: Base UOM, Purchase UOM, Sales UOM, Stocking UOM, Reporting UOM, **N-to-N UOM conversion table** with factor + valid-from, Weight (gross/net), Volume, **Pack hierarchy** (each→inner→case→pallet), Tare weight. **CherryAI:** has UOM enum, StockUOM, PurchaseUOM, single-decimal PurchaseConversion, Weight, L/W/H, DimensionUOM. **Missing:** sales UOM, reporting UOM, normalized N-to-N conversion table, pack hierarchy, tare. **Recommend:** Convert PurchaseConversion → `ItemUomConversion` table; add tare weight + 3-level pack hierarchy.

### 4. Sourcing & Purchasing
Top: AVL (already a table), Preferred Vendor, MOQ, Order Multiple, Procurement Lead Time (vendor-rated + standard + safety), Last/Standard/Average Cost, Last PO# & Date, **Buyer code FK**, VPN cross-reference, Long-Term Agreement (LTA), price-break tiers, Incoterms, payment-terms override. **CherryAI:** has ItemApprovedVendor, ItemVendor (3 price breaks + ContractRef + ContractStartDate + ContractEndDate), PrimaryVendor, MinOrderQty, OrderMultiple, LeadTimeDays, LastPurchaseCost, LastPrice, PriceEffectiveDate, ContractFlag. **Missing:** Buyer FK (currently DefaultBuyerName string), Last PO#/Date denorm, Incoterms, payment-terms override, vendor-requalification date. **Recommend Yes:** Buyer FK to User, Incoterms enum, LastPoNumber + LastPoDate denorm.

### 5. Inventory & Storage
Top: Stock Policy, Lot-controlled Y/N, Serial-controlled Y/N, Shelf life days, Expiry tracking method, **FIFO/LIFO/FEFO selector** (picking-order, distinct from costing method), Storage class (hazmat/ESD/temperature), **Cycle-count frequency** (A=monthly/B=quarterly/C=annual), Count tolerance %, Default location, Putaway strategy, Picking strategy (zone/wave). **CherryAI:** has StockPolicy, TrackingType (None/Lot/Serial/Both), ShelfLifeDays, IsHazmat + HazmatClass, MinStorageTemp/MaxStorageTemp, StorageRequirements text, Warehouse/Aisle/Rack/Shelf/Bin text, DefaultLocationRef FK. **Missing:** FIFO/LIFO/FEFO picking-order separate from CostMethod, cycle-count frequency enum, ESD-sensitive, count tolerance %, putaway/picking strategy. **Recommend:** Split picking-order from costing method; add cycle-count frequency tied to ABC class.

### 6. Planning & MRP — biggest gap
Top: **Planning Method** (MRP / MPS / Reorder Point / Min-Max / Kanban / EOQ / MTO / ETO / PTO), **Lot Sizing Rule** (L4L / FOQ / POS / Wagner-Whitin / Min-Max), Safety Stock, ROP, Reorder Qty, Max Stock, Demand Forecast, **Planning Time Fence** (frozen window), **Demand Time Fence** (firm window), Forecast Consumption Mode (forward/backward), **Service-Level Target %**, Past-Due Demand Behavior, Planning UOM. **CherryAI:** has MinQuantity, MaxQuantity, ReorderPoint, ReorderQuantity, SafetyStock, LeadTimeDays, ReorderMethod (Manual/MinMax/ReorderPoint/EOQ/Kanban), AutoReorderEnabled, EOQ, AnnualUsage, AverageDailyUsage, CarryingCostPercent, OrderingCost. **Missing:** PlanningMethod umbrella enum (currently conflated with ReorderMethod), LotSizingRule, PlanningTimeFence, DemandTimeFence, ServiceLevelTarget %, MTO/ETO/PTO flags, past-due behavior. **Recommend P1:** add all 6. **Biggest single planning-side gap vs SAP/D365.**

### 7. Manufacturing & Engineering
Top: BOM ID (default), Routing ID (default), Recipe ID (process), Phantom Y/N, Backflush Y/N, Yield %, Scrap %, Manufacturing Lead Time, Setup Time + Run Time per unit, Default Work Center, Tool/Fixture list, Drawing # + Revision, ECO/ECN log, Where-used computed view. **CherryAI:** Phase E.2/E.3 polymorphic tables — Bom, Recipe, MaterialStructure, ProductionOrder, ProductionBatch — but link runs FROM those TO Item, not from Item to default-BOM. **Missing on Item:** PhantomFlag, BackflushFlag, ScrapPercent, YieldPercent, default BOM FK, default Routing/Recipe FK, default WorkCenter FK. **Recommend:** Materialize these 7 fields as nullable FKs/decimals.

### 8. Quality
Top: **InspectionPlan FK**, **AQL** (1.0 / 2.5 / 6.5 etc.), **Sampling Plan** (ANSI Z1.4 / ISO 2859), **Receiving Inspection Required**, In-process Inspection Plan, Final Inspection Plan, **CoC required**, **MTR required** (Phase E.2 mill-cert flag), **FAI (First-Article Inspection) required**, **Cpk target**, MRB workflow on reject. **CherryAI:** has Certifications (free text), IsFDARegulated, IsOSHACompliance. **Missing:** structured inspection-plan FK, AQL, sampling plan, MTR-required, CoC-required, FAI-required, Cpk target. **Recommend P1:** `MtrRequired`, `CocRequired`, `FaiRequired`, `InspectionPlanId` FK, `AqlLevel`, `SamplingPlanCode`. Table stakes for aerospace / ASME / FDA verticals.

### 9. Costing & Financial
Top: Cost Method, Standard/Average/Last Cost, **Landed Cost components** (freight + duty + insurance + handling), **Cost Rollup** (Material + Labor + Overhead + Outside-Op), Inventory-Asset GL, COGS Account, Variance Account, Currency, Tax Category, Markup %, Margin %, Transfer Price, 1099 tracking. **CherryAI:** has CostMethod, StandardCost, AverageCost, LastPurchaseCost, ListPrice, CurrencyCode, IsTaxable; ItemCategory carries DefaultGlAccount + ExpenseGlAccount. **Missing:** per-item GL override, landed-cost component breakdown, cost rollup, variance account, tax-category enum, markup/margin. **Recommend:** Add landed-cost 4-column group, cost-rollup 4-column group, per-item GL override FKs, tax-category enum.

### 10. Sales & Demand
Top: Salable Y/N, Price-list membership, Discount Group, Sales UOM, Customer-Item Number (N-to-N), Drop-Ship Y/N, ATP/CTP, Lead Time to Customer, Substitution rules, EOL date, Last SO Date, Last Customer. **CherryAI:** has `ListPrice` only. **Missing:** essentially the whole category. **Recommend:** Defer to Sprint 10+ unless full ERP is in Sprint 4 scope.

### 11. Lifecycle & Status
Top: Active/Inactive, **LifecycleStage** (Concept/Design/Prototype/Pre-Production/Production/Phase-In/Phase-Out/EOL/Obsolete) — separate dim from Status, PhaseOutDate, ObsoletionDate, SuccessorItem FK, PredecessorItem FK, EngineeringStatus, ReleasedToProduction Date, LastMovementDate. **CherryAI:** has Status enum, IsActive, SupersedesPartNumber/SupersededByPartNumber, structured ItemSupersession. **Missing:** LifecycleStage as separate dim, PhaseOut planned-date, ReleasedToProduction date. **Recommend P2:** LifecycleStage is modern best practice (separates "phasing out" from "inactive").

### 12. Regulatory & Compliance — P1
Top: **Conflict-Minerals declaration (3TG — Tin/Tantalum/Tungsten/Gold)**, **REACH SVHC** (EU substances of very high concern), **RoHS Compliance**, **ITAR Controlled**, **EAR99 / ECCN classification**, **FDA UDI** (medical-device unique identifier), **SDS document FK**, **GHS hazard pictograms**, **Prop-65 warning**, **WEEE / EOL recycling code**. **CherryAI:** has IsFDARegulated, IsOSHACompliance, IsHazmat, HazmatClass (string), CountryOfOrigin, HTSCode. **Missing:** REACH, RoHS, ITAR, EAR/ECCN, UDI, SDS link, GHS, conflict-minerals, prop-65. **Recommend P1:** **mandatory for regulated industries** (medical, aerospace, electronics, EU exports). Extend existing `RegulatoryProfile` pattern from production to Item.

---

## Part 2 — Multi-Dimensional Inventory (MDI)

**Definition:** Multi-Dimensional Inventory lets the same SKU be tracked along multiple orthogonal dimensions simultaneously:
- **Lot/Batch** — chemical, food, pharma, regulated (links to MaterialMaster heat-number)
- **Serial** — individual asset (FA prefix), high-value parts
- **Variant/Configuration** — size/color/style (apparel, MTO assemblies)
- **Location** — Site → Warehouse → Zone → Bin → Shelf → Position
- **Status** — Available / Quarantine / Reserved / Allocated / In-Transit / Damaged / MRB / Hold
- **Owner** — Customer-owned / Vendor-consigned / Tooling / Returnable Container
- **Quality** — Conforming / Rework / Quarantine / Scrap

Each combination has its own quantity. **This is how SAP, Oracle Cloud, D365, NetSuite all model on-hand** — they differ in how many of those dims are first-class vs attribute-of-batch.

### How the leaders do it

- **SAP MM** — stock-key tuple of **Plant × Storage-Location × Batch × Special-Stock-Indicator** (special-stock = consignment / customer-owned / project / sales-order). Stock status is a separate field (`S` unrestricted, `Q` quality, `B` blocked). Serial numbers ride on top via `SER01`-family tables.
- **Oracle Cloud Inventory** — **Organization × Subinventory × Locator × Lot × Serial × Status × Grade × Project-Costing-Reference** as the on-hand grain. Status is a configurable enum tied to allowed transactions.
- **D365 F&O** — the **inventory dimension hierarchy**: Product dims (Color/Size/Style/Config), Storage dims (Site/Warehouse/Location/License Plate), Tracking dims (Batch/Serial/Owner). Each dim independently enabled per item. **Most flexible model in the market** — what CherryAI should target.
- **NetSuite** — flatter: Location + Bin + Lot + Serial + Status.

### Canonical schema pattern

```sql
OnHandQuantity (
    Id PK,
    TenantId, CompanyId,
    ItemId FK,
    SiteId FK, WarehouseId FK, ZoneId, BinId FK,           -- location dim
    LotNumber, ProductionBatchId FK NULL,                  -- lot dim (link Phase E.2 batches)
    SerialNumber, StockReceiptId FK NULL,                  -- serial dim (link heat-num)
    VariantId FK NULL,                                     -- variant dim (color/size/config)
    InventoryStatus enum (Available/Quarantine/Reserved/
        Allocated/InTransit/Damaged/MRB/Hold),
    OwnerType enum (Owned/Consigned/Customer/Returnable),
    OwnerRefId NULL,                                       -- owner dim (vendor/customer FK)
    QualityState enum (Conforming/Rework/Scrap/PendingQA),
    ProjectId FK NULL,                                     -- project / cost-object dim
    QuantityOnHand decimal,
    QuantityReserved decimal,
    QuantityAllocated decimal,
    LastMovementUtc, LastCountUtc, ExpirationDate,
    RowVersion (concurrency)
)
UNIQUE INDEX (Item, Site, Warehouse, Bin, Lot, Serial, Variant, Status, OwnerType, OwnerRefId, Quality, Project)
INDEX (Item) INCLUDE (QtyOnHand) -- fast aggregate
INDEX (LotNumber) -- traceability
INDEX (SerialNumber) -- serial history
INDEX (ExpirationDate WHERE NOT NULL) -- FEFO
```

Plus a **transaction log** (CherryAI already has `ItemTransaction` — extend with same dim FKs) that **debits source** + **credits destination** = paired transactions = double-entry inventory.

### Picker / scan flow (defines what dims are first-class)

1. **Scan Item barcode** → resolves Item
2. If item is **Lot-controlled** → prompt Lot (or scan lot barcode / mill-cert OCR)
3. If item is **Serial-controlled** → prompt Serial (scan IUID/DataMatrix)
4. **Scan Bin** → resolves Bin (+ derived Warehouse + Site)
5. Pick from matching on-hand row (status MUST be Available, owner MUST match expected). FEFO sort by ExpirationDate when present.
6. Confirm. System decrements source row, creates outbound transaction.

CherryAI should ship the **Cmd-K voice equivalent** of this flow per the Voice-First AI Co-Pilot vision.

### What CherryAI has today

`ItemInventory` is a flat per-Item-per-Location row with `LotNumber` (string), `SerialNumber` (string), `QuantityOnHand`, `QuantityReserved`, `QuantityAvailable` (computed), `QuantityOnOrder`, `ExpirationDate`. Workable Phase-1 — but LotNumber/SerialNumber being string columns on the **same row** means **two batches of the same item at the same location cannot be represented** (PK collides). No Status, Owner, Quality, Variant, or Project dim. Transactions table is in better shape — already carries FromLocation/ToLocation/FromBin/ToBin/LotNumber/SerialNumber.

### Phased rollout

- **Phase 1 (Sprint 8 PR #1-#3):** `OnHandQuantity` table with Site + Warehouse + Bin + Lot + Status dims. Backfill from `ItemInventory`. Add status enum default Available. Status transitions emit transactions. Quarantine and Reserved become real, not free-text. **Adds:** lot, status, location-tree.
- **Phase 2 (Sprint 8 PR #4-#5):** Add Serial dim + UDI/IUID barcode parser. Link serial rows to StockReceipt (heat-number inheritance). Build serial-history report.
- **Phase 3 (Sprint 8 PR #6-#7):** Add Owner dim (Owned/Consigned/Customer/Returnable) + OwnerRefId FK. Build consignment-settlement transaction type. Build returnable-container balance report.
- **Phase 4 (Sprint 9):** Add Quality dim + MRB integration; Variant dim with VariantId FK (if/when MTO is on roadmap). Add Project dim for project-stock segregation.
- **Phase 5 (Sprint 9):** Cmd-K + voice picker flow on top of MDI.

---

## Part 3 — ItemEdit Page Restructure

Today's `ItemEdit.cshtml` has **7 tabs** — Basics / Revisions / Manufacturer Parts / Vendor Parts / Approved Vendors / Alternates / Supersession. The "Basics" tab is a **~2200-line single page** containing Item Details, Procurement, Inventory, Costing, Hazmat, Storage, Warranty, Compliance, Physical Attributes, Reorder, EOQ, Buyer, Barcode — Dean's "ton of real estate" complaint.

### Recommended 11-tab structure (modeled on SAP MM views + D365 FastTabs)

| Tab | Sections (DataCard groupings) | Voice-AI priority |
|---|---|---|
| **Basics** | Identification (PN, Description, Long Desc, Drawing#, UPC/GTIN) · Classification (Type, Category, Group, ABC, XYZ, Make-Buy, Plannable) · UOM (Stock, Purchase, Sales, Conversion table, Weight/Dim) · Status & Lifecycle (Active, LifecycleStage, EOL date) | **High** |
| **Sourcing** | Preferred Vendor · AVL · Vendor Parts · MPNs · Alternates · Supersession · Buyer | High |
| **Inventory & Storage** | Stock Policy · Tracking Type (Lot/Serial) · Shelf Life · FIFO/LIFO/FEFO · Cycle-count freq · Storage class (Hazmat/ESD/Temp) · Default Bin · Pack Hierarchy | High |
| **Planning / MRP** | Planning Method · Lot Sizing · Min/Max · ROP · Safety Stock · Lead Time · Service-Level Target · Time Fences · Forecast · EOQ inputs · Reorder Method · Auto-Reorder | Medium |
| **Manufacturing** | Default BOM · Default Routing · Default Work Center · Phantom · Backflush · Scrap % · Yield % · Setup Time · Run Time | Low (links out) |
| **Quality** | MTR Required · CoC Required · FAI Required · Inspection Plan · AQL · Sampling Plan · Cpk target · MRB workflow | **High** (receiving) |
| **Costing / GL** | Cost Method · Standard / Average / Last · Landed-cost components · Cost rollup (Mat/Lab/OH/OS) · GL accounts override · Tax Category · Currency · List Price | Low |
| **Regulatory** | Country of Origin · HTS · ECCN/EAR · ITAR · REACH · RoHS · Conflict Minerals · FDA UDI · GHS · SDS link · Prop-65 · OSHA · Hazmat class & UN# | **High** |
| **Documents & Media** | Images · Spec URL · Drawing · SDS · MTR template · External catalog links | Medium |
| **Revisions** | (Existing — keep as-is) | — |
| **Where-Used** | Computed read-only — what BOMs / Recipes / Assets reference this | **High** (voice "where is this used") |

### Density / progressive disclosure

- Each tab opens with the **5-8 most-used fields visible**; the rest sit behind a per-section **"Show advanced"** disclosure toggle (state persisted per-user in localStorage)
- **Compact mode** toggle in the top-right of the form area — switches to 3-col grid + 0.5rem padding. Persisted per-user.
- **Field-level help icons** revealing 1-line definition + which ERP / standard mandates it (mini brand-trust)
- **DataCard primitive** wraps every grouping with consistent header + collapse + edit pencil

### Voice-readiness — fields the AI will most frequently want

Read-most: PartNumber, Description, OnHand qty, Reorder point, Preferred Vendor, Last Cost, Lead Time, Lot/Serial-controlled flags, Hazmat flag, Where-used.
Update-most: Status, AutoReorderEnabled, ReorderPoint, SafetyStock, MtrRequired, IsHazmat, PreferredVendor, DefaultBin.
Ship the page-model through `VoiceReadyPageModel`. Annotate each field with `[VoiceReadable]` / `[VoiceUpdatable]` attributes per ADR-014.

### Master-Data Completeness Score

Sibling to BuyabilityScore. Roll up all 12 categories with a Tier (Bronze/Silver/Gold/Platinum) badge on the hero. Click "Why?" → reveal per-category percent, missing fields with deep-link to tab. Makes the field-count problem **gamified** rather than overwhelming.

---

## Recommended Sprint Structure — 18 PRs over 3 sprints

### Sprint 7 — Item Master Expansion (5 PRs)

- **PR #200 Identification & Classification expansion** (S): UPC/GTIN, ECCN, CAS, Drawing#, XYZ class, Make/Buy, Plannable, Rotating, ProductFamily. ~12 columns. Migration only, no UI yet.
- **PR #201 UOM normalization** (M): new `ItemUomConversion` table, deprecate `PurchaseConversion` decimal; add Sales UOM, Reporting UOM, tare weight, pack hierarchy.
- **PR #202 Planning & MRP fields** (L): PlanningMethod enum, LotSizingRule enum, TimeFence days, ServiceLevelTarget %, forecast horizon, MTO/ETO/PTO flags. Add `[VoiceUpdatable]` attribs.
- **PR #203 Quality fields** (M): MtrRequired, CocRequired, FaiRequired, AqlLevel, SamplingPlanCode, InspectionPlan FK. Wire into Receiving flow.
- **PR #204 Regulatory expansion** (L): Conflict-Minerals, REACH, RoHS, ITAR, EAR/ECCN, FDA UDI, SDS link, GHS, Prop-65. Link pattern to existing `RegulatoryProfile`.

### Sprint 8 — Multi-Dimensional Inventory (7 PRs)

- **PR #210 OnHandQuantity table + backfill from ItemInventory** (XL): wide-key fact table; preserve existing API on `ItemInventory` via a view for backwards compat.
- **PR #211 Status dim + transitions** (M): InventoryStatus enum, quarantine/MRB hooks.
- **PR #212 Serial dim + UDI parser** (M): serial rows, GS1 + UDI scanner, link to StockReceipt.
- **PR #213 Owner dim** (M): Owned/Consigned/Customer/Returnable + consignment settlement transaction type.
- **PR #214 Quality dim + MRB integration** (M).
- **PR #215 Project / Cost-Object dim** (S).
- **PR #216 Voice picker flow** (M): voice trigger "pick X" → walk dim prompts → confirm.

### Sprint 9 — ItemEdit Page Restructure (6 PRs)

- **PR #220 New tab skeleton** (S): expand from 7 tabs to 11.
- **PR #221 DataCard groupings + advanced-disclosure toggle** (M).
- **PR #222 Master-Data Completeness Score** (M): sibling to BuyabilityScore.
- **PR #223 Where-Used computed tab** (M): live join across BOM/Recipe/Asset/WorkOrder.
- **PR #224 VoiceReadable/VoiceUpdatable attribution + page-model wiring** (M).
- **PR #225 Mobile polish + compact-mode toggle** (S).

### Sprint 10 — Sales-side & Costing depth (optional, only if Dean greenlights full ERP)

- IsSalable, Customer-Item Xref, drop-ship, ATP/CTP, landed-cost components, cost rollup, per-item GL overrides.

**Total scope:** 18 PRs over 3 sprints. Sprints 7-8 are hard-to-reverse migrations (schema + backfill). Sprint 9 is mostly Razor / TagHelper. The whole arc moves CherryAI from "fixed-asset + maintenance + procurement" to a credible **lite ERP** with regulated-industry table stakes.

---

## Sources

- SAP MM Material Master Views — Guru99 / LearntoSAP / ERProof
- Oracle Cloud Inventory Item Attributes (25a) — Oracle docs
- Microsoft Learn — D365 UI Elements (FastTabs) + Master planning warehouse coverage
- NetSuite Inventory Items — SuiteRep / VNMT
- IBM Maximo Rotating Items / Item Spare Part / Add to Storeroom — Maximo Secrets
- ISO 8000-110:2021 — Master Data Quality (ISO standard)
- Verdantis — Item Master Data Management guide
