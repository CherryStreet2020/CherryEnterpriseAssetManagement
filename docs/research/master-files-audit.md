---
title: Master Files Audit — Cross-Vertical Readiness
date: 2026-05-23
status: Locked
sprint: 13.5
gates: PR #4 (Manufacturing UI shell)
---

# Master Files Audit — Cross-Vertical Readiness

Sprint 13.5 has shipped the CustomerProject + ProjectAmendment + AS9102 FAI
foundation (PR #1 / #1.5 / #1.75). Before PR #4 lights up the first
user-visible Manufacturing surface — and PR #5 lights up the Customer Project
Cockpit — we need to know what reference data the new screens will reach for
that **does not exist yet**.

The audit was performed by reading `Models/*.cs` + `Data/AppDbContext.cs`
exhaustively (every file, no narrative-scoping). What follows is grounded in
the actual schema as of commit on `main`.

---

## 1. Executive summary

| # | Gap | Verticals affected | Severity | Recommended PR |
|---|---|---|---|---|
| 1 | `Company.IndustryVertical` enum + value | ALL (gates UI/voice routing) | **BLOCKS-13.5** | **PRA-1** |
| 2 | First-class `Carrier` master (today free-text on ASN + ShippingMethod) | ALL — gates Shipping CC | **BLOCKS-13.5** | **PRA-1** |
| 3 | `Customer.DefaultQualityProgram` + `DefaultExportControl` + `DefaultContractType` (inheritance for project create) | Aero/Def/Auto/Med | **BLOCKS-13.5** | **PRA-1** |
| 4 | `Customer.CageCode` / `Vendor.CageCode` / `Vendor.FdaEstablishmentId` / `Vendor.DeaRegistration` (regulator-issued IDs we always-on store) | Aero/Def/Pharma/Cannabis | **BLOCKS-13.5** | **PRA-1** |
| 5 | `Country` + `Subdivision` master tables (today every address is free-text) | ALL (export control, tax, shipping) | **BLOCKS-V1-LAUNCH** | **PRA-2** |
| 6 | `WorkCalendar` + `Holiday` master (cockpit "due today", capacity, EVM) | ALL | **BLOCKS-V1-LAUNCH** | **PRA-2** |
| 7 | `ReasonCode` master (transaction-reason codes — scrap / quarantine / adjustment / hold / cancellation) | ALL | **BLOCKS-V1-LAUNCH** | **PRA-3** |
| 8 | Replace `Vendor.PaymentTerms` enum with FK to existing `PaymentTerm` master | ALL | **BLOCKS-V1-LAUNCH** | **PRA-3** |
| 9 | `Customer.PaymentTermId` already exists as FK but no `CreditLimit` / `TaxCode` / `BillToAddress != ShipToAddress` | ALL | **BLOCKS-V1-LAUNCH** | **PRA-1** |
| 10 | `Item` regulatory fields: `DeaSchedule` / `MslLevel` / `Eccn` / `RoHS` / `IpcClass` | Pharma/Electronics/Aero | **BLOCKS-SPRINT-14+** | (Sprint 14 PR — out of scope) |
| 11 | Allergen / Ingredient / SOP / NDC catalogs | Food / Pharma | **V2 unless food/pharma customer signs** | deferred |
| 12 | `MetrcLicense` / `terpenes` / COA refs | Cannabis | V2 (no cannabis customer yet) | deferred |
| 13 | FX `ExchangeRate` history exists; missing `Currency.IsBaseTenantCurrency` lookup per tenant | Multi-currency tenants | **BLOCKS-V1-LAUNCH** | PRA-3 |
| 14 | `IndustryVertical`-aware seeding of NumberingSequence / ReceiptProfile defaults | ALL | folded into PRA-1 | PRA-1 |

**Net recommendation:** 3 small additive PRs (**PRA-1**, **PRA-2**, **PRA-3**)
ship in parallel with the Sprint 13.5 service PRs. PRA-1 is the hard gate on
PR #4 / PR #5 cockpit ship. PRA-2 + PRA-3 should clear before the v1 launch
but do not block 13.5.

---

## 2. Dimension 1 — Existing master inventory

Sourced from `Data/AppDbContext.cs` DbSets section + every `Models/*.cs`. A
master in this audit is any reference table whose rows are configured once
per tenant/company and consumed by transactional entities.

### 2.1 Organizational

| Entity | File | Status | Notable columns / gaps |
|---|---|---|---|
| `Tenant` | `Models/Tenant.cs` | EXISTS — thin | Name/Code/IsActive. **Missing `IndustryVertical`** for tenant-wide template selection. |
| `Company` | `Models/Company.cs` | EXISTS — fat | 50+ cols incl. CompanyType, Currency, FiscalYearStart, all module toggles. **Missing `IndustryVertical`** (the single most-mentioned gap downstream). PR #1.5 added `ProjectExportControlRequired` — good pattern. |
| `Site` | `Models/Site.cs` | EXISTS — fat | SiteType enum (Manufacturing, Warehouse, Office, …), lat/lng, capacity, shifts. Solid. |
| `Location` | `Models/GlAccount.cs` lines 247-350 | EXISTS | Site-scoped, hierarchical, Building/Floor/Bay/Aisle/Rack/Shelf/Bin granularity. **Note**: file location is misleading — it lives in `GlAccount.cs`. |
| `OrgNode` | `Models/OrgNode.cs` | EXISTS — generic | Tree of org units. Unused by 13.5. |

### 2.2 People

| Entity | File | Status | Notes |
|---|---|---|---|
| `User` | `Models/User.cs` | EXISTS — thin | Username/Hash/Role text. AssignedCompany + AssignedSite present. No first/last split, no employee #, no badge #. |
| `Technician` | `Models/Technician.cs` | EXISTS — fat | Site/Department/CostCenter/Supervisor/Shifts/Crafts/HourlyRate. Solid. |
| `ProjectManager` | `Models/ProjectManager.cs` | EXISTS — thin | Already wired to CustomerProject. |
| `TechnicianCertification` / `TechnicianSkill` | own files | EXISTS | |

### 2.3 Commercial

| Entity | File | Status | Notes |
|---|---|---|---|
| `Customer` | `Models/Customer.cs` | EXISTS — thin (~60 lines) | Code/Name/contact/Address/TaxId/Currency/PaymentTermId. **Missing**: CreditLimit, TaxCodeId FK, BillTo address (separate from Ship-to), DefaultQualityProgram, DefaultExportControl, DefaultContractType, CageCode, DUNS, AS9100 cert ref. Lean by intent — must thicken for project inheritance. |
| `Vendor` | `Models/Vendor.cs` | EXISTS — fat (~158 lines) | Vendor[Status,Type], PaymentTerms enum (NOT FK), Currency, CreditLimit, 1099, ADR-015 DefaultReceiptAttributes + SendsAsn + AsnFormat. **Missing**: CageCode, FdaEstablishmentId, DeaRegistration, ItarRegistration, UEI / SAM.gov number, ISO/AS cert refs. |
| `Manufacturer` | `Models/Manufacturer.cs` | EXISTS — thin (~48 lines) | Code/Name/Country/Contact/Address. Already tenant-scoped. **Missing** same regulator IDs as Vendor (FDA / CAGE) — manufacturers carry many of them more often than vendors. |
| `Carrier` | **DOES NOT EXIST** | **GAP** | Today: `AdvancedShippingNotice.Carrier` is a free-text `string?` (StringLength 50). `ShippingMethod` has a `Carrier` free-text string too. No SCAC code, no API integration, no contact. Required for Shipping CC. |

### 2.4 Product / Item

| Entity | File | Status | Notes |
|---|---|---|---|
| `Item` | `Models/Item.cs` | EXISTS — very fat (~800 lines incl. nested) | Best-in-class column inventory: PartNumber/Description, Type/Status/Category, UoM (text + enum), CostMethod, Standard/Average/LastPurchase cost, ListPrice, Tracking, Min/Max/Reorder/Safety/Lead, Default/Preferred Vendor + Manufacturer, ABC class, Hazmat, ShelfLifeDays, MSL/IPC/RoHS-style fields NOT YET (only IsFDARegulated + Country/HTSCode), ADR-015 DefaultReceiptProfileId + DefaultReceiptAttributes. **Missing for verticals**: DeaSchedule, MslLevel, Eccn, RoHSCompliant, IpcClass — see Dimension 2. |
| `ItemCategory` | `Models/Item.cs` line 122 | EXISTS | Tree, default GL accounts. Solid. |
| `ItemRevision` | `Models/Item.cs` line 791 | EXISTS | (revision metadata) |
| `ItemVendor` | `Models/Item.cs` line 642 | EXISTS | M:N item↔vendor pricing per company. |
| `ItemCompanyStocking` | `Models/Item.cs` line 553 | EXISTS — fat | Per-company stocking overrides — Min/Max/Reorder per company, preferred vendor, default location, ABC. Maximo pattern. Solid. |
| `ItemManufacturerPart` / `VendorItemPart` | `Models/Revisions/*` | EXISTS | Manufacturer-part-number + vendor-part-number cross-ref. |
| `MaterialMaster` | `Models/Production/MaterialMaster.cs` | EXISTS | ShopCode + ASTM + form + density. Wired to StockReceipt. |
| `ReceiptProfile` | `Models/Production/ReceiptProfile.cs` | EXISTS — 12 seeded | STEEL/PHARMA/FOOD/CHEMICAL/ELECTRONICS/MEDICAL_DEVICE/AEROSPACE/CANNABIS/AUTOMOTIVE/APPAREL/CONSTRUCTION/OIL_GAS. ADR-015 industry-agnostic schema. |
| `RegulatoryProfile` | `Models/Production/RegulatoryProfile.cs` | EXISTS | AS9100 / FDA 21 CFR 820 / NADCAP / REACH gating. JSON Gates. |

### 2.5 Financial / Accounting

| Entity | File | Status | Notes |
|---|---|---|---|
| `GlAccount` | `Models/GlAccount.cs` | EXISTS | Full chart of accounts. |
| `CostCenter` | `Models/GlAccount.cs` line 139 | EXISTS | Hierarchical, Type enum (Plant/Building/Line/WorkCell). |
| `Department` | `Models/GlAccount.cs` line 196 | EXISTS | Type enum (15 values). |
| `AssetCategory` | `Models/GlAccount.cs` line 386 | EXISTS | MACRS class + GL defaults. |
| `Currency` | `Models/SystemConfig.cs` line 115 | EXISTS — thin | Code/Name/Symbol/DecimalPlaces. Single `IsBaseCurrency` bool. **Per-tenant base** is not modeled — assumes one global base. |
| `ExchangeRate` | `Models/ExchangeRate.cs` | EXISTS | FX history. |
| `TaxCode` | `Models/SystemConfig.cs` line 139 | EXISTS — thin | Code/Rate/Type/Authority. No jurisdiction FK. |
| `PaymentTerm` | `Models/SystemConfig.cs` line 48 | EXISTS — thin | DueDays/DiscountPercent/DiscountDays. Master EXISTS but `Vendor.PaymentTerms` still uses ENUM not FK. Inconsistency. |
| `FiscalYear` / `FiscalPeriod` / `PeriodLock` | `Models/FiscalPeriod.cs` etc. | EXISTS | Per-company. |

### 2.6 Operational

| Entity | File | Status | Notes |
|---|---|---|---|
| `PMTemplate` / `PMTemplateItem` / `PMTemplateAsset` | `Models/PMTemplate.cs` | EXISTS | |
| `PMSchedule` | `Models/PMSchedule.cs` | EXISTS | Cadence + DaysOfWeekMask + NextDueDateUtc. **No WorkCalendarId** — so "skip weekends/holidays" is impossible without it. |
| `Kit` / `KitItem` | inline in another file | EXISTS | (Registered in DbSet) |
| `ApprovalWorkflow` / `ApprovalAction` | `Models/SystemConfig.cs` + own file | EXISTS | |
| `NumberingSequence` | `Models/SystemConfig.cs` line 5 | EXISTS | Prefix/Suffix/NextNumber/Reset logic. Already in active use (FAI uses it). |
| `EquipmentClass` / `EquipmentModel` / `SensorProfile` | `Models/Catalog/*` | EXISTS | Curated asset templates. |

### 2.7 Reference (truly cross-cutting)

| Entity | File | Status | Notes |
|---|---|---|---|
| `LookupType` / `LookupValue` | own files | EXISTS — universal | Code+Name+Sort+jsonb Metadata. The "swiss army knife" for enum-soft tables. Used everywhere already. |
| `Country` master | **DOES NOT EXIST** | **GAP** | Every Address.Country is `string?` free-text ("United States" vs "USA" vs "US"). Export-control and tax both depend on canonical ISO-3166. |
| `Subdivision` (state/province) master | **DOES NOT EXIST** | **GAP** | Same as Country — StateProvince is everywhere free-text. ISO-3166-2 codes are required for tax + shipping. |
| `TaxJurisdiction` master | **DOES NOT EXIST as table** | **GAP** | An enum named `TaxJurisdiction` exists in `Enums.cs` (line 36) but only for CAD vs USA depreciation — not a tax-rate jurisdiction. |
| `UoM` master | EXISTS as `UOMDefinition` | EXISTS but underused | `Item.StockUOM` / `Item.PurchaseUOM` are STILL string fields and `Item.UOM` is the `UnitOfMeasure` enum (from `Models/Telemetry/UnitOfMeasure.cs`). PR #119.12 namespace-collision veteran. UoM FK not yet plumbed. |
| `WorkCalendar` + `Holiday` master | **DOES NOT EXIST** | **GAP** | Without it: no "due today (business days)", no capacity, no holiday-aware EVM, no "skip weekends" PM. |
| `ReasonCode` master | **DOES NOT EXIST** | **GAP** | Today every scrap/quarantine/cancellation uses a free-text Notes field or hard-coded enum. Reporting is impossible. |

---

## 3. Dimension 2 — Cross-vertical gaps

Each row below names master columns that the vertical's regulator / customer
expects, mapped to what we have today.

### 3.1 Machining (ABS — primary, demo Thursday)

| Need | Today | Gap |
|---|---|---|
| AS9102 FAI workflow | Shipped (PR #1.75) | none |
| Material master (heat#, ASTM, mill cert) | StockReceipt + MaterialMaster | none |
| Customer-required quality program | `CustomerProject.QualityProgram` (PR #1.5) | **Missing `Customer.DefaultQualityProgram` for inheritance** |
| Project-customer commit | `CustomerProject` (PR #1) | none |
| EAR / ITAR | `CustomerProject.ExportControl` (PR #1.5) | **Missing `Customer.DefaultExportControl`** |

### 3.2 ETO Precision (EVS June 3 demo)

| Need | Today | Gap |
|---|---|---|
| Engineer-to-order mode on project | `CustomerProjectMode.EngineerToOrder` | none |
| FAI + Material cert + heat# | shipped | none |
| Customer PO number on project | `CustomerProject.CustomerPoNumber` (PR #1.5) | none |
| **Contract type** (FFP / CPFF / IDIQ) | `CustomerProject.ContractType` (PR #1.5) | **Missing `Customer.DefaultContractType`** |
| Revenue mode (POC) | `CustomerProject.RevenueMode` | none |

### 3.3 Food sciences

| Need | Today | Gap |
|---|---|---|
| Recipe master | `Models/Production/Recipe.cs` | none |
| Lot/batch traceability | StockReceipt + ProductionBatch | none |
| FDA Establishment ID on Vendor | `Vendor.TaxId` text | **`Vendor.FdaEstablishmentId` column missing** |
| Allergen master | **NONE** | V2 — defer until food customer signs |
| Ingredient master (vs Item) | Item carries it | acceptable; could specialize in v2 |
| FSMA-204 traceability lot codes | StockReceipt jsonb under FOOD profile | already shipped; profile-driven |
| USDA registration on Vendor | None | V2 |
| Cold-chain temperature range | Item.MinStorageTemp / MaxStorageTemp | sufficient for v1 |

### 3.4 Pharma

| Need | Today | Gap |
|---|---|---|
| NDC / GTIN catalog on Item | `Item.UNSPSCCode` + `Item.CommodityCode` exist but no NDC | V2 (real pharma customer required) |
| DEA Schedule on Item | None | **Item.DeaSchedule (enum I-V) — folded into PRA-1 stretch** |
| Cold chain temp range | exists on Item | none |
| FDA Establishment ID on Vendor | None | **PRA-1** |
| DEA registration on Vendor | None | **PRA-1** |
| Drug Master File ref | None | V2 |
| 21 CFR Part 11 e-sig audit | not modeled separately | folds into existing AuditLog (acceptable) |

### 3.5 Cannabis

| Need | Today | Gap |
|---|---|---|
| METRC integration master | None | V2 — no cannabis customer yet |
| State license # on Customer / Vendor | None (TaxId only) | V2 |
| Terpene profile / COA refs | StockReceipt under CANNABIS profile (jsonb) | exists |

### 3.6 Electronics

| Need | Today | Gap |
|---|---|---|
| ECCN on Item | None | **Item.Eccn — folded into PRA-1 stretch** |
| MSL level on Item | None | **Item.MslLevel** (folded) |
| RoHS / REACH flags on Item | None (Certifications free-text) | **Item.RoHSCompliant + Item.ReachCompliant** (folded) |
| IPC class on Item | None | **Item.IpcClass** (folded) |
| Country of origin (export) | `Item.CountryOfOrigin` text | exists but free-text — depends on Country master |

### 3.7 Aerospace / Defense

| Need | Today | Gap |
|---|---|---|
| CAGE code on Vendor / Manufacturer / Company | None | **PRA-1** |
| NSN catalog | Item.PartNumber sufficient as v1 alias | V2 dedicated NSN reference table |
| CUI marking | `CustomerProject.ExportControl` covers ITAR/EAR; CUI is finer | acceptable for v1; folds into ExportControl |
| ITAR / EAR jurisdiction | `CustomerProject.ExportControl` (PR #1.5) | shipped |
| AS9100 cert refs | None (Certifications free-text on Item) | **`Vendor.As9100CertRef` (text) — PRA-1 stretch** |
| FAI per AS9102 | shipped (PR #1.75) | none |
| Quality program FK on Customer | None | **`Customer.DefaultQualityProgram` — PRA-1** |

### 3.8 Automotive

| Need | Today | Gap |
|---|---|---|
| IATF 16949 | `QualityProgram.Iatf16949` exists in enum | none |
| PPAP workflow | Not modeled (similar to FAI) | Sprint 14+ |
| Production part approval ref on Item | None | V2 |

### 3.9 Med Device

| Need | Today | Gap |
|---|---|---|
| ISO 13485 cert ref | None | **`Vendor.Iso13485CertRef` — PRA-1 stretch** |
| FDA 21 CFR 820 gating | `RegulatoryProfile` MEDICAL_DEVICE | shipped |
| UDI on Item | None | V2 |

### 3.10 Oil & Gas

| Need | Today | Gap |
|---|---|---|
| API spec on Vendor | None | V2 |
| H2S / pressure-class on Item | Hazmat fields cover most | acceptable v1 |
| Mill cert + heat number | StockReceipt under STEEL profile | shipped |

### 3.11 Cross-vertical (every vertical needs)

| Need | Today | Gap |
|---|---|---|
| Country master (ISO-3166) | None | **PRA-2** |
| Subdivision master (ISO-3166-2) | None | **PRA-2** |
| Tax Jurisdiction master | None | **PRA-3 (depends on Country)** |
| WorkCalendar + Holiday master | None | **PRA-2** |
| Carrier master (SCAC, contact, API) | Free-text on ASN + ShippingMethod | **PRA-1** |
| ReasonCode master | None | **PRA-3** |
| PaymentTerm FK on Vendor (currently enum) | enum | **PRA-3** |
| `IndustryVertical` on Company | None | **PRA-1** |
| Multi-currency-per-tenant base | `Currency.IsBaseCurrency` single flag | **PRA-3 (Currency.BaseForTenantId scoping)** |

---

## 4. Dimension 3 — BLOCKS-13.5 severity ranking

Top 10 in priority order. These are the ones that, if absent on the day PR #4
ships, will produce visible bad-UX or block voice intents.

1. **`Company.IndustryVertical` enum + column.** Without it the cockpit
   can't choose between "show FAI tab by default" (Aero/Med) and "hide it"
   (commercial machining). Voice routing can't disambiguate "schedule" between
   maintenance and shop schedule. **Touches a single column** but unlocks every
   cockpit branching decision.
2. **`Carrier` master table.** PR #5 Project Cockpit shows "Inbound shipment
   from Acme Steel — UPS — tracking 1Z9999". Today that's three free-text
   columns on ASN. PR #4 Manufacturing UI also needs it for outbound. Without
   a master, the Shipping CC has nothing to filter on, and Sprint 21 EDI 856
   ingestion has no canonical carrier identity.
3. **`Customer.DefaultQualityProgram` + `DefaultExportControl` + `DefaultContractType` + `DefaultRevenueMode`.**
   PR #5's "Create Project for [Customer]" form must inherit these defaults.
   Without them, every aero customer's PM types FAI-required from scratch
   each project, and every commercial customer is one mis-click from ITAR
   gating. The whole point of PR #1.5 adding these to CustomerProject was so
   the project could enforce them; the Customer-side inheritance is the
   matching piece.
4. **Regulator-issued ID columns on `Customer` / `Vendor` / `Manufacturer`:**
   `CageCode` (Aero/Def), `FdaEstablishmentId` (Pharma/Food), `DeaRegistration`
   (Pharma), `As9100CertRef` / `Iso13485CertRef` / `Iso9001CertRef` (text).
   These are STORE-ONLY (no enforcement) but every aero/pharma customer
   demands they live in the master so they appear on PO / packing slip /
   FAI / certificate of conformance.
5. **`Customer.BillToAddress` separate from ship-to + `CreditLimit` + `TaxCodeId` FK.**
   Customer today has one address. Real customers bill HQ, ship to plants.
   PR #5 cockpit will surface "Credit utilization" as a header KPI — needs
   the column.

Severity 6-10 (BLOCKS-V1-LAUNCH; can ship as PRA-2 / PRA-3 in parallel):

6. **`Country` master** — ISO-3166-1 alpha-2 + alpha-3 + numeric + display
   name. Required dep of Subdivision + Tax Jurisdiction. Pure reference.
7. **`Subdivision` master** — ISO-3166-2 codes. State/Province/Region of a
   country.
8. **`WorkCalendar` + `Holiday` master** — Per-site / per-company calendar
   of working days + holidays. Required for accurate "due today",
   capacity, and EVM cadence.
9. **`ReasonCode` master** — Single small table keyed by `(EntityType,
   Code)`: ScrapReason / QuarantineReason / CancelReason / AdjustmentReason.
   Drives consistent reporting.
10. **`Vendor.PaymentTermId` FK** (replace the `PaymentTerms` enum). The
    PaymentTerm master already exists; the Vendor model just uses an enum
    instead of FK. Tiny migration; large reporting payoff.

---

## 5. Dimension 4 — Recommended additive PRs

3 additive PRs, all idempotent additive migrations in the style of
`Migrations/20260523_AddCustomerProjectFieldExpansion.cs` (`IF NOT EXISTS`,
`DO $$` existence blocks, `CHECK` constraints, no destructive ops).

### PRA-1 — Industry-vertical, Carrier, Customer & Vendor regulator-IDs (BLOCKS-13.5)

| Property | Value |
|---|---|
| Branch | `feat/sprint-13.5-pra1-master-files` |
| Scope | One column on Company; one new table (Carriers); 4-5 new columns on Customer; 4-5 new columns on Vendor; 2-3 new columns on Manufacturer; CHECK constraints on enum columns; new IndustryVertical enum. |
| New tables | 1 (`Carriers`) |
| Altered tables | 4 (`Companies`, `Customers`, `Vendors`, `Manufacturers`); also `AdvancedShippingNotices` and `ShippingMethods` get a nullable `CarrierId` FK alongside the existing free-text. |
| New columns | ~22 across the 4 altered tables (see SQL draft) |
| Effort | **1 day** (no UI, schema + seed) |
| Parallel with | Sprint 13.5 PR #2 (CustomerProjectService) and PR #3 (FaiService) — no overlap with their files. |
| Ships before | PR #4 (Manufacturing UI shell) — gates. |

`Customer` adds:
```
DefaultQualityProgram smallint NULL            -- mirrors CustomerProject enum
DefaultExportControl smallint NULL             -- mirrors CustomerProject enum
DefaultContractType  smallint NULL             -- mirrors CustomerProject enum
DefaultRevenueMode   smallint NULL             -- mirrors CustomerProject enum
CageCode             varchar(10) NULL          -- aero/def
DunsNumber           varchar(13) NULL          -- universal vendor/customer ID
TaxCodeId            int NULL FK TaxCodes(Id)  -- replaces ad-hoc TaxId for tax-calc
CreditLimit          numeric(18,2) NULL
BillToAddress        varchar(200) NULL         -- separate from ship-to
BillToCity           varchar(100) NULL
BillToStateProvince  varchar(50) NULL
BillToPostalCode     varchar(20) NULL
BillToCountry        varchar(50) NULL
```

`Vendor` adds:
```
CageCode             varchar(10) NULL
DunsNumber           varchar(13) NULL
UeiNumber            varchar(12) NULL          -- SAM.gov successor to DUNS
FdaEstablishmentId   varchar(20) NULL
DeaRegistration      varchar(20) NULL
As9100CertRef        varchar(60) NULL
Iso9001CertRef       varchar(60) NULL
Iso13485CertRef      varchar(60) NULL
Itar Registration    varchar(40) NULL
PaymentTermId        int NULL FK PaymentTerms(Id)  -- service layer can still read enum during transition
```

`Manufacturer` adds:
```
CageCode             varchar(10) NULL
DunsNumber           varchar(13) NULL
```

`Company` adds:
```
IndustryVertical     smallint NOT NULL DEFAULT 0   -- 0 = Unspecified
CageCode             varchar(10) NULL              -- Cherry Street IS one; demo manufacturers ARE; aero customers expect it on their own profile
```

New `Carriers` table:
```
Id           int PK identity
CompanyId    int NULL FK Companies
Code         varchar(10) NOT NULL    -- short code ("UPS")
ScacCode     varchar(4)  NULL        -- Standard Carrier Alpha Code
Name         varchar(100) NOT NULL
ContactName  varchar(100) NULL
ContactEmail varchar(100) NULL
ContactPhone varchar(30)  NULL
WebsiteUrl   varchar(200) NULL
TrackingUrlTemplate varchar(300) NULL  -- e.g. "https://www.ups.com/track?tracknum={0}"
ApiEndpoint  varchar(300) NULL
ApiAuthRef   varchar(100) NULL        -- key id, not the secret
IsActive     boolean NOT NULL DEFAULT TRUE
SortOrder    int NOT NULL DEFAULT 0
CreatedAt    timestamptz NOT NULL DEFAULT now()
ModifiedAt   timestamptz NULL
UNIQUE (CompanyId, Code)
UNIQUE (CompanyId, ScacCode)         -- when present
```

`AdvancedShippingNotices` adds `CarrierId int NULL FK Carriers`. Keep the
free-text `Carrier` column for ingestion fall-back. Service layer resolves
free-text → CarrierId where possible.

`ShippingMethods` adds `CarrierId int NULL FK Carriers`. Keep existing
`Carrier` text for migration compatibility.

Seed 12 starter carriers (companywide): UPS / FedEx / DHL / USPS / OnTrac /
LTL national carriers + a "Customer Pickup" + "Will Call" virtual carrier.

### PRA-2 — Country, Subdivision, WorkCalendar, Holiday (BLOCKS-V1-LAUNCH)

| Property | Value |
|---|---|
| Branch | `feat/sprint-13.5-pra2-geo-and-calendar` |
| Scope | 4 new tables; seed Country (~250 ISO rows) + Subdivision (US states, Canadian provinces, Mexican states for v1 — Sprint 14 expands); WorkCalendar/Holiday seed for Cherry Street + ABS demo + EVS demo. |
| New tables | 4 (`Countries`, `Subdivisions`, `WorkCalendars`, `Holidays`) |
| Altered tables | 0 (additive only — addresses stay free-text for now; PRA-3 adds FK columns) |
| Effort | **1 day** (mostly seed) |
| Parallel with | PRA-1, Sprint 13.5 PRs #2-#3. |
| Ships before | v1 launch. PR #4 / #5 do NOT depend on these. |

`Countries` (ISO-3166-1):
```
Id          int PK identity
Alpha2      char(2)  UNIQUE NOT NULL   -- "US"
Alpha3      char(3)  UNIQUE NOT NULL   -- "USA"
Numeric3    char(3)  UNIQUE NOT NULL   -- "840"
Name        varchar(80) NOT NULL       -- display
OfficialName varchar(200) NULL
Region      varchar(40) NULL           -- "Americas"
SubRegion   varchar(40) NULL           -- "Northern America"
IsoSortOrder int NOT NULL DEFAULT 0
IsExportControlConcern boolean NOT NULL DEFAULT FALSE  -- DPRK, Iran, Cuba, etc.
IsActive    boolean NOT NULL DEFAULT TRUE
```

`Subdivisions` (ISO-3166-2):
```
Id          int PK identity
CountryId   int NOT NULL FK Countries
Code        varchar(10) NOT NULL    -- "US-CA"
Name        varchar(80) NOT NULL    -- "California"
Type        varchar(40) NOT NULL    -- "State", "Province", "Region"
SortOrder   int NOT NULL DEFAULT 0
IsActive    boolean NOT NULL DEFAULT TRUE
UNIQUE (CountryId, Code)
```

`WorkCalendars`:
```
Id          int PK identity
CompanyId   int NULL FK Companies        -- NULL = tenant default
SiteId      int NULL FK Sites            -- NULL = company default
Code        varchar(20) NOT NULL
Name        varchar(100) NOT NULL
TimeZoneId  varchar(64) NOT NULL DEFAULT 'America/New_York'
WorkDaysMask smallint NOT NULL DEFAULT 62  -- bit-flag Mon..Sun, Mon-Fri = 62
DefaultDailyHours numeric(4,2) NOT NULL DEFAULT 8.00
IsActive    boolean NOT NULL DEFAULT TRUE
CreatedAt   timestamptz NOT NULL DEFAULT now()
UNIQUE (CompanyId, Code)
```

`Holidays`:
```
Id              int PK identity
WorkCalendarId  int NOT NULL FK WorkCalendars ON DELETE CASCADE
HolidayDate     date NOT NULL
Name            varchar(100) NOT NULL
IsFullDay       boolean NOT NULL DEFAULT TRUE
Notes           varchar(300) NULL
UNIQUE (WorkCalendarId, HolidayDate)
```

### PRA-3 — ReasonCode, PaymentTerm FK retrofit, Tax Jurisdiction (BLOCKS-V1-LAUNCH)

| Property | Value |
|---|---|
| Branch | `feat/sprint-13.5-pra3-reasoncodes-payterm-tax` |
| Scope | 2 new tables (`ReasonCodes`, `TaxJurisdictions`); `Vendor.PaymentTermId` FK retrofit (keep enum for back-compat); seed 30 starter reason codes. |
| New tables | 2 |
| Altered tables | 2 (`Vendors` + `TaxCodes`) |
| New columns | ~3 |
| Effort | **0.5 day** |
| Parallel with | PRA-1, PRA-2, Sprint 13.5 PRs. |
| Ships before | v1 launch. |

`ReasonCodes`:
```
Id          int PK identity
CompanyId   int NULL FK Companies         -- NULL = system-wide
EntityType  varchar(50) NOT NULL          -- "ScrapReason" / "QuarantineReason" / etc
Code        varchar(30) NOT NULL
Name        varchar(100) NOT NULL
Description varchar(300) NULL
IsActive    boolean NOT NULL DEFAULT TRUE
SortOrder   int NOT NULL DEFAULT 0
UNIQUE (CompanyId, EntityType, Code)
```

Starter `EntityType` values:
- `ScrapReason`, `QuarantineReason`, `MrbDispositionReason`, `CancelReason`,
  `HoldReason`, `RejectReason`, `AdjustmentReason`, `TransferReason`,
  `ReturnReason`, `WaiverReason`.

`TaxJurisdictions`:
```
Id          int PK identity
CountryId   int NOT NULL FK Countries
SubdivisionId int NULL FK Subdivisions
LocalCode   varchar(30) NULL              -- city / county code
Code        varchar(30) NOT NULL          -- "US-CA-LA"
Name        varchar(120) NOT NULL
IsActive    boolean NOT NULL DEFAULT TRUE
UNIQUE (Code)
```

`TaxCodes` adds `TaxJurisdictionId int NULL FK TaxJurisdictions`. Keep
existing `TaxAuthority` text for back-compat.

`Vendors` adds `PaymentTermId int NULL FK PaymentTerms`. Service layer reads
FK first, falls back to enum.

---

## 6. Dimension 5 — `Company.IndustryVertical` enum

Stored as `smallint NOT NULL DEFAULT 0`. Locked list:

| Value | Name | Justification |
|---|---|---|
| 0 | `Unspecified` | Default for legacy rows and onboarding. UI prompts user to set. |
| 1 | `Machining` | ABS — primary launch customer. Excludes metal-fab job-shop heavy fabrication. |
| 2 | `MetalFab` | Sheet-metal / structural fabrication. Distinct from Machining because cut-list / nest features are first-class (PR #119.13a-b shipped this). |
| 3 | `PrecisionEto` | Engineer-to-order. EVS June 3 demo. CustomerProjectMode.EngineerToOrder + Quality programs. |
| 4 | `FoodSciences` | Recipe + lot trace + FSMA-204. ReceiptProfile FOOD. |
| 5 | `Pharma` | FDA 21 CFR 211. ReceiptProfile PHARMA. NDC + DEA. |
| 6 | `Cannabis` | METRC + state license. ReceiptProfile CANNABIS. |
| 7 | `Electronics` | MSL + RoHS + IPC. ReceiptProfile ELECTRONICS. |
| 8 | `Aerospace` | AS9100 / AS9120 / CAGE / NSN. ReceiptProfile AEROSPACE. |
| 9 | `Defense` | DCAA / CAS / ITAR. Distinct from Aerospace because cost-accounting + clearance rules diverge. |
| 10 | `MedDevice` | ISO 13485 / FDA 21 CFR 820 / UDI. ReceiptProfile MEDICAL_DEVICE. |
| 11 | `OilGas` | API specs / H2S / mill cert pressure-class. |
| 12 | `Automotive` | IATF 16949 / PPAP. ReceiptProfile AUTOMOTIVE. |
| 13 | `Chemicals` | REACH / SDS / batch processing. ReceiptProfile CHEMICAL. |
| 14 | `Apparel` | Style/SKU/size matrix. ReceiptProfile APPAREL. Low pri but Plex-style enum reserved. |
| 15 | `Construction` | RFI / submittals / AIA G7XX. ReceiptProfile CONSTRUCTION. |
| 16 | `GeneralMfg` | Catch-all for tenants who don't fit. |

Rationale for splits / merges considered:

- **Aerospace + Defense kept SEPARATE.** Considered merging — both use AS9100,
  ITAR, CAGE. Kept separate because DCAA cost-accounting (Defense) is a
  completely different revenue/cost discipline from commercial Aerospace; the
  enum is the natural switch for Sprint 14 RevenueMode defaults.
- **Machining + MetalFab kept SEPARATE.** Considered merging — both metal,
  both shop-floor. Kept separate because the cut-list + nest functionality is
  meaningfully different in UI prominence; Machining is mill/lathe-centric.
- **No "Job Shop" value.** That's a `ProductionType`, not an `IndustryVertical`.
  Job-shop is a *mode of operation* used by Machining, MetalFab, Construction,
  etc.
- **No separate "MedTech" beyond MedDevice.** UDI + ISO 13485 + 21 CFR 820 is
  the consistent compliance triple; one enum value handles the cluster.
- **Reserved 17-31** for future verticals (Mining, Marine, Rail, Energy,
  Semiconductors). 16-bit room.

Sample seed mapping for current tenants:
- Cherry Street (us) → `Machining` (Dean's job-shop) — though we are
  themselves the platform vendor, the tenant row for our internal company
  uses Machining to test all the defaults.
- ABS Machining demo → `Machining`.
- EVS demo → `PrecisionEto`.

---

## 7. Anti-patterns — what NOT to do

1. **Do not put `CageCode` on `CustomerProject`.** It belongs on the parties
   (Customer, Vendor, Manufacturer, our own Company). Putting it on the
   project causes data duplication and breaks the audit chain — the
   regulator-issued ID is a property of the entity, not the engagement.
2. **Do not free-text UoM.** Already an existing problem (`Item.StockUOM` /
   `Item.PurchaseUOM` are strings). New code MUST go through the existing
   `UOMDefinition` master. UoM enforcement-via-FK is a Sprint 14+ retrofit.
3. **Do not duplicate `Currency` across tables.** Today every entity has its
   own `string Currency = "USD"`. Tolerate for now (cost of converting is
   high), but the FX retrieval path should always go through `Currency.Code`
   lookup, not the raw column.
4. **Do not split `Vendor` and `Manufacturer` into more entities.** Both are
   already separate; the Sprint 12+ pattern of one row per regulator-issued
   ID is sufficient.
5. **Do not seed industry-specific data (NDC, allergen, METRC) until a
   paying customer requires it.** PRA-1 plumbs the regulator-IDs; PRA-2/3
   plumb the cross-cutting reference; verticals get their dedicated catalogs
   only when justified.
6. **Do not put `IndustryVertical` on `Site`.** A company can have machining
   and food sites, but the *tenant's posture* is set at Company level. Site-
   level diversification is a v3 problem; force pick at Company for now.
7. **Do not retrofit `Vendor.PaymentTerms` enum to NOT NULL FK.** Add FK
   column NULL-able; service layer reads FK first, falls back to enum. This
   matches the ItemCompanyStocking / Location two-tier pattern Dean has
   approved repeatedly (DEF-008 etc).
8. **Do not bind `Holiday` to `WorkCalendar` ON DELETE RESTRICT.** Use
   CASCADE — holidays are derivative; the calendar is the unit of administration.

---

## 8. Deferred to v2

- **Allergen master** (food). Wait for first food customer.
- **Ingredient master** (specialized Item subtype for food). Wait.
- **SOP master** (food / pharma). Wait — `ProductionJobShopDetail` carries it free-text today.
- **Recipe expansion for pharma compounding.** Existing Recipe + RecipeRevision adequate for v1.
- **NDC catalog** (pharma). Wait for first pharma customer.
- **GTIN catalog**. Use ItemBarcode field for now.
- **METRC integration master** (cannabis). Wait.
- **State cannabis license # on Customer / Vendor.** Wait.
- **NSN catalog** (defense). PartNumber alias good enough for v1.
- **CUI marking** finer than ExportControl. Wait.
- **Drug Master File reference.** Wait.
- **PPAP** workflow (automotive). Sprint 14+ in parallel with FAI.
- **UDI** on Item (med device). V2.
- **API spec catalog** (oil & gas). V2.
- **Per-tenant base currency**. Use existing single `IsBaseCurrency` for v1.
- **Sprint 21 EDI 856 trading-partner master.** Folds in when EDI lands.
- **Site-level `IndustryVertical` override.** v3.

---

## 9. Implementation plan (ordering)

```
Day 1 (today, 2026-05-23):
  PRA-1 in parallel with Sprint 13.5 PR #2 / #3 services.
  Migration committed to .ship/drafts/sprint-13.5-PRA1-master-files.sql.
  EF migration class wraps the SQL.
  Seed: starter Carriers (12 rows), CageCode for Cherry Street demo Company.

Day 2-3:
  PRA-2 Country/Subdivision/WorkCalendar/Holiday.
  Seed ISO-3166 + US/CA/MX subdivisions + Cherry Street / ABS / EVS calendars.

Day 3-4:
  PRA-3 ReasonCode + TaxJurisdiction + Vendor.PaymentTermId.
  Seed 30 starter reason codes.

Day 5+:
  PR #4 Manufacturing UI shell lands ON TOP of PRA-1 schema. The UI can
  immediately bind Customer.DefaultQualityProgram dropdowns + Carrier
  picker + CAGE-code chip on Vendor card.
```

Estimated total: **2.5 dev days** across the 3 PRs. All idempotent. Zero
breakage risk. Each migration is independently re-runnable.

---

## 10. SQL draft

The full SQL for PRA-1 lives at
`.ship/drafts/sprint-13.5-PRA1-master-files.sql`. PRA-2 and PRA-3 SQL drafts
will be authored when their owning PRs open.
