---
title: Master Files Baseline — Cross-Vertical Foundation Audit
date: 2026-05-24
status: Locked for Dean review — pre-PR-#5c.4 gate
sprint: 13.5 (extends)
gates: PR #5c.4 (tenant-aware seeder), PR #5e (event tables) — block on the PRA-4/5/6/7/8 cascade landing first
supersedes: docs/research/master-files-audit.md (which closed out as PRA-1/2/3 — all shipped)
inputs:
  - "Master Loader REV 03-19-24.xlsx — 57 sheets (informational only, ERP21 German-translated nomenclature ignored per Dean lock)"
  - "docs/research/master-files-audit.md (PRA-1/2/3 scope, all shipped)"
  - "docs/research/item-master-and-multi-dim-inventory.md (multi-dim inventory + UOM expansion plan, 2026-05-18)"
  - "Models/*.cs exhaustive read 2026-05-24"
  - "seed/reference-data/*.json (84 LookupValue JSONs — confirmed thin dropdown enrichers, not real masters)"
---

# Master Files Baseline — Cross-Vertical Foundation Audit (2026-05-24)

## 0. Why this memo exists

On the morning of 2026-05-24, after PR #5d (Operator Workbench) shipped at
`fdc4740`, Dean halted the queued PR #5c.4 work with a directive:

> "We have a chart of accounts but it's def not large enough, not sure on
> UOM, but we need to make sure once again that we have the infrastructure
> set up first so we don't chase our tails finding stuff we missed."

He also granted access to the **Master Loader** workbook from his prior
ERP21 (abas) implementation at ABS Machining as **informational floor**
— 57 sheets covering the full master footprint of a working production
ERP. Explicit guidance: *use it for INTENT only; ERP21 is a German-
translated system and the field names are not what we want.*

This memo is the result of the audit Dean asked for. It supersedes the
2026-05-23 `master-files-audit.md` (which closed cleanly as PRA-1/2/3
all shipped) and re-baselines what is needed before the MES event-layer
cascade (PR #5e/5f/5g) layers on top.

---

## 1. Executive summary (Dean read-this section)

CherryAI's master-file backbone is **good at the periphery** (Customer,
Vendor, Site, Location, Manufacturer, Carrier, Country, WorkCalendar, Holiday,
ReasonCode, IndustryVertical — all in PRA-1/2/3) but **thin at the financial
and inventory core**. Specifically, four masters that an ERP cannot operate
without are either undersized, enum-only, or absent:

| # | Master | Today | What's wrong |
|---|---|---|---|
| **A** | **Chart of Accounts (GlAccount)** | One-segment table, ~30 fixed-asset categories, ~58 seeded accounts | Too small; no posting matrix; no segment key; no per-MaterialGroup / per-ProductGroup posting profile; no variance accounts (PPV/MUV/Labor/OH); no intercompany pairs |
| **B** | **Unit of Measure** | **Two parallel enums** (`Models/Item.UnitOfMeasure` 22 vals for inventory · `Models/Telemetry.UnitOfMeasure` 40+ vals for sensors). Only the telemetry side has conversions. No master UOM table at all. | No UOM categories, no base-unit pattern, no per-item UOM list (Stock/Purchase/Sales/Pack/Price), no UCUM, no decimal precision per UOM |
| **C** | **Currency / PaymentTerm / TaxCode** | All three are **enum or LookupValue rows**, not real tables | `Currency` is a 3-char string + ExchangeRate; can't carry ISO 4217 decimals/symbol. `PaymentTerms` is a 7-value enum; can't model "2/10 N30" with discount %. `TaxCode` is referenced as FK but **no entity exists** |
| **D** | **Warehouse / Bin / Lot / Serial / ItemGroup** | None of them exist as first-class masters. Lot/Serial are flat strings on StockReceipt; Warehouse collapses into Location; Bin is a string on Location; ItemGroup → posting profile is absent | Blocks proper inventory accounting, multi-warehouse, lot/serial genealogy (which PR #5f also needs), and the COA posting matrix |

Beyond those four, the audit surfaces six additional gaps the ERP21 sheets
illuminated that the prior audit did not flag:

| # | Master | Today | Why it matters |
|---|---|---|---|
| E | **PriceList / Discount / Rebate** | None | Quote → Sales Order can't honor tier pricing, customer-specific contracts, volume breaks |
| F | **Employee (HR)** | Only `Technician` (maintenance-side) | LaborEntry from PR #5d posts against `OperatorUserId int` (an integer with no FK) — needs real Employee master so payroll, GL labor posting, dept attribution, and credentials all hang off one row |
| G | **WageGroup / LaborRate by skill+shift** | `LaborRate` has Code+Name, `LaborType` has multiplier-by-category (OT/DT/Holiday); no skill × shift × wage-group matrix | Cost-of-Goods labor posting can't compute correctly; shift premium can't apply; multi-currency labor blocked |
| H | **Department → GL posting profile** | `Department` exists with `CostCenterId` FK but no GL posting matrix (no default cost-of-labor account, no fringe account, no overhead-applied account) | Department-by-department P&L impossible; labor variance routing impossible |
| I | **Real Tax authority + rate table** | `TaxJurisdiction` LookupValue (3 rows: US/CA/INTL) — no rates, no agencies, no nexus, no filing frequency, no rate-effective-date | AvaTax-style sales-tax compute impossible; multi-state nexus impossible; intra-EU VAT impossible |
| J | **Pack hierarchy (Each/Inner/Case/Pallet)** | Item carries L/W/H/Weight scalars only | Receiving, shipping, putaway can't compute pallet count / cube / loaded-trailer balance |

### What ships before ABS Thursday May 28

**Recommendation: ONLY the minimum that the demo touches.**
PRA-4 (UOM master) + PRA-5a (COA additive expansion — the **safe subset**
of the COA refactor that does NOT renumber existing accounts) ship before
Thursday. Everything else ships **after the demo** but before EVS June 3
or shortly after.

ABS Thursday demo runs on the **Production Control Center + Operator
Workbench** at industryos.app — both of those surfaces already work with
the current thin masters. Pushing a full COA/Warehouse/Bin/Lot refactor
into a 4-day window before a customer demo is the textbook way to break
a working surface. The cascade lands cleanly across PRA-4 → PRA-5
→ PRA-6 → PRA-7 → PRA-8 in the week after Thursday.

### Sequenced PR plan (full detail in §8)

| PR | Scope | Lines added (est) | Days | When |
|---|---|---:|---:|---|
| **PRA-4** | UOM master (table + category + conversions + per-item UOM list) — replaces both enums | ~900 LOC | 1.5 | **Mon May 25** (pre-ABS) |
| **PRA-5a** | COA additive expansion — add Manufacturing/Inventory/Variance/Intercompany categories. NO renumber. NO breaking. Safe to ship pre-demo. | ~600 LOC | 1.0 | **Tue May 26** (pre-ABS) |
| PR #5c.4 | (queued) Tenant-aware dev seeder + system ReasonCodes + MaterialStructure CompanyId backfill — slot in *after* PRA-5a so the seeder also knows new ItemGroup/posting-profile shape | ~700 LOC | 1.0 | Wed May 27 (pre-ABS) |
| — | **ABS demo gate** | — | — | **Thu May 28** |
| **PRA-5b** | COA segment-key refactor (Company-Site-Account-CostCenter-Dept-Project-Interco) — the breaking part | ~1500 LOC | 2 | Fri May 29 – Sat May 30 |
| **PRA-6** | Currency / PaymentTerm / TaxCode real-table masters (replace enums + LookupValue with first-class tables) | ~1200 LOC | 1.5 | Sun May 31 – Mon Jun 1 |
| **PRA-7** | Warehouse + Bin + Lot + SerialMaster + ItemGroup→PostingProfile | ~1800 LOC | 2 | Mon Jun 1 – Tue Jun 2 |
| — | **EVS pitch gate** | — | — | **Wed Jun 3** |
| **PRA-8** | Employee + WageGroup + LaborRate matrix + Department→GL posting profile | ~1100 LOC | 1.5 | Thu Jun 4 – Fri Jun 5 |
| **PRA-9** | PriceList + Discount + Rebate | ~900 LOC | 1.0 | Sat Jun 6 |
| **PRA-10** | Tax authority + rate table (rates per jurisdiction × effective date × product class) | ~800 LOC | 1.0 | Sun Jun 7 |
| **PRA-11** | Pack hierarchy (Each/Inner/Case/Pallet on Item) | ~400 LOC | 0.5 | Mon Jun 8 |
| **then →** | PR #5e (event tables) → PR #5f (genealogy) → PR #5g (OEE) — the MES cascade resumes with proper foundation underneath | | | Jun 9+ |

**Net runway impact:** the MES event cascade slips by ~10 days. ABS Thursday
+ EVS June 3 demos are untouched.

---

## 2. Methodology

### 2.1 Sources read

- **Master Loader REV 03-19-24.xlsx** (57 sheets, 2.4MB) — parsed with
  openpyxl. Inventoried each sheet for: row count, column count, header
  row, populated row sample. **Treated INTENT-only** per Dean lock —
  ERP21's German-translated field names (`nummer/such/namebspr/name14/
  name/lohn/berschl/beruf/kstelle/azpt/nschicht/lager/lgruppe/dispo/
  vhe/vpe/ehe/epe/le/...`) are not transferred. Only the *concept* the
  sheet represents and the *data shape* (one row per X, with these
  attributes) is mined.
- **Models/*.cs** exhaustive — every file in `Models/`, `Models/Masters/`,
  `Models/Production/`, `Models/Projects/`, `Models/Catalog/`, `Models/
  Telemetry/`, `Models/Quality/`. Confirmed entity presence + field
  shape for each candidate master.
- **seed/reference-data/*.json** — 84 LookupValue JSONs. Confirmed they
  are dropdown-label enrichers (LookupType → LookupValue rows), NOT
  real master tables. (Currency.json = 4 rows of (code, name);
  UnitOfMeasure.json = 22 rows of (label); PaymentTerms.json = 7 rows
  of (label); TaxJurisdiction.json = 3 rows of (label).)
- **docs/research/master-files-audit.md** (2026-05-23) — prior audit
  that closed as PRA-1/2/3, all shipped. This memo builds on it, does
  not repeat it.
- **docs/research/item-master-and-multi-dim-inventory.md** (2026-05-18)
  — item-master expansion plan and multi-dim inventory architecture.
  Several recommendations in this memo align with that plan (UOM
  table, pack hierarchy, OnHand fact table).
- **Memory locks** — BIC entity checklist, no-shortcuts multi-tenant
  lineage, terminology lock, CC quality bar, reuse Cockpit primitives,
  Replit auto-diff destructive-on-populated-tables.

### 2.2 What "informational only" means for the Master Loader

The Master Loader sheets are **shape evidence**, not naming guidance.

- **Take:** the list of masters that a working production ERP cannot
  run without (the sheet inventory itself proves it), and the cardinality
  of each (e.g. Material Group has ~30 GL posting columns per row;
  Product carries 5+ UOMs at once).
- **Leave:** every field name. CherryAI gets purposefully-named English
  fields. Examples of explicit replacements:
  - `nummer` → `Code` (we already use `Code` everywhere)
  - `such` → `Name` (the abas "search-key" overload is dropped)
  - `namebspr / name14 / name` → consolidated to `Name` + `DisplayName`
  - `kstelle` → `CostCenterId` (FK)
  - `azpt` → `StandardHoursPerShift`
  - `nschicht` → `ShiftsPerDay`
  - `lgruppe` → `WarehouseGroupId` (FK)
  - `vhe / vpe / ehe / epe / le` → `SalesUomId / SalesPackUomId /
    PurchaseUomId / PurchasePackUomId / StockUomId` (5 separate FKs)
  - `fvhle / fvple / fehle / fepe` → `SalesPackPerStock /
    PurchasePackPerStock / ...` (computed at runtime from UomConversion)

### 2.3 Audit lens — three questions per candidate master

For each of the 57 ERP21 sheets, I asked:

1. **Does CherryAI have a corresponding entity today?** (Yes / No / Partial)
2. **Is it sized correctly for our multi-vertical / multi-tenant / multi-
   site / multi-org goals?** (Yes / Undersized / Missing structure)
3. **What blocks if we don't fix it?** (ABS-Thu / EVS-Jun3 / Multi-vertical /
   Sprint-15+ / Long-tail)

---

## 3. Sheet-by-sheet gap audit (57 sheets)

Compact form — full per-sheet detail in `appendix-A.md` if Dean wants
the long version. Sheets are grouped by domain.

### 3.1 Organization & people

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| EMPLOYEE (228 rows) | `Technician` (maintenance-side only) | **Missing as Employee master** | EVS-Jun3 (labor posting) |
| WAGE GROUPS (442 rows) | `LaborRate` (Code+Name) + `LaborType` (multiplier-by-category) | **Undersized** — no skill×shift×wage matrix | EVS-Jun3 |
| DEPARTMENT (20 rows, w/ kstelle/azpt/nschicht) | `Department` (20 type enum) | **Undersized** — no GL posting profile, no shift hours, no cost-center FK enforced | EVS-Jun3 |
| PASSWORD DEF | `User` + `LookupValue` UserRole | OK for v1 | — |
| COMPANION (Q.FIELD/Q.GROUP/INDIV/ROLE/USER) | `UserRole` LookupValue + Razor `[Authorize]` | OK for v1 | — |

### 3.2 Items / products / inventory

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| MATERIAL (header) | `MaterialMaster` (Production) | OK | — |
| MATERIAL GROUP (8 rows, ~30 GL postings each) | None | **MISSING** — gates COA matrix | Multi-vertical (PRA-7) |
| PRODUCT (152 rows, 52 cols, 5 UOMs per row) | `Item` (~90 fields, single UOM enum) | **Undersized** — no multi-UOM, no pack hierarchy | Multi-vertical (PRA-4 + PRA-11) |
| PRODUCT GROUP (17 rows, ~22 GL postings each) | `ItemCategory` (tree) | **Undersized** — no GL posting profile | Multi-vertical (PRA-7) |
| PRODUCT CHAR BAR (Item characteristics matrix) | LookupType/LookupValue + Item free-text cols | OK for v1; revisit in Sprint 16 (configurator) | — |
| PRODUCT BOM | `MaterialStructure` + `MaterialStructureLine` + `Bom` | OK | — |
| ALT.PROD.LIST | `ItemAlternate` | OK | — |
| CONFIGURATOR VALUE / OPTION / MASTER | None | Sprint 16 (CPQ engine) | Long-tail |
| LOT (1 row template) | StockReceipt.LotNumber string only | **MISSING as master** | PR #5f genealogy |
| REASON CODES | `ReasonCode` (PR #5d) | OK | — |
| STOCK ADJUSTMENTS | `InventoryTransaction` (need to verify shape) | Partial | EVS-Jun3 |
| SUPP. ITEMS | `ItemApprovedVendor` + `ItemVendor` | OK | — |
| MEANS OF PRODUCTION (template) | `Asset` (100+ MES/IoT/OEE cols) | OK — actually richer than ERP21 | — |

### 3.3 Inventory locations

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| WAREHOUSE GROUP (3 rows) | None | **MISSING** — but only matters when WarehouseHierarchy is needed | PRA-7 (optional) |
| WAREHOUSE (10 rows: INTWH, WH-SUB, WH1490, WH3100, …) | None — `Location` overloads this | **MISSING as distinct master** | EVS-Jun3 (inventory accounting requires per-warehouse asset accounts) |
| LOCATION (10 rows: 1490 SHOP FLOOR, 1495 SHOP FLOOR…) | `Location` (rich, hierarchical, Building/Floor/Bay/Aisle/Rack/Shelf/Bin scalar fields) | **OK at Location level but Bin is a string, not a row** | PRA-7 |
| WH GRP PROP (1 row template) | None | Skip — driven by `WarehouseGroup.DefaultPolicy` if PRA-7 lands | — |

### 3.4 Manufacturing operations

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| WORK CENTER (127 rows) | `WorkCenter` (PR #5c) — tenant-trio NOT NULL | **OK** | — |
| OPERATION (68 rows: WELD_BEVEL / GRINDING / PRESSURE_TEST / LOAD_TEST …) | `RoutingOperation` + `ProductionOperation` (PR #5c) | **OK** | — |

### 3.5 Commercial — customers / vendors

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| CUSTOMER (660 rows) | `Customer` (PRA-1 expanded: CageCode, DunsNumber, CreditLimit, TaxCodeId FK, BillTo block, DefaultQualityProgram/ExportControl/ContractType/RevenueMode) | **OK** | — |
| CUSTOMER CONTACT | inlined on Customer (Contact{Name,Email,Phone}); needs separate table for N contacts when sales-pipeline lands | Partial — fine for v1 | Sprint 14 (Sales CC) |
| VENDOR (21 rows) | `Vendor` (158 lines, PaymentTerms enum, ADR-015 DefaultReceiptAttributes, SendsAsn, AsnFormat) | **OK** except PaymentTerms is enum (see PRA-6) | — |
| VENDOR CONTACT | inlined on Vendor | Partial | Sprint 14 |

### 3.6 Pricing / terms / tax

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| PRICESDISCOUNTS (1 template row) | None | **MISSING — PriceList master** | EVS-Jun3 (quote→SO pricing) |
| TERMS OF PAYMENT (1 row) | `PaymentTerms` enum + `PaymentTerms.json` LookupValue (7 rows) | **MISSING as table** — needs (DiscountPct, DiscountDays, NetDays, MultiCutSchedule, MultiCurrencyVariant) | PRA-6 |
| SALES TAX CODE (15 rows: NOTAX/TAX/AB/BC/MB/NB/NL/NS/NT/NU/ON/PE/QC/SK/YT — CA provinces) | `TaxJurisdiction.json` LookupValue (3 rows: US/CA/INTL) | **MISSING as table** — needs (Rate, Authority, EffectiveDate, ProductClass override, ItemType override) | PRA-6 + PRA-10 |
| PROGRESS BILLING (1 row template) | `CustomerProject.RevenueMode == ProgressBilling` already exists | OK for v1 | — |

### 3.7 Sales transactions (these are TRANSACTIONS not masters; called out for completeness)

| ERP21 sheet | CherryAI equivalent | Status |
|---|---|---|
| SALES BLANKET ORDER | None — Sprint 14 Sales CC | — |
| QUOTE | None — Sprint 14 Sales CC | — |
| SALES ORDER | None — Sprint 14 Sales CC | — |
| SALES PACKING SLIP | `AdvancedShippingNotice` (inbound side exists; outbound Sprint 18 Shipping CC) | Partial |
| SALES INVOICE | None — Sprint 14 Sales CC + Finance | — |

### 3.8 Purchase transactions

| ERP21 sheet | CherryAI equivalent | Status |
|---|---|---|
| PURCHASE ORDER | `PurchaseOrder` | OK |
| PURCH. PACKING SLIP | `AdvancedShippingNotice` | OK |
| PURCHASE INVOICE | `VendorInvoice` | OK |

### 3.9 Finance

| ERP21 sheet | CherryAI equivalent | Status | Blocker |
|---|---|---|---|
| GL ACCOUNT (1 row template) | `GlAccount` (8 types × 27 categories × 22 subcategories) | **Undersized + wrong shape** — see §4 | EVS-Jun3 |
| GL-AR-AP BALANCE | `JournalEntry` + `JournalLine` | OK shape; needs balance-snapshot subledger | Sprint 14 Finance CC |
| AP-AR DETAIL | `VendorInvoice` + (Sprint 14: CustomerInvoice) | Partial | Sprint 14 |
| SERVICE PRODUCT | `Item` with `ItemType.Service` | OK for v1 | — |

### 3.10 Reference utility sheets

| ERP21 sheet | Notes |
|---|---|
| Loader-EDPIMPORT / INDEX / TIPS-FAQ | Documentation sheets — ignored |
| REFERENCE (ACTION codes NEW/UPDATE/STORE/COPY) | Importer-side — ignored |
| IDENTIFIER (Screen 826) / ENUMERATION (Screen 825) | abas's LookupType analog — already covered by our `LookupType`/`LookupValue` |
| PRODUCT - DEAN | Dean's annotated picks — confirms PRODUCT shape is core |

---

## 4. Deep dive — Chart of Accounts (Dean-flagged: too small)

### 4.1 What CherryAI has today

`Models/GlAccount.cs` defines:

```csharp
public class GlAccount {
    int Id;
    string AccountNumber;        // 20 chars
    string Name;                 // 100 chars
    GlAccountType AccountType;   // 8 values (Asset/Liab/Eq/Rev/Exp + 3 contras)
    GlAccountCategory Category;  // 27 values — FIXED-ASSET-FLAVORED
    GlAccountSubCategory Sub;    // 22 values
    NormalBalance NormalBalance; // Debit / Credit
    bool RequiresCostCenter;
    bool RequiresDepartment;
    bool RequiresAssetCategory;
    int? ParentAccountId;        // simple hierarchy
    int? CompanyId;              // tenant-scoped (NULL = system)
}
```

Adjacent entities: `CostCenter` (6-type enum, hierarchical), `Department`
(14-type enum, FK to CostCenter), `FiscalYear` + `FiscalPeriod`,
`JournalEntry` + `JournalLine`, `Book` + `BookGlAccount` (multi-book
for tax/IFRS/GAAP), `ExchangeRate`, `CompanyGlAccountConfig`.

### 4.2 What's wrong (concretely)

**Problem 1 — Categories are fixed-asset-flavored, not manufacturing-
flavored.** The 27 `GlAccountCategory` values include FixedAssetsLand
Buildings / Machinery / Vehicles / Technology / Tooling /
AccumulatedDepreciation / DepreciationExpense / MaintenanceLabor /
RepairParts / CalibrationCertification — appropriate for the original
Fixed Assets product, but missing:

- Raw Material Inventory (separate from MRO)
- Work-in-Process Inventory (currently lumped into one)
- Finished Goods Inventory
- Sub-Assembly Inventory
- Subcontract Inventory (parts at vendor)
- Consigned Inventory (vendor-owned, on our floor)
- Inventory in transit
- **Purchase Price Variance (PPV)** — debit on RM, applied
- **Material Usage Variance (MUV)** — over/under issue
- **Labor Rate Variance / Labor Efficiency Variance**
- **Overhead Applied** (with `Burden` per WorkCenter)
- **Overhead Variance** (Spending / Volume)
- **Scrap expense — by material group**
- **Rework expense — by material group**
- **Yield variance** (process industries)
- **WIP-to-FG transfer** (clearing)
- **COGS by ProductGroup** (a holding company needs the breakdown)
- **Sales Revenue by ProductGroup** (same reason)
- **Intercompany Receivable / Payable (paired by partner Co)**
- **Intercompany Sales / COGS (paired by partner Co)**
- **CTA (Cumulative Translation Adjustment)** — multi-currency
- **Retained Earnings — Current Year Earnings clearing**
- **Statutory accounts** for IFRS roll-up vs US-GAAP (these can be in `Book` but the COA needs a flag)
- **Vertical-specific** — e.g. Cannabis Excise Tax Payable; Pharma R&D
  Capitalization clearing; Aero Long-Term-Contract Cost-Accumulation
  account; Food Recall Reserve.

**Problem 2 — No segment key.** All real ERP COAs use a multi-segment
posting key:

```
{Company} - {Site} - {Account} - {CostCenter} - {Department} - {Project} - {InterCoPartner} - {Vertical}
```

The segment key is the dimensionality of the journal entry. Today,
`JournalLine` carries `AccountId + Amount` only — no CostCenter,
no Department, no Project on the LINE. That means you can't slice
the P&L any way except by Account. You can't ask "what did
Department PROGRAMM contribute this month" or "what's the COGS for
the Weir program."

Recommended segments:

| Segment | Source | NULLable | Notes |
|---|---|---|---|
| Company | Tenant.CompanyId | NO | Already on header |
| Site | Location.CompanyId / Location.Id | YES | NULL for corporate posts |
| **Account** | GlAccount.Id | NO | The natural account |
| CostCenter | CostCenter.Id | YES | NOT NULL if `GlAccount.RequiresCostCenter` |
| Department | Department.Id | YES | NOT NULL if `GlAccount.RequiresDepartment` |
| Project | CustomerProject.Id | YES | NOT NULL for project-tracked accounts |
| InterCoPartner | Company.Id | YES | NOT NULL for intercompany accounts |
| Vertical | Company.IndustryVertical | YES | Mostly auto-populated from Company |

**Problem 3 — No posting matrix.** ERP21's MATERIAL GROUP sheet shows ~30
GL accounts wired per material group. PRODUCT GROUP shows ~22 more.
That's because every material movement (receipt, issue, scrap, rework,
ship, return, adjustment, count-variance, revalue) posts to a
*different account based on the material group + the transaction
type*. Today, CherryAI has zero modeling of this — material movements
post to a single hardcoded `Inventory` and `MaterialExpense` account
(if at all).

Recommendation: introduce `MaterialGroup` + `ProductGroup` masters, each
with a `PostingProfile` row joining (Group × TransactionType) → GlAccount.
This is exactly the pattern SAP MM uses (Movement Type → Valuation Class →
Account).

**Problem 4 — Account hierarchy is single-parent, no roll-up tagging.**
Statutory reporting needs N-to-N tagging (one account rolls into N
statutory lines). Add `GlAccountRollupTag` (Account, ReportingFramework,
ReportingLine, SortOrder).

### 4.3 Recommended target COA design

**Tables:**

1. **`GlAccount`** (extended)
   - Add: `IsControlAccount` (subledger summary), `Currency` (account-
     denominated currency — optional, for memo accounts), `BalanceSheetSide`
     (Operating/Investing/Financing), `IsIntercompany`, `RequiresProject`,
     `RequiresInterCoPartner`, `RequiresVertical`, `StatutoryGroupCode`,
     `IsAutoPostOnly` (true = system-only)
   - Add account NUMBER as a multi-segment string (e.g. `1300-100-RM` =
     {Account-Site-MatGroup}) — but keep `AccountNumber` for the natural
     account; combination of segments is materialized into `AccountingKey`.

2. **`AccountingKey`** (NEW) — the materialized segment-key per journal line:
   ```
   (CompanyId, SiteId, AccountId, CostCenterId, DepartmentId, ProjectId,
    InterCoPartnerCompanyId, IndustryVertical, AccountingKeyHash)
   UNIQUE (CompanyId, AccountingKeyHash)
   ```
   The hash is deterministic across the segment values so JournalLine
   carries one FK instead of N.

3. **`JournalLine`** (extended) — replace `AccountId` with `AccountingKeyId`.
   Migration backfills existing rows with `AccountingKey` resolved from
   `(CompanyId, AccountId, NULL...)`.

4. **`MaterialGroup`** (NEW) — `(CompanyId, Code, Name, ParentMaterialGroupId,
   DefaultCostMethod, IsHazmat, IsRotable, IsConsigned)`. Holds rollup
   of materials for inventory accounting.

5. **`ProductGroup`** (NEW) — `(CompanyId, Code, Name, ParentProductGroupId,
   DefaultRevenueAccountId, DefaultCogsAccountId)`. Holds rollup of items
   for revenue & COGS.

6. **`AccountPostingProfile`** (NEW) — `(CompanyId, ProfileType
   {MaterialGroup|ProductGroup|Customer|Vendor|Department|WorkCenter},
   ProfileId, TransactionType, AccountingKeyId)`. The matrix that says
   "Material Group RM × Receipt → debit 13000-Site-RM"."

7. **`GlAccountRollupTag`** (NEW) — `(AccountId, ReportingFramework
   {IFRS|US-GAAP|Statutory|MgmtReporting}, ReportingLine, SortOrder)`.

**Segment-key example for ABS Machining:**
```
Company:        ABS-MACHINING (id=1)
Site:           1490 SHOP FLOOR (id=17)
Account:        5610 — Material Issued to WIP
CostCenter:     110100 — Production
Department:     2009 — MACHINING (CNC)
Project:        WEIR-OG-2026-014
InterCoPartner: NULL
Vertical:       Machining (enum value 1)
=> AccountingKey hash: sha256(...) → AccountingKeyId 8421
```

A single material-issue posts ONE journal line with
`AccountingKeyId=8421, Amount=$1,250.00`. The P&L roll-up traverses
the segments any way you want — by Department, by Project, by Site, by
Vertical — using SQL `GROUP BY`.

### 4.4 Migration path

**Phase A — Additive (PRA-5a):** Add the new categories to `GlAccountCategory`
enum (manufacturing-flavored: RawMaterialInventory, WipInventory,
FinishedGoodsInventory, SubAssemblyInventory, SubcontractInventory,
ConsignedInventory, InventoryInTransit, PurchasePriceVariance,
MaterialUsageVariance, LaborRateVariance, LaborEfficiencyVariance,
OverheadApplied, OverheadSpendingVariance, OverheadVolumeVariance,
ScrapExpense, ReworkExpense, YieldVariance, WipToFgClearing,
CogsByProductGroup, RevenueByProductGroup, IntercompanyReceivable,
IntercompanyPayable, IntercompanySales, IntercompanyCogs,
CurrencyTranslationAdjustment, CurrentYearEarnings). Seed system
default accounts under each. No breaking changes. Ships pre-ABS.

**Phase B — Segment refactor (PRA-5b):** Add `AccountingKey` table,
add segment columns + indexes, write the AccountingKey resolver
service. Migrate `JournalLine.AccountId` → `AccountingKeyId`. Backfill
existing rows. Ships **after ABS Thursday**.

**Phase C — Posting profiles (PRA-7):** Add `MaterialGroup`,
`ProductGroup`, `AccountPostingProfile`. Wire material movement services
to look up posting accounts via profile. Ships after ABS.

---

## 5. Deep dive — Unit of Measure (Dean-flagged: uncertain)

### 5.1 What CherryAI has today

**Two parallel enums, no master table:**

1. **`Models/Item.cs::UnitOfMeasure`** — 22 hardcoded inventory units
   (Each, Box, Case, Pack, Pair, Set, Kit, Roll, Feet, Meter, Inch,
   Gallon, Liter, Quart, Pint, Ounce, Pound, Kilogram, Gram, Dozen,
   Hundred, Thousand). Used by `Item.UOM`, `Item.StockUOM`,
   `Item.PurchaseUOM` (with a single decimal `PurchaseConversion` —
   no Sales UOM, no Reporting UOM, no Pack UOM).

2. **`Models/Telemetry/UnitOfMeasure.cs`** — 40+ hardcoded engineering
   units (DegreesCelsius, PSI, RPM, Hz, Volts, Watts, etc.) with a
   numbering scheme that reserves ranges by quantity-kind. Used by
   `SensorEvent.Unit` and `AssetSensorLatest.Unit`. PAIRED WITH a
   `UnitConversion` table (FromUnit, ToUnit, Multiplier, Offset,
   UneceCode) for affine conversions.

3. **`seed/reference-data/UnitOfMeasure.json`** — 22 LookupValue rows
   that *just hold the display labels* for the inventory enum. No
   conversions, no categories, no metadata.

### 5.2 What's wrong (concretely)

- **Bifurcation** — sensors live in one universe, inventory in another.
  When a sensor reads `5.6 KG` of material consumed and we want to
  debit `5.6 KG` from inventory, today there is no path between the
  two enums.
- **No categories** — can't say "all length units" or "convert any
  mass to grams." Conversions are only meaningful within a quantity
  kind (mass ↔ mass, length ↔ length, never mass → length).
- **No per-item multi-UOM** — an Item has ONE UOM. The Master Loader
  shows real products carry 5+ UOMs simultaneously (warehouse, sales,
  sales-pack, purchase, purchase-pack, price). CherryAI today: one
  Item.UOM (e.g. `Each`) + a separate `PurchaseUOM` (e.g. `Box`) + one
  decimal conversion factor (e.g. `24` = 24 Each per Box).
- **Conversion factor lives on Item, not on UOM relationship** — if
  vendor A sells in `Box of 24` and vendor B sells the same item in
  `Case of 144` and both convert to `Each`, today we model only ONE
  PurchaseConversion. ItemVendor needs its own conversion.
- **No decimal precision per UOM** — a kg should round to 3 dp,
  an `Each` to 0 dp, but we have a single global decimal-precision.
- **No UCUM codes** — needed for FDA submissions (pharma vertical),
  HL7 integration (medical device), and EU compliance.
- **No price-per UOM** — `Item.ListPrice` is in `Currency` but per what?
  EA? Box? Per pound? Today the price-per-UOM is implicit (assumed =
  StockUOM), which breaks for catch-weight items (e.g. price-per-pound
  of a fish that's sold by the each).

### 5.3 Recommended target UOM design

**Tables:**

1. **`UomCategory`** (NEW) — `(Id, Code, Name, BaseUomId, SortOrder,
   IsSystem)`. Examples:
   - `LENGTH` (base = METER)
   - `MASS` (base = GRAM)
   - `VOLUME` (base = LITER)
   - `TIME` (base = SECOND)
   - `AREA` (base = SQUARE_METER)
   - `COUNT` (base = EACH)
   - `ENERGY` (base = JOULE)
   - `POWER` (base = WATT)
   - `PRESSURE` (base = PASCAL)
   - `TEMPERATURE` (base = KELVIN, but display defaults to CEL/FAH per locale)
   - `FREQUENCY` (base = HERTZ)
   - `CURRENCY` (base = … per Tenant.BaseCurrency)
   - `CONCENTRATION` (base = MOLE_PER_LITER)
   - `LUMINANCE` (base = CANDELA)
   - `RADIATION` (base = BECQUEREL)
   - `INFORMATION` (base = BYTE)
   - `PACKAGE` (no base — each row is its own pack form; conversions per Item)

2. **`UnitOfMeasure`** (NEW master, replaces both enums) —
   `(Id, Code, Name, Symbol, UomCategoryId, ConversionFactorToBase,
   ConversionOffsetToBase, DecimalPrecision, IsoCode, UneceCode,
   UcumCode, IsSystem, IsActive, SortOrder, CompanyId NULL=system)`

   Conversion to base is affine: `Base = Factor * Value + Offset`.
   - `METER → METER` (factor 1, offset 0)
   - `INCH → METER` (factor 0.0254, offset 0)
   - `CELSIUS → KELVIN` (factor 1, offset 273.15)
   - `FAHRENHEIT → KELVIN` (factor 5/9, offset 273.15 - 32*5/9)

   Seed ~200 rows covering ISO 80000 / UCUM / UNECE-Rec-20 + the
   industry-common imperial units.

3. **`UomConversion`** (NEW) — cross-category conversions (the EXCEPTIONS
   to "stay within category"):
   `(Id, FromUomId, ToUomId, Multiplier, Offset, ItemId NULL, CompanyId,
   IsSystem)`. Item-scoped row = per-item override (e.g. for *this* item,
   one Case = 144 Each). System rows handle the conversions that are
   universal regardless of item (1 KG = 1000 G).

4. **`Item`** (extended) — replace `UOM/StockUOM/PurchaseUOM/PurchaseConversion`
   single-fields with FKs to `UnitOfMeasure`:
   - `StockUomId` (NOT NULL)
   - `PurchaseUomId` (NULL = same as Stock)
   - `PurchasePackUomId` (NULL — e.g. Case)
   - `SalesUomId` (NULL = same as Stock)
   - `SalesPackUomId` (NULL — e.g. Pallet)
   - `PriceUomId` (NULL = same as Stock)
   - `ReportingUomId` (NULL = same as Stock)
   - `WeightUomId` (NULL — pairs with `Weight` decimal)
   - `VolumeUomId` (NULL — pairs with `Volume` decimal)
   - `DimensionUomId` (replaces the existing DimensionUOM string)

5. **`ItemPackHierarchy`** (NEW — covers PRA-11 too) — defines pack levels:
   `(Id, ItemId, PackLevelOrdinal {1=Each, 2=Inner, 3=Case, 4=Pallet},
   UomId, QtyOfNextLowerLevel, BarcodeFormat, BarcodeValue)`. Three
   to four rows per stocked item.

6. **`ItemUomConversion`** (NEW — optional per-item override) — when
   a specific item has a non-standard pack conversion that overrides
   the system Uom default for that pair.

### 5.4 Migration path

**Phase A (PRA-4):** Build the new tables, seed system rows from both
existing enums (deduplicated, since `Meter` and `Meters` are the same
thing — bridge the bifurcation). Map every `Item.UOM` enum value to
the corresponding `UnitOfMeasure.Id`. Set `Item.StockUomId` from that
mapping. Set `Item.PurchaseUomId` similarly + persist
`PurchaseConversion` as an `ItemUomConversion` row. Leave the existing
enum fields in place during a deprecation window (mark `[Obsolete]`).

**Phase B (Sprint 14 cleanup):** Drop the obsolete enum columns once
all read-paths migrated to the FK.

The Telemetry-side `UnitOfMeasure` enum can ALSO converge here — the
sensor enum has explicit numbering by category which translates 1:1
into `UomCategoryId`. The `UnitConversion` table maps directly into
the new `UomConversion`. Net: one master, two readers, one source of
truth.

---

## 6. Deep dive — Currency, PaymentTerm, TaxCode (referenced as FK, not defined)

### 6.1 Currency

Today: `Customer.Currency = "USD"` (3-char string). `ExchangeRate` exists.
No `Currency` table. `Currency.json` is 4 LookupValue rows.

Recommended `Currency` master:
- `(Id, IsoCode CHAR(3), Name, Symbol, DecimalPlaces, RoundingRule, IsActive)`
- Seed all ISO 4217 currencies (~180 rows).
- `Company.FunctionalCurrencyId` (NOT NULL — currency company books in)
- `Tenant.ReportingCurrencyId` (currency consolidated reports roll up to)
- `ExchangeRate` (`FromCurrencyId`, `ToCurrencyId`, `Rate`, `EffectiveDate`,
  `Source` {Manual|API|Override}, `IsClosingRate` — for period-end revaluation)
- `JournalLine.OriginalCurrencyId` + `OriginalAmount` (so multi-currency
  retains the source value)
- Multi-currency revaluation event posting (CTA account) — Phase B

### 6.2 PaymentTerm

Today: `PaymentTerms` enum (Net30/Net45/Net60/Net90/DueOnReceipt/Prepaid/COD).
`Customer.PaymentTermId` FK exists but points to nothing (just an int).
`PaymentTerms.json` is 7 LookupValue rows.

Recommended `PaymentTerm` master:
- `(Id, CompanyId NULL=system, Code, Name, DueDays, DiscountPct,
   DiscountDays, MultiCutSchedule JSON, BasisDate {InvoiceDate|MonthEnd|
   ReceiptDate|ShipDate}, MultiCurrencyVariantId NULL, IsActive)`
- Examples to seed:
  - `NET30` — DueDays=30, DiscountPct=0
  - `2/10 N30` — DueDays=30, DiscountPct=2.0, DiscountDays=10
  - `NET-EOM-30` — DueDays=30, BasisDate=MonthEnd
  - `25/25/25/25 N90` — quarterly cut, MultiCutSchedule=[{day:30,pct:25},{60,25},{90,25},{120,25}]
- Migration: map existing enum values to seeded rows. Add `PaymentTermId`
  FK to Customer + Vendor + PurchaseOrder + VendorInvoice + (Sprint 14)
  SalesOrder + CustomerInvoice. Mark old `Vendor.PaymentTerms` enum
  `[Obsolete]`.

### 6.3 TaxCode + TaxAuthority + TaxRate

Today: `Customer.TaxCodeId` FK exists, points to nothing. `TaxJurisdiction.json`
has 3 LookupValue rows (US/CA/INTL).

Recommended chain:
- **`TaxAuthority`** — `(Id, Code, Name, CountryCode, AdministrativeLevel
   {Federal|State|Province|County|City|Other}, FilingFrequency, AgencyUrl)`
- **`TaxCode`** — `(Id, CompanyId NULL=system, Code, Name, TaxAuthorityId,
   IsRecoverable, IsInclusive, IsReverse, GlAccountIdInput,
   GlAccountIdOutput, IsActive)`
- **`TaxRate`** — `(Id, TaxCodeId, Rate, EffectiveFrom, EffectiveTo,
   ProductClass NULL, ItemTypeOverride NULL, MinThresholdAmount NULL)`
- **`TaxJurisdiction`** — `(Id, CountryCode, SubdivisionCode, PostalCode
   Pattern, DefaultTaxCodeId, NexusRequired)` — replaces the LookupValue
   version.
- Seed: US states (50 + DC + 5 territories) with default sales tax codes,
  Canadian provinces (10 + 3 territories) with GST/PST/HST codes, EU
  states with VAT defaults. The ERP21 SALES TAX CODE sheet (NOTAX, TAX,
  AB, BC, MB, NB, NL, NS, NT, NU, ON, PE, QC, SK, YT) maps directly to
  this.

---

## 7. Deep dive — Warehouse, Bin, Lot, Serial, ItemGroup

### 7.1 Warehouse

Today: `Location` is overloaded — sometimes it's a building, sometimes
it's a bin. The ERP21 WAREHOUSE sheet (10 rows: INTWH = internal,
WH-SUB = subcontract, WH1490, WH3100) shows real ERPs treat Warehouse
as a distinct entity from Site/Building.

Recommended:
- **`Warehouse`** — `(Id, CompanyId NOT NULL, LocationId NOT NULL,
   Code, Name, WarehouseType {Standard|Quarantine|InTransit|
   Subcontract|Consignment|Returns|Scrap|Virtual}, IsActive,
   DefaultBinId NULL)`
- **`WarehouseGroup`** (Optional — covers ERP21 WAREHOUSE GROUP sheet) —
   `(Id, CompanyId, Code, Name, DefaultReceivingWarehouseId,
   DefaultShippingWarehouseId)`. Lets multiple warehouses share
   inventory accounting + replenishment policy.
- Migration: split existing `Location` rows where `LocationType = Storage`
  into Warehouse rows.

### 7.2 Bin

Today: `Location.Bin` is a free-text string (max 50 chars).

Recommended:
- **`Bin`** — `(Id, WarehouseId NOT NULL, Code, Name, BinType
  {Floor|Rack|Shelf|Drawer|PalletPosition|Carousel|Vertical
  Lift|Drum|Tank|Reefer}, MaxWeight, MaxVolume, IsBlocked,
  PickSequence, PutawayStrategy, ReplenishmentTrigger,
  EnvironmentalRequirement NULL)`
- Migration: split Location.Bin strings into proper Bin rows.

### 7.3 Lot master

Today: `StockReceipt.LotNumber` is a flat string. PR #5f (Sprint 13.5
late) introduces `LotGenealogy`. We need a **Lot master** under it.

Recommended:
- **`Lot`** — `(Id, CompanyId NOT NULL, ItemId NOT NULL, LotNumber,
   VendorLotNumber NULL, ManufactureDate, ExpirationDate, RetestDate,
   Status {Quarantine|Approved|Hold|Expired|Recalled|Released},
   QuantityReceived, QuantityRemaining, LotAttributes JSON,
   CofA AttachmentId NULL)`. Tenant-trio + composite UNIQUE
   `(CompanyId, ItemId, LotNumber)`.

### 7.4 SerialMaster

Today: `StockReceipt.SerialNumber` is a flat string.

Recommended:
- **`SerialMaster`** — `(Id, CompanyId NOT NULL, ItemId NOT NULL,
   SerialNumber, LotId NULL, Status, CurrentLocationId, CurrentOwner
   CompanyId NULL = our inventory, FirstSeenAt, LastSeenAt, WarrantyEnd
   NULL, ServiceContractEnd NULL, Attributes JSON)`. UNIQUE
   `(CompanyId, ItemId, SerialNumber)`.

### 7.5 ItemGroup / MaterialGroup / ProductGroup

Per §4.3 — drives the COA posting matrix. The ERP21 MATERIAL GROUP sheet's
~30 GL postings per group and PRODUCT GROUP's ~22 per group are the
template here.

---

## 8. PR sequence (the actionable plan)

| PR | Title | Scope | LOC | Days | Window | Ships pre-ABS? |
|---|---|---|---:|---:|---|:---:|
| **PRA-4** | UOM master + per-item UOM list | UomCategory, UnitOfMeasure, UomConversion, ItemPackHierarchy. Map existing enums. Item FK columns added; old enum cols kept [Obsolete]. | ~900 | 1.5 | Mon May 25 – Tue May 26 AM | ✅ |
| **PRA-5a** | COA additive expansion | Add ~26 manufacturing/inventory/variance categories to `GlAccountCategory`. Seed system default accounts. **NO** renumber. | ~600 | 1.0 | Tue May 26 PM | ✅ |
| **PR #5c.4** | Tenant-aware seeder (queued) | Slotted AFTER PRA-5a so seeder knows new UOM + COA shape | ~700 | 1.0 | Wed May 27 | ✅ |
| — | **ABS Thursday demo** | — | — | — | **Thu May 28** | — |
| **PRA-5b** | COA segment-key refactor | AccountingKey table, segment columns on JournalLine, AccountingKeyResolver service, JournalLine backfill | ~1500 | 2.0 | Fri May 29 – Sat May 30 | post-ABS |
| **PRA-6** | Currency + PaymentTerm + TaxCode real tables | 3 master tables, seed ISO 4217, seed term variants, migrate enums + FKs | ~1200 | 1.5 | Sun May 31 – Mon Jun 1 AM | post-ABS |
| **PRA-7** | Warehouse + Bin + Lot + SerialMaster + ItemGroup→PostingProfile | 5 new masters, posting profile matrix table, migration from Location.Bin string + StockReceipt.LotNumber/SerialNumber | ~1800 | 2.0 | Mon Jun 1 PM – Tue Jun 2 | post-ABS |
| — | **EVS Jun 3 pitch** | — | — | — | **Wed Jun 3** | — |
| **PRA-8** | Employee + WageGroup + LaborRate matrix + Department→GL profile | Employee master (real HR table separate from Technician), WageGroup with skill×shift, Department.PostingProfile | ~1100 | 1.5 | Thu Jun 4 – Fri Jun 5 AM | post-EVS |
| **PRA-9** | PriceList + Discount + Rebate | Customer-specific pricing, volume breaks, contract pricing, rebate accruals | ~900 | 1.0 | Fri Jun 5 PM – Sat Jun 6 | post-EVS |
| **PRA-10** | TaxAuthority + TaxRate (effective-dated) | Layered on PRA-6 (which lands the TaxCode entity); PRA-10 adds the rate engine | ~800 | 1.0 | Sun Jun 7 | post-EVS |
| **PRA-11** | Pack hierarchy (Each/Inner/Case/Pallet) | Built on PRA-4 (UOM exists); adds the pack-level table | ~400 | 0.5 | Mon Jun 8 AM | post-EVS |
| — | **MES event cascade resumes with proper foundation** | PR #5e (DowntimeEvent / ScrapEvent / ReworkEvent / MaterialConsumption) → PR #5f (LotGenealogy / SerialGenealogy — built on PRA-7) → PR #5g (OeeEvent + OEE / Throughput / Down-Machines KPI tiles) | — | — | Jun 8+ | — |

### 8.1 Per-PR BIC checklist exercise

Every one of PRA-4 through PRA-11 will be drafted against the
[BIC Entity Checklist](memory:reference-bic-entity-checklist) before
the EF migration is written:

1. Tenant trio columns NOT NULL where applicable
2. UNIQUE indexes tenant-prefixed (`(CompanyId, Code)` for company-scoped
   masters; `(CompanyId, LocationId, Code)` for site-scoped)
3. Service constructor takes `ITenantContext` + mutation pre-check
4. Cross-domain emit `RecordEdgeAsync` for chain-of-custody where the
   master links into a transaction graph (Lot → Receipt, Serial → Order,
   etc.)
5. NO tenant data in migrations — all PR seeds go in
   `seed/reference-data/*.json` (system) or `seed/dev-demo/*.sql`
   (tenant-shaped)
6. Snapshot lineage at instantiation for execution-time entities (Lot
   snapshots StorageWarehouseId at first move; AccountingKey snapshots
   `IndustryVertical` at create)

### 8.2 Per-PR Replit psql pre-apply playbook

Every PR in this cascade touches populated tables (we have 279 WorkOrders,
1000+ Items, 660 Customers — none are empty). Each PR follows the
[psql pre-apply pattern](memory:feedback-replit-autodiff-destructive-on-populated-tables):

1. Verify prod row counts.
2. Draft transaction-wrapped SQL with idempotent guards under `.ship/drafts/`.
3. Apply via `psql "$PROD_DB" -v ON_ERROR_STOP=1 -1 -f /tmp/pr-<N>-prod.sql`.
4. Insert `__EFMigrationsHistory` row at end.
5. Verify schema landed cleanly.
6. THEN click Republish.

---

## 9. Open questions for Dean (need 1-click direction)

1. **COA segment count** — recommendation is 8 segments (Company / Site /
   Account / CostCenter / Department / Project / InterCoPartner / Vertical).
   Do you want to add more (e.g. Customer? Product? Equipment?) or fewer?
2. **Currency seed** — recommend full ISO 4217 (~180 rows). Alternative
   is "USD/CAD/EUR/GBP only" (already today) until a multi-currency
   customer signs. Recommend full seed because the table is small and
   future-proofs.
3. **UOM seed** — recommend ~200 system rows covering ISO 80000 + UNECE
   Rec 20 + UCUM. Alternative is "just what we use today" (~30 rows).
   Recommend full seed because the cross-vertical pitch (Pharma needs
   UCUM, EU customers need ISO codes, US retail needs UNECE pack codes)
   wants this.
4. **Pack hierarchy depth** — recommend 4 levels (Each / Inner / Case /
   Pallet). Alternative is 3 (Each / Pack / Pallet). Recommend 4.
5. **PRA-5b segment-refactor blast radius** — segment-keyed
   `JournalLine.AccountingKeyId` is a breaking change to every Finance
   service. Acceptable to do this in a long-lived branch (Fri-Sun) and
   land Monday morning, or split into smaller increments?
6. **Employee master split from Technician** — recommendation is a
   real `Employee` master with Technician as a 1:0..1 satellite (same
   pattern as ProductionJobShopDetail). Alternative is to extend
   Technician to be the Employee. Recommend the split because
   Engineering/Production/Sales/Admin employees are not Technicians
   but still need to clock-in on LaborEntry.
7. **PRA-8 timing vs EVS June 3** — PRA-8 (Employee + WageGroup + Dept→GL)
   lands Thu-Fri Jun 4-5, AFTER EVS. Acceptable? EVS demo won't show
   payroll/labor-posting depth, so probably yes.
8. **PR #5c.4 slot in cascade** — recommend slotting #5c.4 AFTER PRA-5a
   so the seeder knows the new COA shape (otherwise we'd reseed in
   PRA-5a). Acceptable?

---

## 10. Risk register

| Risk | Severity | Mitigation |
|---|---|---|
| PRA-5b (segment-refactor) breaks an in-flight Finance feature | High | Land in long-lived branch; full regression run before merge; psql pre-apply on prod with rollback ready |
| UOM enum-to-FK migration leaves a stale Item.UOM enum value somewhere we missed | Medium | Mark old enum cols `[Obsolete]` not delete; deprecation window of one sprint; CHERRY025-style analyzer to catch new code reading the obsolete enum |
| Replit auto-diff destroys an existing populated table during one of the 8 cascade PRs | Medium | Pre-apply each migration via psql per [Replit lock](memory:feedback-replit-autodiff-destructive-on-populated-tables); never click "Approve and publish" on auto-generated TRUNCATE SQL |
| ABS Thursday demo breaks because PRA-4 introduces a UOM-related bug | High | PRA-4 + PRA-5a + #5c.4 ship Mon-Wed; full E2E smoke on industryos.app Wed PM; Thu morning dress rehearsal |
| Master Loader file names get accidentally adopted (German nomenclature creeps in) | Low | PR descriptions cite this memo's `§2.2 take/leave` table; code review gate on field names |
| Segment-refactor needs > 2 days | Medium | Time-box; if it spills, drop intercompany-pair segment to a Phase C |
| EVS pitch demands a feature in the cascade (e.g. multi-currency on quote) that hasn't landed yet | Medium | EVS demo brief: agree with Dean on what must be live by Jun 3 |
| Cherry025 analyzer false-positives on the new posting-profile service | Low | Either fix the analyzer or apply `[ControlPlaneExempt]` with documented reason per ADR-025 |

---

## 11. What this memo does NOT change

- **MES event-layer cascade plan** is unchanged in shape — PR #5e, #5f, #5g
  are still next after the master files baseline lands. They slip ~10 days.
- **PR #4 / #5a / #5b.1 / #5c / #5c.1 / #5c.1.1 / #5c.2 / #5c.3 / #5d**
  are not rebuilt. All of those ship-and-locked work continues to function.
- **PRA-1 / PRA-2 / PRA-3** stay as the foundation (Carriers, regulator
  IDs, Country/Subdivision/WorkCalendar/Holiday, ReasonCode). PRA-4
  onwards layers on top.
- **Locked UX baseline** (`docs/research/luxury-cockpit-ux.md`) is unchanged.
- **The 5 hard memory locks** (terminology, CC quality bar, reuse Cockpit
  primitives, no shortcuts, Replit auto-diff) all apply unchanged to the
  PRA-4 through PRA-11 cascade.

---

## 12. References

- `docs/research/master-files-audit.md` — prior audit (PRA-1/2/3, all shipped)
- `docs/research/item-master-and-multi-dim-inventory.md` — item-master and
  multi-dim inventory plan (Sprints 7-9 originally; pulled forward)
- `docs/research/luxury-cockpit-ux.md` — locked UX baseline
- `docs/research/customerproject-field-set.md` — PR #1.5 expansion
- `docs/research/fai-workflow-schema.md` — PR #1.75 AS9102 FAI
- `Models/GlAccount.cs` — current COA
- `Models/Item.cs` — current Item + UnitOfMeasure enum
- `Models/Telemetry/UnitOfMeasure.cs` — current sensor UOM enum + UnitConversion
- `Models/Telemetry/UnitConversion.cs` — affine conversion pattern (template
  for the new UomConversion)
- `seed/reference-data/Currency.json` / `UnitOfMeasure.json` /
  `PaymentTerms.json` / `TaxJurisdiction.json` — thin LookupValue files
  that PRA-4/5/6/7 supersede
- `Memory: reference_bic_entity_checklist` — the 6-point gate
- `Memory: feedback_no_shortcuts_multi_tenant_lineage` — the lineage
  discipline lock
- `Memory: feedback_replit_autodiff_destructive_on_populated_tables` —
  the psql pre-apply playbook
- ADR-022 — chain-of-custody graph (Lot/Serial genealogy hangs on this)
- ADR-025 — service-layer + CHERRY025 analyzer
- ADR-026 — Seven Customer Modes contract (the multi-vertical contract
  this baseline serves)

---

*End of memo. Awaiting Dean's 1-click direction on §9 open questions
and the pre-ABS scope cut (PRA-4 + PRA-5a + #5c.4).*
