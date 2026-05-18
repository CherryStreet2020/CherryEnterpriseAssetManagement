# Industry-Agnostic StockReceipt Schema — Deep Research

**Status:** Research / pre-ADR
**Authors:** Claude (research) for Dean Dunagan
**Date:** 2026-05-18
**Predecessors:** PR #119.13b (StockReceipt table), PR #219 (admin CRUD), ADR-013 (Polymorphic Production Backbone), ADR-014 (Voice-Ready Foundation)
**Successors (proposed):** ADR-015 (Industry-Agnostic Receipt Schema), Sprint 7 (Item Master Expansion), Sprint 8 (Multi-Dim Inventory), Sprint 5 (Voice-AI Co-Pilot)

---

## Executive summary

The current `StockReceipt` table is *correctly modeled for a structural-steel / aerospace machining shop and nothing else*. Its load-bearing columns (`HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm/WidthMm/ThicknessMm`, `UsableLengthMm/UsableWidthMm`) bake the ASME / AWS / AS9100 worldview into the schema. They are simultaneously **too specific** (none of them apply to pharma, food, electronics, cannabis, etc.) and **too narrow** (they cannot represent the equally-mandatory trace fields that other regimes demand: DSCSA serial numbers, FSMA traceability lot codes, EUDAMED UDI-PI / UDI-DI pairs, METRC tags, IPC J-STD-033 MSL clocks, IATF 16949 PPAP level, REACH CAS numbers).

The good news is that the table is **brand new** (PR #119.13b shipped the table, PR #219 just shipped admin CRUD on 2026-05-18) and **not yet populated at scale** — migration is still cheap. The platform already has the right hook in `RegulatoryProfile.Gates` (`jsonb`), and the strategic direction of the product (voice-AI co-pilot in Sprint 5, multi-industry positioning) makes industry-agnosticism a hard requirement, not a nice-to-have.

This document recommends a **profile-driven typed-core + JSONB hybrid** schema:

1. A small, stable, **strictly typed core** on `StockReceipts` that captures the fields every receipt has regardless of industry (FKs, identifiers, quantities, status, audit/timestamps).
2. A **`ReceiptProfile`** FK that points to an industry-specific shape definition stored in a config table (similar to existing `RegulatoryProfile`).
3. A `jsonb attributes` column on `StockReceipts` that holds the industry-specific payload, schema-validated at the service layer against the profile's JSON Schema.
4. **Promoted facet columns** (selective, generated-via-expression-index columns) for the hottest 5–10 query fields per profile, so voice-AI lookups like "find heat H-12345" or "lots of lidocaine expiring in 30 days" hit a B-tree, not a JSONB scan.

This is the **shape SAP MM Batch Management, Oracle Cloud, NetSuite, and D365 F&O all converged on** — the lessons of the last 30 years of ERP say: don't subclass the lot table, don't EAV the lot table, and don't TPH it. Hybrid with a profile-driven schema definition wins on every axis: write performance, query performance, evolution cost, admin UI dynamism, and (critically for us) voice-AI introspectability.

---

## Section 1: Industry survey

Each industry profile below was assembled from publicly-documented regulatory texts and the operational realities of the dominant ERPs serving that vertical. "Killer fields" are the ones an auditor or recall officer would demand within 24 hours.

### 1.1 Pharmaceutical (finished dose + API)

**Regimes:** FDA 21 CFR Part 211 (cGMP finished), 21 CFR Part 210 (cGMP manufacturing), 21 CFR Part 11 (electronic records), DSCSA (Drug Supply Chain Security Act), EU GMP Annex 11, EU GMP Annex 16, USP <1079> (good storage and shipping practices), ICH Q7 (API GMP).

**Required receipt fields (per DSCSA enforcement post 2024-11-27 and 2025-08-27 partner-data deadlines):**
- GTIN (14-digit Global Trade Item Number, AI 01)
- Serial number (AI 21, alphanumeric up to 20 chars, **unique per saleable unit** at package and homogeneous-case levels)
- Lot/batch number (AI 10)
- Expiration date (AI 17, YYMMDD)
- NDC (National Drug Code) — derivable from GTIN but always recorded
- T3 data: Transaction Information (TI), Transaction History (TH), Transaction Statement (TS) — must be exchanged in EPCIS (Electronic Product Code Information Services) electronic format (paper, PDF, spreadsheets are no longer DSCSA-compliant)

**Common but optional:**
- Manufacture date
- DEA Schedule (I / II / III / IV / V) for controlled substances
- Country of origin
- Temperature excursion log reference (cold chain — see USP <1079>)
- Chain-of-custody / pedigree document URL
- Quarantine release approver + signature meta (21 CFR Part 11)

**Killer fields:**
- Lot number
- Expiration date
- Serial number (DSCSA mandatory at saleable-unit level for prescription drugs)
- GTIN

**Voice-AI examples:**
- "Find all lots of lidocaine expiring within 30 days"
- "What's the EPCIS pedigree for serial 9847-A?"
- "Show me all Schedule II receipts last quarter without DEA222 confirmation"

### 1.2 Food / beverage

**Regimes:** FSMA Section 204 Food Traceability Rule (compliance date extended to 2028-07-20 by Congressional action in Nov 2025), HACCP / FSMA Preventive Controls, USDA Organic (NOP), GFSI schemes (SQF, BRCGS, FSSC 22000), ISO 22000.

**Required receipt fields** (per the FSMA 204 Key Data Elements for the Receiving CTE on the Food Traceability List):
- Traceability Lot Code (TLC) — the singular trace identifier required by Section 204
- Product description
- Quantity + unit of measure
- Location description (your location)
- Date of receipt
- Reference document type (e.g. invoice / BOL) and number
- Traceability Lot Code Source (where the lot was assigned)
- Traceability Lot Code Source reference (URL / address)

**Common but optional:**
- Best-by / use-by / sell-by date
- Pack / harvest / catch date
- Country of origin
- Organic certification number (NOP-accredited certifier)
- Allergen profile (Big 9: milk, eggs, fish, shellfish, tree nuts, peanuts, wheat, soy, sesame)
- COA (Certificate of Analysis) URL
- Cold-chain sensor data reference
- GFSI scheme + cert number (SQF, BRCGS, FSSC 22000)
- Slaughter / process date (for meat)
- Fishing area / catch method (for seafood, per EU IUU rules)

**Killer fields:**
- Traceability Lot Code (TLC)
- Date of receipt
- Source (one-up traceability)

**Voice-AI examples:**
- "Quarantine all spinach received from supplier SmartGreens on lot SG-2026-44"
- "Show me every TLC currently in the cooler past best-by"
- "Trace TLC ABC-1234 forward — what finished goods used it?"

### 1.3 Chemicals

**Regimes:** REACH (EU 1907/2006), CLP (EU 1272/2008), OSHA HCS / GHS (US), TSCA (US), DOT 49 CFR (hazmat transport), IATA DGR (air), IMDG (sea), Seveso III directive (EU major-accident hazards).

**Required receipt fields:**
- CAS Registry Number (canonical chemical identifier — `1234-56-7` format)
- UN/NA number (4-digit hazmat transport ID)
- Hazard class / division (DOT 49 CFR)
- Packing group (I / II / III)
- Grade / purity (e.g. ACS, USP, technical, electronic-grade — varies by substance)
- SDS revision number + URL (REACH Annex II / OSHA 1910.1200(g) format)
- Lot / batch number
- Manufacture date
- Quantity + container size + unit of measure

**Common but optional:**
- Cure-by / shelf-life date (especially for reactives, peroxides, monomers)
- Density / specific gravity (for volume↔mass conversion at receipt)
- Storage temperature class
- Compatibility group / segregation code (for warehouse zoning)
- Concentration (for solutions)
- Inhibitor type + concentration (for monomers)
- Container type (drum, IBC, tote, cylinder)

**Killer fields:**
- CAS number
- UN number
- Lot number + manufacture date
- SDS revision

**Voice-AI examples:**
- "Find all containers of CAS 67-64-1 with cure-by inside 30 days"
- "What SDS revision is on the drum we received yesterday from BulkChem?"
- "Show me every hazmat class 3 receipt staged in Zone B"

### 1.4 Electronics / semiconductors

**Regimes:** IPC J-STD-033 (moisture sensitivity handling), IPC J-STD-020 (MSL classification), JEDEC J-STD-609 (lead-free marking), RoHS (EU 2011/65), REACH SVHC, WEEE (EU 2012/19), Conflict Minerals (Dodd-Frank Section 1502 / RMI CMRT), IPC-1782 (traceability data).

**Required receipt fields:**
- Manufacturer Part Number (MPN)
- Manufacturer lot code
- Date code (e.g. YYWW)
- MSL level (1, 2, 2a, 3, 4, 5, 5a, 6 per J-STD-020)
- MBB (moisture barrier bag) seal date
- Time-out-of-bag clock start (the J-STD-033 floor-life timer)
- RoHS compliance flag + version
- REACH SVHC declaration

**Common but optional:**
- ESD class (HBM / CDM / MM levels per ANSI/ESDA/JEDEC JS-001)
- Conflict-minerals CMRT version
- DPPM (defects per million) from supplier
- Country of origin
- Reel ID (for SMT reels)
- Pick-and-place pin-1 orientation
- Serial range (start/end) for serialized parts
- Tape-and-reel quantity per reel

**Killer fields:**
- MPN + manufacturer lot code
- Date code
- MSL level + bag-open time (drives bake decisions before reflow)

**Voice-AI examples:**
- "Show all electronics receipts with MSL 3 that opened more than 168 hours ago"
- "Find every reel of MPN AT24C32-XXX with date code 24WW33"
- "What's the RoHS rev on the BoM for product P-117?"

### 1.5 Medical devices

**Regimes:** FDA 21 CFR 820 (QSR), 21 CFR 830 (UDI), EU MDR 2017/745 (replaces MDD 93/42), EUDAMED (EU device DB — ~111 data attributes per UDI), IVDR 2017/746, ISO 13485 (international QMS), ISO 14971 (risk management).

**Required receipt fields:**
- Basic UDI-DI (device-family identifier — connects EUDAMED actor entries)
- UDI-DI (specific device version)
- Lot / batch number OR Serial number (UDI-PI variant chosen per device)
- Manufacture date and/or Expiration date (UDI-PI variants)
- GTIN (if GS1 issuing agency) — most common in US/EU
- Sterilization batch (if sterile-supplied)
- ISO 13485 cert evidence reference

**Common but optional:**
- Software version (UDI-PI for SaMD / firmware-coupled devices)
- Class (I / IIa / IIb / III for EU MDR)
- Storage temp range
- Single-use vs reusable flag
- Implant flag + implant-card data (per EU MDR Article 18)

**Killer fields:**
- UDI-DI + UDI-PI (lot / serial / date / software version)
- Expiration date
- Sterilization batch (for sterile)

**Voice-AI examples:**
- "What's the chain of custody for serial 9847-A?"
- "Find all implantable receipts whose Basic UDI-DI isn't in EUDAMED"
- "Show stock with sterilization batch ST-2026-Q2-014"

### 1.6 Aerospace (current shop today)

**Regimes:** AS9100 / AS9120 (aerospace QMS / distributors), NADCAP AC7102 (heat treat), NADCAP AC7108 (chem processing), AC7114 (NDT), FAA 14 CFR Part 21, EASA Part 21, ITAR / EAR (for defense / dual-use).

**Required receipt fields:**
- Heat number
- Mill name
- Mill test report (MTR / CMTR) URL
- ASTM / AMS / MIL spec designation
- Source PO + PO line
- Lot number (when distinct from heat)
- AS9100 receiving inspection result + inspector
- Country of melt (DFARS 252.225-7008/7009 for defense)
- Country of origin

**Common but optional:**
- Pyrometry chart reference (NADCAP heat-treat)
- Charpy / yield / tensile lot results
- Form (plate / bar / tube / forging)
- Dimensions (length, width, thickness — current schema)
- DFARS / DPAS rating
- Special-process certification (heat-treat, NDT, surface treat)

**Killer fields:**
- Heat number
- Mill cert URL
- ASTM/AMS designation
- Country of melt (DFARS for US defense)

**Voice-AI examples:**
- "Find all receipts of heat H-12345"
- "Show every MTR missing for parts cut after 2026-05-01"
- "List receipts where country of melt is not US/UK/CA"

### 1.7 Cannabis

**Regimes:** State-level seed-to-sale (METRC in 20+ states, BioTrack, Leaf Data Systems, Florida/Maryland/MI custom), state DOH lab COA, state labeling rules.

**Required receipt fields:**
- METRC package tag (24-digit UID printed on RFID-coded label issued by state)
- Source harvest batch tag
- Strain / cultivar name
- Source license number (state-issued)
- Lab COA URL + COA pass/fail
- Cultivation batch / production batch link
- Quantity + UoM (typically grams)

**Common but optional:**
- Cannabinoid profile (THC %, CBD %, total cannabinoids)
- Terpene profile
- Microbial / pesticide / heavy metal test results
- Harvest date
- Cure date
- Trim type (whole flower, shake, trim)
- Indoor / outdoor / mixed-light cultivation type

**Killer fields:**
- METRC tag (the audit join key)
- Source license # (one-up trace)
- COA pass/fail (gate before sale)

**Voice-AI examples:**
- "Show every package tag from harvest HB-2026-014"
- "Find receipts whose COA failed pesticide screen"
- "What's the THC% on package 1A4FF030..."

### 1.8 Automotive parts

**Regimes:** IATF 16949 (auto QMS), AIAG PPAP (4th edition, 18 elements, 5 submission levels), AIAG APQP, AIAG MMOG/LE (materials management), VDA 6.3 / 6.5 (German equivalents), Customer-Specific Requirements (CSRs) from GM / Ford / Stellantis / Toyota / etc.

**Required receipt fields:**
- Supplier code (D-U-N-S or customer-assigned)
- PPAP level (1–5) + PSW (Part Submission Warrant) status
- Part number / change level
- Date code
- Lot / batch number
- Production date
- Container / load lot
- ASN (Advance Shipping Notice) reference

**Common but optional:**
- Critical / safety / regulatory characteristic flag (the "shield" / "diamond" / "Pass-Through Characteristics")
- IMDS (International Material Data System) declaration ID
- Conflict-minerals CMRT
- Country of origin
- Deviation / waiver reference (if part is non-conforming with customer authorization)
- Capacity Verification (Run @ Rate) reference

**Killer fields:**
- Supplier code + PPAP level
- Lot number + date code
- Change level (engineering revision)

**Voice-AI examples:**
- "Find all receipts of part 12345 from supplier S-9988 with PPAP level 3"
- "Show me deviations open against any active receipt"
- "List receipts whose change level lags the current design"

### 1.9 Apparel / textiles

**Regimes:** USDA AMS (cotton / wool), GOTS (organic textiles), OEKO-TEX, TSCA (chem-finishes), Berry Amendment (US defense apparel), USMCA rules of origin, HTS / Schedule B tariff classification.

**Required receipt fields:**
- Roll / dye lot number
- Size / color / SKU matrix line
- Country of origin
- HTS / HS tariff classification
- Fiber composition (% breakdown)
- Width
- Length (per roll)

**Common but optional:**
- GSM (grams per square meter)
- GOTS / OEKO-TEX cert + revision
- Care code
- Test reports (color fastness, shrinkage, pilling)
- Mill name
- Country of yarn-spinning (separate from country of origin under some rules)

**Killer fields:**
- Roll / dye lot
- Country of origin
- Fiber composition

**Voice-AI examples:**
- "Show all dye-lot 5567 receipts across colors"
- "Find every roll under 40 gsm received this month"
- "List rolls with GOTS cert expiring in 60 days"

### 1.10 Construction / MRO

**Regimes:** ASTM (materials), AISC (steel), ACI (concrete), API (oil/gas piping), ASME B31 (process piping), NFPA (fire-rated), local building code.

**Required receipt fields:**
- Batch / plant code (concrete, asphalt, paint, sealants)
- Manufacture / cure date
- Spec compliance (ASTM / ACI / etc.)
- Quantity + UoM

**Common but optional:**
- Slump / strength results (concrete)
- Mix design ID
- VOC content (paints, coatings)
- Cure-by / use-by date
- Color batch (paints, coatings)
- Pressure rating (for pipe / fittings)

**Killer fields:**
- Batch number + manufacture / cure date
- Spec compliance reference

### 1.11 Biotech / cell therapy

**Regimes:** FDA 21 CFR 1271 (HCT/P), FDA 21 CFR 600s (biologics), USP <1046>, USP <1047>, EU GMP Annex 1 (sterile), EU GMP Annex 2 (biological), FACT-JACIE.

**Required receipt fields:**
- Donor ID (DIN — Donation Identification Number)
- Cell line ID
- Passage number
- Cryogenic timestamp (when frozen)
- Container / vial ID
- Quantity (cells/mL × mL)
- Viability %

**Common but optional:**
- Source tissue type
- GLP / GMP designation
- Karyotype (for stem cells)
- Mycoplasma test result
- Sterility test result
- Recipient match info (for autologous)

**Killer fields:**
- Donor ID + passage number
- Cryogenic timestamp (chain-of-cold)
- Viability %

### 1.12 Agriculture / seed

**Regimes:** AOSA / ISTA (seed testing), USDA AMS (organic, non-GMO Project), state seed laws, OECD seed schemes.

**Required receipt fields:**
- Variety / cultivar
- Crop year
- Lot number
- Germination %
- Purity %
- Quantity + UoM

**Common but optional:**
- Treatment (e.g. fungicide coating + active ingredient)
- Non-GMO / organic cert
- AOSA tag / state-issued tag #
- Origin (state / country)
- Tetrazolium / cold-test results

### 1.13 Oil & gas / energy

**Regimes:** API 5L / 5CT / 6A (pipe, casing, wellhead), API Q1 / Q2 (QMS), NACE MR0175 (sour service), ASME B31.4 / B31.8 (pipeline), DOT PHMSA 49 CFR 192/195.

**Required receipt fields:**
- Heat number
- Mill name + MTR URL
- API spec + grade (e.g. API 5L X65, API 5CT L80)
- Serial / joint number (per-joint for casing/tubing)
- Heat treatment condition
- Pressure rating
- Schedule / wall thickness
- Length (per joint)

**Common but optional:**
- Charpy results
- H2S / sour-service compliance (NACE MR0175)
- Hydrotest result + pressure
- Coating type + lot
- API monogram cert

**Killer fields:**
- Heat number + serial / joint number
- API spec + grade
- Pressure rating

### 1.14 Bonus: Discrete-assembly OEM (current heavy-equipment vibe)

This is the **default** profile and overlaps with aerospace/automotive but with looser regulatory teeth. Lot + supplier + PO + receive-date is the floor; everything else is optional.

---

## Section 2: ERP / WMS precedents

Lessons from the systems that have already lived this problem at scale.

### 2.1 SAP MM Batch Management (Classification, Characteristics, Batch Determination)

SAP solves industry variance with a **classification + characteristics** system layered on the universal `MCH1` / `MCHA` batch tables. Every batch row is the same shape; industry-specific attributes live in **characteristics** (think: typed key/value pairs) attached via **class type 023** (`Batch Class`).

- A **class** groups characteristics that describe an object (a material, a batch). One class might be `Z_PHARMA_BATCH` with characteristics `LOT_EXPIRY`, `DEA_SCHEDULE`, `MFG_DATE`; another might be `Z_STEEL_HEAT` with `HEAT_NUMBER`, `MILL`, `ASTM_GRADE`.
- **Characteristics** carry their own datatype (numeric, char, date), value list (fixed values, ranges, or table-pulled), and validation.
- **Batch determination** lets users say "find me a batch where SHELF_LIFE > 30 days AND MFG_DATE > X" without writing SQL — a strategy + sort + selection-class config drives lookups.
- **Standard characteristics** ship from SAP for tables MCHA/MCH1 (mfg date, expiry date, vendor batch) and can be reference-typed when defining custom characteristics so they auto-populate from the batch master.

**Architecture pattern:** Single fat batch table + **separate characteristics tables** that EAV-style hold the variable attributes, but read via the classification system that exposes them as if they were native columns. It is fundamentally EAV with very heavy tooling.

**Pros:** Universal — *any* industry can be modeled. Excellent search via batch determination. UI auto-renders against the class. Schema-stable, configuration-only changes for new verticals.

**Cons:** Performance of characteristic-based lookups is notoriously slower than direct columns (SAP shops put a lot of work into "characteristic-to-field" projection for hot queries). Reporting tools need batch-class awareness. Heavyweight config — full SAP MM consultants exist whose job is configuring this. EAV's join-fan-out problem at scale.

### 2.2 Microsoft Dynamics 365 F&O — Inventory Dimensions framework

D365 F&O takes a **dimension-group** approach. Every inventory transaction (receipt, issue, transfer) is identified by a tuple of three dimension types:

- **Product dimensions** — Configuration, Size, Color, Style, Version. Defined per-product.
- **Storage dimensions** — Site, Warehouse, Location, Status, License Plate, Pallet. Defined per-item via Storage Dimension Group.
- **Tracking dimensions** — Batch Number, Serial Number, Owner. Defined per-item via Tracking Dimension Group.

Beyond the 15 native dimensions, Microsoft supports **12 generic extensibility slots** (InventDimension1…InventDimension12) plus the ability to add new dimensions via X++ extension. The framework auto-handles costing, tracing, and ledger posting per dimension.

**Architecture pattern:** *Strongly-typed extensibility* — fixed core, finite + named extensibility slots (not free-form). Items declare which dimensions they use via Dimension Groups.

**Pros:** Extension cost is bounded — adding a new dimension is a known, well-tooled operation. Costing posts automatically per dimension. Same UI everywhere, dimensions render conditionally.

**Cons:** 12-slot ceiling on extensibility (you'll hit it in pharma + multi-tenant scenarios). Dimension types are global, so a pharma tenant pays the storage cost of dimension fields they don't use. X++ extension is heavyweight.

### 2.3 Oracle Cloud Inventory — Lot/Serial Descriptive Flexfields + Item Attributes via Extensible Flexfields (EFFs)

Oracle's approach is the most "schema-as-data" of the bunch. Lot and Serial pages support **Descriptive Flexfields (DFFs)** — user-defined extension fields with their own typed value sets and context mappings. For more sophisticated needs, items get **Extensible Flexfields (EFFs)** that store **attribute groups** in extension tables, mapped per-item-category.

- **DFF** = simple extra columns on the lot/serial page, typed (date / number / list-of-values).
- **EFF** = full attribute-group system — context values + context-sensitive segments stored in a separate extension table, joined per category.
- Both deploy without code changes.
- Lot/Serial DFFs map per item category, so pharma items get pharma DFFs, electronics items get electronics DFFs, all on the same physical Lot table.

**Architecture pattern:** Hybrid — typed core lot table + DFF columns at the row level (sparse columns) + EFF extension tables for richer per-category attribute groups.

**Pros:** No code deployment to add a field. Per-category schemas (electronics ≠ pharma). Strong typing (number, date, LOV). Search index support.

**Cons:** Flexfield-heavy queries are awkward in BI tools. Some performance overhead vs native columns. Migration of DFF data across upgrades has gotchas.

### 2.4 NetSuite — Custom Fields + Subtype scoping

NetSuite supports adding **custom fields** to Item Receipt records, with **Subtype** scoping (Both / Purchase / Sale) and per-item-type assignment via the "Items" field. SuiteScript can validate, default, and post against these. The architecture is sparse columns on a shared table — closer to TPH than to EAV — but the field metadata is configurable per record-type without code.

**Architecture pattern:** TPH-ish with metadata-driven UI. Receipt-line custom fields can be limited to specific items / item types.

**Pros:** Familiar to admins; low-code field addition; full UI support. SuiteScript hooks for industry-specific validation.

**Cons:** Sparse columns at scale → schema bloat. Cross-industry tenants pay for fields they don't use. Less elegant than D365's dimension framework.

### 2.5 Plex (automotive-focused) — Pre-configured industry SKUs

Plex's pattern (which our ADR-013 already cited) ships **pre-configured industry templates** that pre-populate fields, validation, and approval gates. The schema is still one shared lot table, but the **policy** layer (which fields are required, which gates fire, what audit events emit) is industry-keyed.

**Architecture pattern:** Single typed lot table + policy-driven validation. The schema itself is broad enough to cover all served industries, but per-tenant + per-item-class config narrows the required subset.

**Pros:** Hot-path queries are direct column reads. No EAV penalty. Onboarding is fast (templates).

**Cons:** Schema bloat as new verticals onboard. Hard to remove a column once it's there.

### 2.6 Veeva Vault QMS, MasterControl, TrackWise — life-sciences-vertical purpose-built

These are **vertical** by design — they ship pharma/medical-device/biotech schemas baked in. Lot tracking, electronic signatures (21 CFR Part 11), audit trails, validation packs come pre-built. Veeva runs multi-tenant on AWS (Vault); MasterControl and Sparta/TrackWise also cloud-first. They do NOT try to be industry-agnostic — they sell that vertical specialization as the moat.

**Lesson for us:** the pull to specialize *per vertical* is real, and successful vertical-specific products exist. We can ship "Cherry Pharma Edition" later if needed. For now, our advantage is being able to span verticals on a single configurable platform.

### 2.7 Aptean — process-manufacturing genealogy

Aptean's food/pharma/chemical lines use **product-genealogy** as the spine — every lot has an upstream-lot graph and a downstream-finished-good graph. The schema is industry-tuned per product line (Aptean Food = different schema than Aptean Pharma), but the genealogy graph is universal. This is the **lot → nest → remnant** linkage we already model, extended forward to finished goods.

---

## Section 3: Data-modeling patterns comparison

Six architectural choices to consider. For each: definition, sketch, pros/cons, Postgres performance, EF Core flavor.

### 3.1 Pure polymorphism (one table per industry, shared base)

**Definition:** Abstract `StockReceiptBase` with FKs and timestamps; concrete `PharmaStockReceipt`, `SteelStockReceipt`, `FoodStockReceipt` tables. Each has its own typed columns.

```
                  StockReceiptBase
                  ----------------
                  Id, ReceiptNumber, ItemId, Quantity, Status, ...
                          ^   ^   ^
              ___________|   |   |___________
             |               |               |
       PharmaReceipt    SteelReceipt    FoodReceipt
       LotNumber        HeatNumber      TraceabilityLotCode
       Serial           MillCertUrl     OrganicCert
       ExpDate          AstmDesignation BestByDate
```

**Pros:** Strong typing per industry; no nulls; pure ANSI SQL queries.
**Cons:** Cross-industry queries require UNION ALL. Adding a 15th industry = 15th table. Doesn't compose well with `Nest.StockReceiptId` FK — what does it point at? Voice-AI must know which table to query. UI must render 15 different edit pages.
**Postgres perf:** Excellent per-table.
**EF Core:** TPC (Table-Per-Concrete) inheritance — supported but awkward for our existing FK shape.
**When to use:** When you have ≤3 industries permanently and absolute referential integrity matters.
**When NOT to use:** Multi-tenant, multi-industry SaaS — i.e. us.

### 3.2 Table-Per-Hierarchy (TPH) — one fat table with all columns nullable

**Definition:** Single `StockReceipts` table with every industry's columns as nullable. Discriminator column (`IndustryProfile`) tells the app which fields to render.

```
StockReceipts
-------------
Id, ReceiptNumber, ItemId, Quantity, ...
IndustryProfile
HeatNumber?, MillCertUrl?, AstmDesignation?       -- steel
LotNumber?, SerialNumber?, ExpirationDate?, DEAClass?  -- pharma
TraceabilityLotCode?, BestByDate?, AllergenProfile?    -- food
METRCTag?, COAUrl?, ...                                -- cannabis
... (200 columns)
```

**Pros:** All rows in one table; single FK target for `Nest.StockReceiptId`; one EF entity; one Razor edit page (conditional rendering).
**Cons:** 200-column tables. Schema migration every new vertical. Sparse-column waste. Tools (DDL, EF migrations) get slow. Naming collisions inevitable (we already hit this twice this sprint — see `feedback_namespace_enum_collisions.md`).
**Postgres perf:** Good for column reads; bad for storage (NULLs are ~1 bit each in Postgres heap, so cost is mostly DDL maintenance not row size).
**EF Core:** Native TPH support via discriminator — easiest .NET pattern.
**When to use:** ≤5 industries with mostly-overlapping fields.
**When NOT to use:** Indefinite vertical expansion.

### 3.3 EAV — generic Receipt + ReceiptAttribute(K,V,Type) table

**Definition:** Receipt has core columns + zero-N rows in `ReceiptAttribute` table keyed on `(ReceiptId, AttributeKey)`.

```
StockReceipts                    ReceiptAttributes
-------------                    -----------------
Id, ReceiptNumber, ItemId       Id, ReceiptId FK
                                AttributeKey ("HeatNumber" / "LotNumber" / ...)
                                ValueText | ValueDate | ValueNumeric
                                DataType
```

**Pros:** Infinitely flexible at runtime; UI auto-renders from attribute metadata; new fields = INSERT into metadata table.
**Cons:** EVERY field read is a JOIN. EAV writes are notoriously slow due to per-attribute index updates; benchmarks show **JSONB beats EAV by up to 1000× on write throughput**. Reporting is hellish. Type safety is nominal at best.
**Postgres perf:** Worst-of-class for both reads and writes at million-row scale. Index per attribute key is unbounded.
**EF Core:** Custom mapping; awkward.
**When to use:** Almost never in 2026. This was the right answer in Oracle 9i; Postgres has JSONB now.
**When NOT to use:** Anywhere JSONB is available.

### 3.4 JSON column extension — base + jsonb attributes

**Definition:** Typed core columns + a single `attributes jsonb` column holding everything industry-specific. Schemaless to the database.

```
StockReceipts
-------------
Id, ReceiptNumber, ItemId, Quantity, Status, ...
ProfileId FK -> ReceiptProfile
attributes jsonb  -- { "heatNumber": "H-12345", "mill": "Nucor", ... }
                  -- OR { "lotNumber": "L-9988", "expirationDate": "2027-06-30", "deaSchedule": "II", ... }
                  -- OR { "metrcTag": "1A4FF030...", "coaUrl": "..." }
```

**Pros:** Zero migrations for new fields. Single FK target. Postgres JSONB is fast (jsonb_path_ops + GIN indexing). Native EF Core 8+ support (`HasColumnType("jsonb")` + JSON value converter). Voice-AI can introspect the JSON for fields-by-name. Outperforms EAV by ~1000×.
**Cons:** No DB-level type enforcement (must validate in service). Reporting tools need to know JSONB ops. Containment-only GIN indexes (jsonb_path_ops) won't accelerate range queries — needs expression indexes for hot fields. `->>` (text extraction) is NOT accelerated by GIN.
**Postgres perf:** GIN containment fast; range queries need expression indexes (B-tree on `(attributes ->> 'expirationDate')::date`). JSONB updates rewrite the whole jsonb value (Postgres limitation), so very wide JSON is slow to update — but at receipt scale (a few dozen fields per row) it's fine.
**EF Core:** EF 8+ has `OwnsOne(...).ToJson()` or raw string column with manual JSON serialization. We already use jsonb on `RegulatoryProfile.Gates`.
**When to use:** Anywhere you'd otherwise pick EAV. Most B2B SaaS that needs vertical flexibility.

### 3.5 Profile-driven hybrid — typed core + jsonb + profile defines shape

**Definition:** Pattern 3.4 + a config table that defines the JSON Schema for each industry, plus seeded validation, plus admin-side schema management.

```
ReceiptProfiles                    StockReceipts
---------------                    -------------
Id                                 Id, ReceiptNumber, ItemId,
Name ("Pharma", "Steel", ...)      Quantity, Status, ...
JsonSchema jsonb                   ProfileId FK -> ReceiptProfile
PromotedColumns text[]             attributes jsonb
RegulatoryProfileIds int[]
DefaultValues jsonb
UiFormSpec jsonb                   -- admin Razor reads UiFormSpec
                                   -- to render the right form per profile
```

**Pros:** All of 3.4 *plus* the schema is itself queryable + versioned + admin-editable. UI is profile-driven (one Razor page renders every industry's form). New industry = INSERT a profile, zero deployment. Voice-AI can ask "what fields exist for the pharma profile?" and answer arbitrary queries. JSON Schema gives us validation, defaults, documentation.
**Cons:** More moving parts up-front. The UI must read the profile to render the form (vs hard-coded fields). Voice-AI prompt has to include profile schema in context (manageable — small per receipt).
**Postgres perf:** Same as 3.4 + selective expression indexes on hot fields per profile (see "Promoted facet columns" in §4).
**When to use:** Multi-tenant multi-industry SaaS with voice-AI / LLM intent. **This is what we recommend.**

### 3.6 Strongly-typed industry profiles — ItemMaster.IndustryProfileId → fixed-schema satellite tables

**Definition:** Like 3.1 (polymorphism) but driven from `ItemMaster.IndustryProfileId`. Each industry has its own satellite table (`PharmaReceiptDetails`, `SteelReceiptDetails`) linked 1:0..1 to `StockReceipts`. Receipt core stays in one table; details live in a typed satellite.

```
StockReceipts (1)
-------------
Id, ReceiptNumber, ItemId, ProfileId, Quantity, ...
   |
   |---- 1:0..1 ---->  PharmaReceiptDetails
   |                   ReceiptId PK/FK
   |                   LotNumber, SerialNumber, ExpDate, DEAClass, NDC, ...
   |
   |---- 1:0..1 ---->  SteelReceiptDetails
   |                   ReceiptId PK/FK
   |                   HeatNumber, MillCertUrl, AstmDesignation, ...
   |
   |---- 1:0..1 ---->  FoodReceiptDetails
                       ReceiptId PK/FK
                       TraceabilityLotCode, BestByDate, ...
```

This is *exactly* the WorkOrder satellite pattern shipped in Sprint 3 Phase D (CipWorkOrderDetails, QualityWorkOrderDetails, EngineeringWorkOrderDetails, HseWorkOrderDetails).

**Pros:** Strong typing; per-industry indexes; precedent in our codebase; no JSONB knowledge required for backend devs.
**Cons:** Adding a vertical = a new table + EF entity + Razor page + service. NOT zero-code. UI render logic must dispatch on profile. Doesn't generalize beyond the seeded profiles.
**Postgres perf:** Excellent — every field is native typed.
**EF Core:** Beautiful — straight 1:0..1 relationships, optional `.Include()`.
**When to use:** When you have a finite, stable set of verticals (≤10) AND you DO want to ship a vertical-specific Razor page per profile.

---

## Section 4: Recommendation — Profile-driven typed-core + JSONB hybrid (with promoted facet columns)

**Recommendation: Pattern 3.5 (profile-driven hybrid) with selective promoted columns** for hot-query fields per profile.

### 4.1 Why this and not the satellite-table pattern (3.6)

We already use the satellite pattern for WorkOrder (CIP / Quality / Engineering / HSE). It works *because* those four satellites are stable, internally-defined, and the universe is closed. **StockReceipt is the opposite** — the universe is open (10+ industries today, more later), tenants will want their own variants, and a non-trivial chunk of the value proposition is "your industry just works on day one without a code deploy." Satellites would force a code deploy per new vertical and force `IStockReceiptService` into N method signatures.

The hybrid pattern lets us **keep 80% of the strong-typing benefit** (everything in the core is typed; promoted facets are typed columns) while **buying out of the schema-migration tax** for the long tail of industry-specific fields.

### 4.2 Schema shape

```
+---------------------------------------------+
| ReceiptProfiles                             |
+---------------------------------------------+
| Id                          serial PK       |
| Code                        varchar(64) UQ  |  -- "PHARMA", "STEEL", "FOOD", "CANNABIS"...
| Name                        varchar(128)    |
| Description                 varchar(500)    |
| JsonSchema                  jsonb           |  -- the schema for attributes
| PromotedFacets              text[]          |  -- ["heatNumber","mill"] or ["lotNumber","expirationDate","ndc"]
| DefaultAttributes           jsonb           |  -- defaults seeded on new receipt
| UiFormSpec                  jsonb           |  -- field-order, field-grouping, labels, voice hints
| RegulatoryProfileIds        int[]           |  -- which RegulatoryProfile rows this triggers
| IsActive                    bool            |
| CreatedAt / By, ModifiedAt / By             |
+---------------------------------------------+

+---------------------------------------------+
| StockReceipts (extended)                    |
+---------------------------------------------+
| Id                          serial PK       |
| ReceiptNumber               varchar(32) UQ  |
| ItemId                      FK -> Items     |
| MaterialMasterId            FK?             |
| ProfileId                   FK -> ReceiptProfile  -- NEW
| LotNumber                   varchar(128)    |  -- promoted, universal
| SerialNumber                varchar(128)    |  -- promoted, universal (lot/serial duality, both nullable)
| ReceivedAt                  timestamptz     |
| ReceivedByUserId            FK?             |
| LocationId                  FK?             |
| QuantityReceived            numeric(18,4)   |
| QuantityRemaining           numeric(18,4)   |
| Uom                         varchar(16)     |
| SourcePoNumber              varchar(64)     |
| SourcePoLineId              varchar(64)     |
| Status                      enum            |
| QuarantineReason            varchar(500)    |
| Notes                       varchar(2000)   |
| Attributes                  jsonb           |  -- NEW: profile-defined extension
| -- Per-profile promoted facet columns (generated from Attributes, indexed individually): --
| FacetHeatNumber             varchar(64)     |  -- expression-indexed when profile=steel
| FacetExpirationDate         date            |  -- expression-indexed when profile=pharma/food
| FacetTraceabilityLotCode    varchar(64)     |  -- expression-indexed when profile=food
| -- (or we go pure expression-index, see 4.5) --
| CreatedAt / By, ModifiedAt / By, RowVersion (xmin) |
+---------------------------------------------+
```

We **drop** these existing columns (they move into `Attributes` under the Steel profile):
- `HeatNumber`, `MillCertUrl`, `Mill`, `LengthMm`, `WidthMm`, `ThicknessMm`, `UsableLengthMm`, `UsableWidthMm`.

We **promote** these to the core (every industry needs them):
- `LotNumber` (already there)
- `SerialNumber` (new — used by pharma serial, electronics, automotive, medical, oil&gas)

### 4.3 Migration path from current sheet-metal-centric schema

1. **Add columns first** (additive, zero-downtime): `ProfileId`, `SerialNumber`, `Attributes jsonb`.
2. **Seed `ReceiptProfiles`** with at least 11 starter profiles (see §7).
3. **Backfill existing rows**: every existing `StockReceipt` is for steel, so set `ProfileId = (id of STEEL profile)`. Copy `HeatNumber/MillCertUrl/Mill/Length/Width/Thickness/UsableLength/UsableWidth` into `Attributes` JSON. Verify counts.
4. **Add not-null constraint** on `ProfileId` once backfill is verified.
5. **Add expression indexes** for the Steel profile's hot facets: `(attributes ->> 'heatNumber')`, `(attributes ->> 'mill')`.
6. **Update `IStockReceiptService`** signatures (see §4.7).
7. **Update Razor pages** to render from `UiFormSpec` (one Edit page renders every profile).
8. **Drop the legacy columns** after one sprint of running both representations in parallel + verifying.

### 4.4 Voice-AI query translation

The voice-AI co-pilot (Sprint 5) gets a major boost from this schema: every receipt carries an explicit profile id, the profile carries the field catalog, and the LLM can build a SQL query directly. Five examples:

| Voice input | LLM-built query |
|---|---|
| "Find all receipts of heat H-12345" | `SELECT * FROM "StockReceipts" WHERE attributes ->> 'heatNumber' = 'H-12345'` (hits expression index when profile=Steel) |
| "Find all lots of lidocaine expiring within 30 days" | `JOIN Items ON ItemId WHERE Items.Description ILIKE '%lidocaine%' AND (attributes ->> 'expirationDate')::date <= now() + interval '30 days' AND ProfileId = (SELECT Id FROM ReceiptProfiles WHERE Code = 'PHARMA')` |
| "Quarantine all spinach received from supplier SmartGreens on lot SG-2026-44" | `UPDATE StockReceipts SET Status = 'Quarantined' WHERE LotNumber='SG-2026-44' AND attributes @> '{"supplierName":"SmartGreens"}'` then audit row |
| "Show all electronics receipts with MSL 3 that opened more than 168 hours ago" | `WHERE ProfileId = (Electronics) AND attributes @> '{"mslLevel":3}' AND (attributes ->> 'bagOpenedAt')::timestamptz < now() - interval '168 hours'` |
| "What's the chain of custody for serial 9847-A?" | `WHERE SerialNumber = '9847-A'` then graph-walk through `Nest` / `Remnant` / outbound `Shipment` |

Because the LLM knows the profile schema (from `ReceiptProfile.JsonSchema` injected into prompt context), it can compose queries grammatically. The voice-AI also gets validation: if a user says "find heat H-12345" but the active profile is Pharma (which has no `heatNumber` field), the AI can prompt "no such field on this profile — did you mean lot number?"

### 4.5 Promoted facet columns — the performance trick

For the 5–10 fields per profile that **drive 90% of queries**, we don't want to pay the `(attributes ->> 'fieldName')::cast` interpretation cost. Two ways:

**Option A — Postgres generated columns:**
```sql
ALTER TABLE "StockReceipts"
ADD COLUMN "FacetHeatNumber" varchar(64)
GENERATED ALWAYS AS (attributes ->> 'heatNumber') STORED;
CREATE INDEX ix_receipt_facet_heat ON "StockReceipts" ("FacetHeatNumber");
```

**Option B — Pure expression indexes:**
```sql
CREATE INDEX ix_receipt_attr_heat ON "StockReceipts" ((attributes ->> 'heatNumber'));
CREATE INDEX ix_receipt_attr_exp_date ON "StockReceipts" (((attributes ->> 'expirationDate')::date));
CREATE INDEX ix_receipt_attr_metrc ON "StockReceipts" ((attributes ->> 'metrcTag'));
```

**Recommendation: Option B (expression indexes)** for most fields. They're cheap to create, can be dropped without DDL on the table, and don't bloat the row width. Reserve Option A (generated columns) for fields that need to participate in foreign keys (e.g., if `traceabilityLotCode` needs to join to a `LotMaster` table later).

GIN on `attributes` for full containment queries:
```sql
CREATE INDEX ix_receipt_attr_gin ON "StockReceipts" USING gin (attributes jsonb_path_ops);
```
This gives O(log n) on `attributes @> '{"key":"value"}'` for any key. Note: `jsonb_path_ops` doesn't index by key alone — for that, use the default `jsonb_ops`. We probably want the default given mixed query shapes.

### 4.6 Postgres performance at million-row scale

Published benchmarks:
- JSONB writes outperform EAV by up to ~1000×.
- GIN index lookups on million-row JSONB tables are sub-millisecond for containment queries.
- Expression indexes on `(jsonb ->> 'key')` perform identically to native B-tree on that same column for equality + range.
- GIN is *not* used by `->>` operator alone — must use containment (`@>`) for GIN, OR add an expression index.
- Whole-row JSONB updates rewrite the entire JSONB column, so deep documents (>10KB) are expensive to update. Our receipts top out at maybe 2–3KB of JSON — well under the threshold.

**Verdict:** At realistic CherryAI EAM scale (hundreds of millions of receipts is the worst case for a megacorp tenant — most tenants will be in the low millions), Postgres + JSONB + selective expression indexes is **comfortably faster** than EAV, **competitive with native columns** for the hot 5–10 fields per profile (via expression indexes), and **trivially cheap** for the long tail of cold fields.

### 4.7 `IStockReceiptService` evolution

Today's `CreateStockReceiptRequest` / `UpdateStockReceiptRequest` records carry every steel-specific column. We need to make them profile-aware. Approach: **collapse industry fields into a `Dictionary<string,object>` Attributes payload** + keep core fields explicit.

```csharp
public sealed record CreateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    int ProfileId,                                   // NEW — required
    string? LotNumber,
    string? SerialNumber,                            // NEW — promoted universal
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    string? Uom,
    string? SourcePoNumber,
    string? SourcePoLineId,
    StockReceiptStatus Status,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes  // NEW — profile-defined extension
);
```

`StockReceiptService.CreateAsync` then:
1. Loads `ReceiptProfile` by `ProfileId`.
2. Validates `Attributes` against `Profile.JsonSchema` (using e.g. `JsonSchema.Net` — pure-managed C# JSON Schema validator).
3. Merges `Profile.DefaultAttributes` into the payload.
4. Writes the row with `Attributes` as a serialized JSONB.
5. Fires the gates listed in `RegulatoryProfileIds` linked through `ReceiptProfile`.
6. Writes audit row, idempotency key, voice-event the usual way.

### 4.8 Admin UI evolution — profile-driven form rendering

`UiFormSpec` is a jsonb document like:

```json
{
  "groups": [
    {
      "title": "Identity",
      "fields": [
        { "key": "lotNumber", "label": "Lot #", "type": "text", "voice": ["lot","batch"] },
        { "key": "serialNumber", "label": "Serial", "type": "text", "voice": ["serial"] }
      ]
    },
    {
      "title": "Traceability",
      "fields": [
        { "key": "heatNumber", "label": "Heat #", "type": "text", "required": true, "voice": ["heat","heat number"] },
        { "key": "mill", "label": "Mill", "type": "text" },
        { "key": "millCertUrl", "label": "Mill Cert URL", "type": "url" },
        { "key": "astmDesignation", "label": "ASTM", "type": "lov", "lovSource": "MaterialMaster.AstmDesignation" }
      ]
    },
    {
      "title": "Dimensions",
      "fields": [
        { "key": "lengthMm", "label": "Length (mm)", "type": "decimal", "scale": 2 },
        { "key": "widthMm", "label": "Width (mm)", "type": "decimal", "scale": 2 },
        { "key": "thicknessMm", "label": "Thickness (mm)", "type": "decimal", "scale": 2 }
      ]
    }
  ]
}
```

The Razor page `Pages/Admin/StockReceipts/Edit.cshtml` reads this and renders **one form per profile**, **dynamically**, against the same backing model. We can build a small generic `<receipt-form profile="@Model.Profile" data="@Model.Attributes" />` TagHelper — fits the ADR-014 voice-ready TagHelper pattern shipped in Sprint 4 PR #1.

### 4.9 Interaction with planned Sprint 7 (Item Master Expansion) and Sprint 8 (Multi-Dim Inventory)

**Sprint 7 (Item Master):** `Item.IndustryProfileId` (or `Item.DefaultReceiptProfileId`) becomes the default for receipts created against that item. UI auto-selects the right profile. Items can also override profile per-receipt (rare but useful for the "this batch is special" case).

**Sprint 8 (Multi-Dim Inventory, D365 F&O-style):** This proposal sits *underneath* the D365 dimensions framework. `LotNumber` and `SerialNumber` are the universal tracking dimensions (already promoted). Industry-specific attributes are *characteristics on the lot*, not dimensions. This is the same separation D365 enforces (dimensions vs lot attributes via DFFs / batch attributes). Clean fit.

### 4.10 The PO-driven receiving workflow — receipts are born from POs, not from blank forms

This is the most important workflow insight in this document. **A StockReceipt is almost never hand-typed from scratch.** In every real receiving operation — pharma warehouse, steel yard, electronics distributor, food cold-storage — the receipt is **born from a Purchase Order line** at the moment the truck rolls up to the dock. The receiver's job is mostly **verification and document capture**, not data entry.

This changes the admin UI direction we just shipped in PR #219 (blank "New Receipt" form), changes the service API surface, and gives the hybrid schema even more leverage than it has on paper.

#### 4.10.1 What actually gets typed at the dock

For a receipt with ~30 fields, the realistic data-entry breakdown is:

| Source | % of fields | What |
|---|---|---|
| **PO line + Supplier master + Item master** | ~70-80% | `ItemId`, `MaterialMasterId`, `SourcePoNumber`, `SourcePoLineId`, expected `QuantityReceived`, `Uom`, supplier name + DUNS, default `Mill` for steel buys, default DEA Schedule for pharma SKUs, default MSL level for electronics SKUs, expected country of origin, expected `ProfileId` (driven by Item's industry classification), `LocationId` default (per Item's default storage zone), `RegulatoryProfileIds` gates to fire |
| **ASN (Advance Ship Notice) — if supplier sends one** | +10-15% | `LotNumber`, `SerialNumber` ranges, `ExpirationDate`, `HeatNumber` (steel ASN), `ManufactureDate`, `CountryOfOrigin` (per-lot), pallet/case `SSCC` (GS1 Serial Shipping Container Code), `GTIN`, container temperature log, GS1-128 / DSCSA T3 data, batch-level COA URL |
| **Supplier-provided physical documents** (mill cert PDF, COA, packing slip, BoL) | ~10% | Operator types off the doc OR OCR pulls: actual `HeatNumber`, `MillCertUrl` (uploaded to Box/SharePoint), `BatchNumber`, `ExpirationDate`, `LotNumber` for non-ASN suppliers, country of origin, supplier signature/inspector ID |
| **Receiver's eyes + scanner** | ~5% | `ActualQuantityReceived` (vs expected), damage notes, temperature reading at arrival, packing condition, container counts, immediate-quarantine flag if visibly damaged or out-of-temp |

The implication: **the "create receipt" admin form is the wrong primary entry point.** The primary entry point is **"Receive against PO."** The receiver opens a PO, picks a line (or scans a barcode that resolves to a PO line), and a receipt draft materializes with 80% of the fields already populated. The receiver then attaches documents, fixes any deltas, and posts.

#### 4.10.2 What this means for the service API

We need two service methods, not one:

```csharp
// Primary path — used 95%+ of the time
Task<Result<StockReceipt>> CreateFromPoLineAsync(
    long purchaseOrderLineId,
    ReceiveAgainstPoRequest request,    // actual qty + lot/serial + docs + delta notes
    int actorUserId,
    Guid? idempotencyKey,
    CancellationToken ct);

// Escape hatch — non-PO receipts (intercompany transfers, returns, found-stock,
// inventory adjustments, prototype receipts that bypass purchasing)
Task<Result<StockReceipt>> CreateAdHocAsync(
    CreateStockReceiptRequest request,
    int actorUserId,
    Guid? idempotencyKey,
    CancellationToken ct);
```

`CreateFromPoLineAsync` does the heavy lifting:

1. **Load the PO line** + parent PO + supplier + item + item's default receipt profile.
2. **Pull defaults** from `Item.DefaultReceiptAttributes` (e.g. `{"deaSchedule":"II","mslLevel":3}` from the Item master) + `Supplier.DefaultReceiptAttributes` (e.g. `{"mill":"Nucor Steel — Decatur, AL"}` from the supplier master).
3. **Pull ASN data** if present (a new `AdvanceShipNotice` / `AsnLine` table — see §4.10.5).
4. **Merge** in the operator-typed delta from `ReceiveAgainstPoRequest`.
5. **Validate** the merged `Attributes` payload against `ReceiptProfile.JsonSchema`.
6. **Persist** the receipt and create a `PoLineReceiptLink` row so we can roll the PO line's open-quantity down.
7. **Fire** the `RegulatoryProfileIds` gates (e.g. "DEA222 confirmation required" for Schedule II pharma).
8. **Audit + idempotency + voice-event** as usual.

This is also where **partial receipts**, **over-receipts**, and **multi-receipt PO lines** get handled — all driven from the PO line, not from the receipt side.

#### 4.10.3 What this means for the admin UI

The admin sidebar should expose **three** receiving paths, not one:

- **`/Receiving/Inbox`** — primary surface. Shows open PO lines expected this week + ASNs not yet received. Click → resolves to the "Receive PO Line" wizard.
- **`/Receiving/PO/{poId}`** — the wizard. Pre-populated with all 80%. Operator confirms quantity + attaches docs + posts.
- **`/Admin/StockReceipts`** — the data grid we just shipped in PR #219. Becomes a **lookup / audit** view, not a creation view. The "New Receipt" button on the page should route to the PO Inbox by default (with a small "Ad-hoc receipt" affordance for the 5% non-PO case).

The `Edit` form we just shipped is still correct for the **edit existing** case (correcting a typo'd lot # after the fact), but is *not* the right primary creation entry point. We don't need to throw away the work — it gets reused for the audit/edit path.

#### 4.10.4 Why the hybrid schema is *more* valuable in this workflow

Because 80% of attribute values come from upstream sources (PO, Item, Supplier, ASN), and the upstream sources already know which profile applies to which item, **the receiver almost never picks a profile manually**. The profile is implied by `Item.DefaultReceiptProfileId`. This means:

- The hybrid schema's "every receipt has a profile" constraint is *free* at the workflow level — the profile is computed from the item, not from a UI control.
- The Attributes payload is **pre-filled** before the receiver ever sees the form. Their job is to verify deltas, not fill blanks.
- Profile-driven validation runs once at post-time, not on every keystroke.
- Voice-AI: "Receive PO 12345 against item A36-0.250, 5 sheets" — the voice agent reads the PO, knows the item is Steel-profiled, knows the expected fields, and asks **only** the deltas: "What's the heat number on each sheet?"

The "blank form for receiver to fill out" model that PR #219 currently implements is *the wrong workflow*. The hybrid schema makes the *right* workflow trivial.

#### 4.10.5 New supporting tables this surfaces

To do PO-driven receiving properly, the doc needs to acknowledge two adjacent schema additions (not in scope for the immediate ADR-015 migration but worth flagging):

```
+----------------------------------+   +----------------------------------+
| AdvanceShipNotice                |   | AsnLine                          |
+----------------------------------+   +----------------------------------+
| Id                  serial PK    |   | Id                  serial PK    |
| AsnNumber           varchar(64)  |   | AsnId               FK -> ASN    |
| PurchaseOrderId     FK?          |   | PoLineId            FK?          |
| SupplierId          FK           |   | ItemId              FK           |
| ExpectedArrivalAt   timestamptz  |   | ExpectedQuantity    numeric(18,4)|
| CarrierCode         varchar(64)  |   | LotNumber           varchar(128) |
| TrackingNumber      varchar(128) |   | SerialRangeStart    varchar(128) |
| Bol                 varchar(64)  |   | SerialRangeEnd      varchar(128) |
| EpcisDocumentUrl    varchar(500) |   | ExpirationDate      date         |
| CreatedAt / By                   |   | Attributes          jsonb        |  -- profile-shape payload
+----------------------------------+   +----------------------------------+

+----------------------------------+
| PoLineReceiptLink (bridge)       |
+----------------------------------+
| Id                  serial PK    |
| PoLineId            FK -> PoLine |
| StockReceiptId      FK -> Receipt|
| QuantityApplied     numeric(18,4)|  -- supports partial / multi-receipt
| AppliedAt           timestamptz  |
+----------------------------------+

+-------------------------------------------------+
| Item (existing, extended)                       |
+-------------------------------------------------+
| ...                                             |
| DefaultReceiptProfileId  FK -> ReceiptProfiles  |  -- which profile applies
| DefaultReceiptAttributes jsonb                  |  -- per-item defaults
+-------------------------------------------------+

+-------------------------------------------------+
| Supplier / Vendor (existing, extended)          |
+-------------------------------------------------+
| ...                                             |
| DefaultReceiptAttributes jsonb                  |  -- per-supplier defaults
| SendsAsn                bool                    |  -- supplier sends ASNs?
| AsnFormat               varchar(32)             |  -- 'EDI856' / 'EPCIS' / 'CSV' / 'NONE'
+-------------------------------------------------+
```

ASN ingestion is a downstream-of-ADR-015 effort (probably its own sprint), but **knowing the table is coming changes how we name and shape things today** — specifically, the `Attributes` column we just defined on StockReceipt is the *same shape* as the `Attributes` column on AsnLine, and the merge from ASN → Receipt becomes a straight JSON deep-merge. If we didn't anticipate this, we'd risk reshaping the attribute payload twice.

#### 4.10.6 The "no PO" cases that justify the escape hatch

For completeness, here are the receipts that bypass a PO entirely — these are why `CreateAdHocAsync` exists alongside `CreateFromPoLineAsync`:

- **Customer return** — material flowing in from a customer for inspection/credit (separate workflow really, but creates a stock receipt under the hood).
- **Intercompany transfer** — material moving from another plant under the same parent company.
- **Customer-supplied material** — toll-manufacturing scenarios where the customer ships you their raw material for you to process. No PO; the customer's purchase order *to themselves* is the trace point.
- **Found stock / cycle-count discovery** — physical inventory turns up material with no system record; recipient creates a receipt to explain its existence.
- **Prototype / dev / R&D buys** — small purchases made on a corporate card outside the PO system. Receiving still posts a receipt for compliance.
- **Sample / freebie** — supplier sends a free sample that goes into inventory; no PO.
- **Returns to vendor in reverse** — when a partial scrap allowance comes back as a credit-bearing receipt.

For each of these, the operator picks a profile manually (or the system picks based on the item) — same hybrid schema applies, just with no PO to pull defaults from.

#### 4.10.7 Workflow alignment with voice-AI

The voice-AI co-pilot becomes dramatically more useful in the PO-driven model:

| Voice input | Voice agent action |
|---|---|
| "Receive PO 12345" | Opens the wizard pre-populated with all 80% defaults; asks the receiver only the deltas (qty + lot # + heat # / expiry / lot if not on ASN) |
| "Is the truck for PO 12345 here yet?" | Looks up expected arrival from ASN; reports status |
| "Quarantine line 3 of PO 12345 — pallet looks damaged" | Marks the to-be-received line as quarantined-on-arrival; doesn't post a normal receipt; routes to inspector |
| "Show me overdue ASNs" | Reports ASNs past their expected arrival window |
| "What did supplier Nucor send us on PO 12345?" | Pulls the ASN payload + any received line + delta vs PO line |
| "Apply 50 of these to PO 12345 line 2 and 25 to line 3" | Splits the physical receipt across two PO lines via PoLineReceiptLink |

All of this is **impossible** without `CreateFromPoLineAsync` as the primary code path.

#### 4.10.8 What this means for the migration plan in §4.3

The migration plan in §4.3 stands, but two items need to be added at the front of the sequence:

- **§4.3.0a — Extend `Item` with `DefaultReceiptProfileId` + `DefaultReceiptAttributes`** before the receipt-side migration runs, so the backfill can resolve `ProfileId` from the item rather than guessing from the data.
- **§4.3.0b — Extend `Vendor` (Supplier) with `DefaultReceiptAttributes` + `SendsAsn` + `AsnFormat`** in the same migration window. Tiny columns, low risk, dramatically improves the receiver experience the day ADR-015 lands.

The `AdvanceShipNotice` / `AsnLine` / `PoLineReceiptLink` tables can land in a follow-up sprint (probably Sprint 9 alongside the ItemEdit restructure) without blocking ADR-015 itself.

---

## Section 5: Counter-arguments + things to validate

### 5.1 The strongest arguments AGAINST

1. **"Two of your existing satellite WorkOrder patterns work fine; why diverge for receipts?"** — Counter: WorkOrder satellites are 4 fixed internal subtypes (CIP, Quality, Engineering, HSE) — closed universe. Receipts span 10+ external industries with tenant-driven variants — open universe. Different tool for different problem.

2. **"JSONB validation in service is weaker than DB-level CHECK constraints."** — True. A bad migration or a service bug can leak invalid JSON. Mitigation: JSON Schema validation in `IStockReceiptService`, plus a Postgres `CHECK (jsonb_typeof(attributes) = 'object')` constraint as a backstop, plus a nightly `attributes`-conformance audit job that flags drift.

3. **"Reporting tools don't speak JSONB well."** — Partially true. Tableau and Power BI need either views or explicit cast columns. Mitigation: ship per-profile **reporting views** (`vw_receipts_pharma`, `vw_receipts_steel`) that flatten JSONB into typed columns. Generate them from `ReceiptProfile.JsonSchema`. This also doubles as documentation.

4. **"Voice-AI prompt context grows linearly with profile field count."** — Manageable. Even pharma's 20-field profile is ~2KB of schema. Whole platform-wide profile catalog is maybe 25KB — fits comfortably in modern LLM context.

5. **"You're betting on JSON Schema as a long-term spec."** — JSON Schema is in active development at IETF; Draft 2020-12 is stable; tooling (`JsonSchema.Net` in .NET) is mature. Low risk.

### 5.2 Things to validate before adopting

1. **Run the migration on a copy of prod** and time the `HeatNumber` → `Attributes` backfill. Need to know if it's a 30-second or a 30-minute migration so we can plan downtime / maintenance window.
2. **Benchmark expression-index query plans** on a synthesized 10M-row table to verify the planner does pick the expression index for `attributes ->> 'heatNumber' = ?`. Postgres usually does, but plan-stability under stats churn is worth confirming.
3. **Spike the voice-AI side**: feed three profile schemas to Claude/GPT and verify it generates the expected query for each of the 5 example utterances. If it consistently misses, we may need richer profile metadata (synonyms, field-types-by-voice-utterance hints).
4. **Validate the JSON Schema validator's performance** — `JsonSchema.Net` benchmarks are excellent but we want to confirm sub-millisecond validation on a 30-field receipt under our load.
5. **Confirm the Razor TagHelper approach can render every profile** — build it once for Steel + Pharma + Food and verify it's truly one component, not three.

---

## Section 6: Concrete schema — SQL + EF Core

### 6.1 New / modified tables

```sql
-- New: ReceiptProfile catalog (one per industry vertical)
CREATE TABLE "ReceiptProfiles" (
    "Id"                serial PRIMARY KEY,
    "Code"              varchar(64)  NOT NULL UNIQUE,
    "Name"              varchar(128) NOT NULL,
    "Description"       varchar(500),
    "JsonSchema"        jsonb        NOT NULL,
    "PromotedFacets"    text[]       NOT NULL DEFAULT '{}',
    "DefaultAttributes" jsonb        NOT NULL DEFAULT '{}'::jsonb,
    "UiFormSpec"        jsonb        NOT NULL DEFAULT '{}'::jsonb,
    "RegulatoryProfileIds" int[]     NOT NULL DEFAULT '{}',
    "IsActive"          boolean      NOT NULL DEFAULT TRUE,
    "CreatedAt"         timestamptz  NOT NULL DEFAULT now(),
    "CreatedBy"         varchar(100),
    "ModifiedAt"        timestamptz,
    "ModifiedBy"        varchar(100)
);

-- Modified: StockReceipts — add Profile FK, LotNumber/SerialNumber promoted, Attributes jsonb
ALTER TABLE "StockReceipts"
    ADD COLUMN "ProfileId"      integer       REFERENCES "ReceiptProfiles"("Id"),
    ADD COLUMN "SerialNumber"   varchar(128),
    ADD COLUMN "Attributes"     jsonb         NOT NULL DEFAULT '{}'::jsonb,
    ADD CONSTRAINT "ck_receipt_attributes_object"
        CHECK (jsonb_typeof("Attributes") = 'object');

-- Backfill: every existing row is Steel — copy legacy columns into Attributes
UPDATE "StockReceipts"
SET "ProfileId" = (SELECT "Id" FROM "ReceiptProfiles" WHERE "Code" = 'STEEL'),
    "Attributes" = jsonb_strip_nulls(jsonb_build_object(
        'heatNumber', "HeatNumber",
        'mill', "Mill",
        'millCertUrl', "MillCertUrl",
        'lengthMm', "LengthMm",
        'widthMm', "WidthMm",
        'thicknessMm', "ThicknessMm",
        'usableLengthMm', "UsableLengthMm",
        'usableWidthMm', "UsableWidthMm"
    ))
WHERE "ProfileId" IS NULL;

ALTER TABLE "StockReceipts" ALTER COLUMN "ProfileId" SET NOT NULL;

-- Indexes — expression indexes for hot facets across profiles
CREATE INDEX ix_receipts_profile           ON "StockReceipts" ("ProfileId");
CREATE INDEX ix_receipts_lot               ON "StockReceipts" ("LotNumber");
CREATE INDEX ix_receipts_serial            ON "StockReceipts" ("SerialNumber");
CREATE INDEX ix_receipts_attr_gin          ON "StockReceipts" USING gin ("Attributes");
CREATE INDEX ix_receipts_attr_heat         ON "StockReceipts" (("Attributes" ->> 'heatNumber'));
CREATE INDEX ix_receipts_attr_expdate
    ON "StockReceipts" ((("Attributes" ->> 'expirationDate')::date));
CREATE INDEX ix_receipts_attr_metrc        ON "StockReceipts" (("Attributes" ->> 'metrcTag'));
CREATE INDEX ix_receipts_attr_tlc          ON "StockReceipts" (("Attributes" ->> 'traceabilityLotCode'));
CREATE INDEX ix_receipts_attr_udi          ON "StockReceipts" (("Attributes" ->> 'udiDi'));

-- After one sprint of dual-write verification, drop legacy columns:
-- ALTER TABLE "StockReceipts"
--   DROP COLUMN "HeatNumber",
--   DROP COLUMN "MillCertUrl",
--   DROP COLUMN "Mill",
--   DROP COLUMN "LengthMm",
--   DROP COLUMN "WidthMm",
--   DROP COLUMN "ThicknessMm",
--   DROP COLUMN "UsableLengthMm",
--   DROP COLUMN "UsableWidthMm";
```

### 6.2 EF Core entities

```csharp
[Table("ReceiptProfiles")]
public class ReceiptProfile
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "jsonb"), Required]
    public string JsonSchema { get; set; } = "{}";

    // Array column — pgvector / Npgsql maps to text[]
    [Column(TypeName = "text[]")]
    public string[] PromotedFacets { get; set; } = Array.Empty<string>();

    [Column(TypeName = "jsonb")]
    public string DefaultAttributes { get; set; } = "{}";

    [Column(TypeName = "jsonb")]
    public string UiFormSpec { get; set; } = "{}";

    [Column(TypeName = "integer[]")]
    public int[] RegulatoryProfileIds { get; set; } = Array.Empty<int>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [StringLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [StringLength(100)] public string? ModifiedBy { get; set; }
}

[Table("StockReceipts")]
public class StockReceipt
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public int ItemId { get; set; }
    public Item? Item { get; set; }

    public int? MaterialMasterId { get; set; }
    public MaterialMaster? MaterialMaster { get; set; }

    // NEW — profile selector
    public int ProfileId { get; set; }
    public ReceiptProfile? Profile { get; set; }

    // Promoted universal trace fields
    [StringLength(128)] public string? LotNumber { get; set; }
    [StringLength(128)] public string? SerialNumber { get; set; }

    // Source / receiving / location
    [StringLength(64)] public string? SourcePoNumber { get; set; }
    [StringLength(64)] public string? SourcePoLineId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public int? ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }

    // Quantity
    [Column(TypeName = "decimal(18,4)")] public decimal QuantityReceived { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal QuantityRemaining { get; set; }
    [StringLength(16)] public string? Uom { get; set; }

    // Status
    public StockReceiptStatus Status { get; set; } = StockReceiptStatus.Available;
    [StringLength(500)] public string? QuarantineReason { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }

    // NEW — profile-defined extension payload
    [Column(TypeName = "jsonb")]
    public string Attributes { get; set; } = "{}";

    // Audit / concurrency
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [StringLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [StringLength(100)] public string? ModifiedBy { get; set; }
    [Timestamp] public byte[]? RowVersion { get; set; }
}
```

### 6.3 Service signature evolution

```csharp
public sealed record CreateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    int ProfileId,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    string? Uom,
    StockReceiptStatus Status,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);

public sealed record UpdateStockReceiptRequest(
    string ReceiptNumber,
    int ItemId,
    int? MaterialMasterId,
    int ProfileId,
    string? LotNumber,
    string? SerialNumber,
    string? SourcePoNumber,
    string? SourcePoLineId,
    DateTime ReceivedAt,
    int? ReceivedByUserId,
    int? LocationId,
    decimal QuantityReceived,
    decimal QuantityRemaining,
    string? Uom,
    string? Notes,
    IReadOnlyDictionary<string, object?> Attributes);
```

Validation in `StockReceiptService`:

```csharp
private async Task<Result> ValidateAttributesAsync(
    int profileId,
    IReadOnlyDictionary<string, object?> attrs,
    CancellationToken ct)
{
    var profile = await _db.ReceiptProfiles.FindAsync(new object?[] { profileId }, ct);
    if (profile is null) return Result.Failure("Invalid profile id");

    var schema = JsonSchema.FromText(profile.JsonSchema);
    var doc = JsonSerializer.SerializeToDocument(attrs);
    var result = schema.Evaluate(doc, new EvaluationOptions { OutputFormat = OutputFormat.List });
    if (!result.IsValid)
    {
        var errors = string.Join("; ", result.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(kv => $"{d.InstanceLocation}: {kv.Value}")));
        return Result.Failure($"Attribute validation failed: {errors}");
    }
    return Result.Success();
}
```

---

## Section 7: Industry profile catalog — the starter pack

Ship at least these 11 profiles seeded on every install. Each is a `ReceiptProfile` row with a JSON Schema + UI form spec + promoted facets.

### 7.1 STEEL (current default, no behavior change for existing users)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["heatNumber"],
  "properties": {
    "heatNumber":      { "type": "string", "maxLength": 64 },
    "mill":            { "type": "string", "maxLength": 128 },
    "millCertUrl":     { "type": "string", "format": "uri", "maxLength": 500 },
    "astmDesignation": { "type": "string", "maxLength": 64 },
    "countryOfMelt":   { "type": "string", "maxLength": 2 },
    "lengthMm":        { "type": "number", "minimum": 0 },
    "widthMm":         { "type": "number", "minimum": 0 },
    "thicknessMm":     { "type": "number", "minimum": 0 },
    "usableLengthMm":  { "type": "number", "minimum": 0 },
    "usableWidthMm":   { "type": "number", "minimum": 0 }
  }
}
```
Promoted facets: `heatNumber`, `mill`, `astmDesignation`.

### 7.2 PHARMA

```json
{
  "type": "object",
  "required": ["lotNumber", "expirationDate", "ndc"],
  "properties": {
    "ndc":                 { "type": "string", "pattern": "^[0-9]{4,5}-[0-9]{3,4}-[0-9]{1,2}$" },
    "gtin":                { "type": "string", "pattern": "^[0-9]{14}$" },
    "lotNumber":           { "type": "string", "maxLength": 64 },
    "serialNumber":        { "type": "string", "maxLength": 64 },
    "expirationDate":      { "type": "string", "format": "date" },
    "manufactureDate":     { "type": "string", "format": "date" },
    "deaSchedule":         { "type": "string", "enum": ["I","II","III","IV","V",""] },
    "countryOfOrigin":     { "type": "string", "maxLength": 2 },
    "epcisPedigreeUrl":    { "type": "string", "format": "uri" },
    "tempExcursionLogUrl": { "type": "string", "format": "uri" }
  }
}
```
Promoted facets: `lotNumber` (universal), `serialNumber` (universal), `(attributes->>'expirationDate')::date`, `ndc`.

### 7.3 FOOD

```json
{
  "type": "object",
  "required": ["traceabilityLotCode", "tlcSource"],
  "properties": {
    "traceabilityLotCode":      { "type": "string", "maxLength": 64 },
    "tlcSource":                { "type": "string", "maxLength": 200 },
    "tlcSourceReference":       { "type": "string", "maxLength": 500 },
    "bestByDate":               { "type": "string", "format": "date" },
    "harvestDate":              { "type": "string", "format": "date" },
    "packDate":                 { "type": "string", "format": "date" },
    "allergens":                { "type": "array", "items": { "type": "string" } },
    "organicCertNumber":        { "type": "string", "maxLength": 64 },
    "gfsiScheme":               { "type": "string", "enum": ["SQF","BRCGS","FSSC22000","IFS","none"] },
    "gfsiCertNumber":           { "type": "string" },
    "countryOfOrigin":          { "type": "string", "maxLength": 2 },
    "coaUrl":                   { "type": "string", "format": "uri" }
  }
}
```
Promoted facets: `traceabilityLotCode`, `bestByDate`, `coaUrl`.

### 7.4 CHEMICAL

```json
{
  "type": "object",
  "required": ["casNumber", "lotNumber"],
  "properties": {
    "casNumber":     { "type": "string", "pattern": "^[0-9]{1,7}-[0-9]{2}-[0-9]$" },
    "unNumber":      { "type": "string", "pattern": "^UN[0-9]{4}$" },
    "hazardClass":   { "type": "string" },
    "packingGroup":  { "type": "string", "enum": ["I","II","III",""] },
    "grade":         { "type": "string" },
    "purity":        { "type": "number", "minimum": 0, "maximum": 100 },
    "lotNumber":     { "type": "string" },
    "manufactureDate":{ "type": "string", "format": "date" },
    "shelfLifeDate": { "type": "string", "format": "date" },
    "sdsRevision":   { "type": "string" },
    "sdsUrl":        { "type": "string", "format": "uri" },
    "containerType": { "type": "string", "enum": ["drum","ibc","tote","cylinder","pail","bottle","bag","other"] },
    "containerSizeLiters": { "type": "number" }
  }
}
```
Promoted facets: `casNumber`, `unNumber`, `lotNumber`.

### 7.5 ELECTRONICS

```json
{
  "type": "object",
  "required": ["mpn", "manufacturerLot", "dateCode"],
  "properties": {
    "mpn":                  { "type": "string" },
    "manufacturer":         { "type": "string" },
    "manufacturerLot":      { "type": "string" },
    "dateCode":             { "type": "string", "pattern": "^[0-9]{4}$" },
    "mslLevel":             { "type": "number", "enum": [1,2,2.5,3,4,5,5.5,6] },
    "bagSealedAt":          { "type": "string", "format": "date-time" },
    "bagOpenedAt":          { "type": "string", "format": "date-time" },
    "rohsCompliant":        { "type": "boolean" },
    "reachSvhcDeclared":    { "type": "boolean" },
    "conflictMineralsRev":  { "type": "string" },
    "esdClass":             { "type": "string" },
    "reelId":               { "type": "string" },
    "countryOfOrigin":      { "type": "string", "maxLength": 2 }
  }
}
```
Promoted facets: `mpn`, `manufacturerLot`, `dateCode`, `(attributes->>'bagOpenedAt')::timestamptz`.

### 7.6 MEDICAL_DEVICE

```json
{
  "type": "object",
  "required": ["basicUdiDi", "udiDi"],
  "properties": {
    "basicUdiDi":         { "type": "string" },
    "udiDi":              { "type": "string" },
    "lotNumber":          { "type": "string" },
    "serialNumber":       { "type": "string" },
    "manufactureDate":    { "type": "string", "format": "date" },
    "expirationDate":     { "type": "string", "format": "date" },
    "sterilizationBatch": { "type": "string" },
    "softwareVersion":    { "type": "string" },
    "deviceClass":        { "type": "string", "enum": ["I","Is","Im","IIa","IIb","III"] },
    "implantable":        { "type": "boolean" },
    "singleUse":          { "type": "boolean" }
  }
}
```
Promoted facets: `udiDi`, `(attributes->>'expirationDate')::date`, `sterilizationBatch`.

### 7.7 AEROSPACE

Same as STEEL but with stricter required fields:

```json
{
  "type": "object",
  "required": ["heatNumber", "millCertUrl", "amsSpec"],
  "properties": {
    "heatNumber":     { "type": "string" },
    "mill":           { "type": "string" },
    "millCertUrl":    { "type": "string", "format": "uri" },
    "amsSpec":        { "type": "string" },
    "astmDesignation":{ "type": "string" },
    "countryOfMelt":  { "type": "string", "maxLength": 2 },
    "dfarsCompliant": { "type": "boolean" },
    "pyrometryChartUrl": { "type": "string", "format": "uri" },
    "tensileLotResult":  { "type": "string" }
  }
}
```
Promoted facets: `heatNumber`, `amsSpec`, `countryOfMelt`.

### 7.8 CANNABIS

```json
{
  "type": "object",
  "required": ["metrcTag", "sourceLicenseNumber"],
  "properties": {
    "metrcTag":             { "type": "string", "pattern": "^[0-9A-Z]{24}$" },
    "sourceHarvestTag":     { "type": "string" },
    "strain":               { "type": "string" },
    "cultivar":             { "type": "string" },
    "sourceLicenseNumber":  { "type": "string" },
    "coaUrl":               { "type": "string", "format": "uri" },
    "coaPassed":            { "type": "boolean" },
    "thcPercent":           { "type": "number" },
    "cbdPercent":           { "type": "number" },
    "harvestDate":          { "type": "string", "format": "date" },
    "cureDate":             { "type": "string", "format": "date" },
    "cultivationType":      { "type": "string", "enum": ["indoor","outdoor","mixed-light","greenhouse"] }
  }
}
```
Promoted facets: `metrcTag`, `sourceLicenseNumber`.

### 7.9 AUTOMOTIVE

```json
{
  "type": "object",
  "required": ["supplierCode", "partNumber", "dateCode", "ppapLevel"],
  "properties": {
    "supplierCode":     { "type": "string" },
    "supplierDuns":     { "type": "string", "pattern": "^[0-9]{9}$" },
    "partNumber":       { "type": "string" },
    "changeLevel":      { "type": "string" },
    "dateCode":         { "type": "string" },
    "lotNumber":        { "type": "string" },
    "ppapLevel":        { "type": "number", "enum": [1,2,3,4,5] },
    "pswStatus":        { "type": "string", "enum": ["full","interim","rejected"] },
    "imdsId":           { "type": "string" },
    "asnReference":     { "type": "string" },
    "criticalCharacteristic": { "type": "boolean" }
  }
}
```
Promoted facets: `supplierCode`, `partNumber`, `dateCode`, `ppapLevel`.

### 7.10 APPAREL

```json
{
  "type": "object",
  "required": ["rollDyeLot"],
  "properties": {
    "rollDyeLot":        { "type": "string" },
    "color":             { "type": "string" },
    "size":              { "type": "string" },
    "fiberComposition":  { "type": "string" },
    "widthCm":           { "type": "number" },
    "rollLengthM":       { "type": "number" },
    "gsm":               { "type": "number" },
    "htsClassification": { "type": "string" },
    "countryOfOrigin":   { "type": "string", "maxLength": 2 },
    "gotsCertNumber":    { "type": "string" },
    "oekoTexCertNumber": { "type": "string" }
  }
}
```
Promoted facets: `rollDyeLot`, `countryOfOrigin`.

### 7.11 CONSTRUCTION

```json
{
  "type": "object",
  "required": ["batchNumber", "manufactureDate"],
  "properties": {
    "batchNumber":     { "type": "string" },
    "plantCode":       { "type": "string" },
    "manufactureDate": { "type": "string", "format": "date" },
    "cureByDate":      { "type": "string", "format": "date" },
    "specCompliance":  { "type": "string" },
    "mixDesignId":     { "type": "string" },
    "vocContent":      { "type": "number" },
    "slumpInches":     { "type": "number" },
    "designStrengthPsi": { "type": "number" }
  }
}
```
Promoted facets: `batchNumber`, `manufactureDate`.

### 7.12 OIL_GAS (bonus 12th — high-value vertical)

```json
{
  "type": "object",
  "required": ["heatNumber", "apiSpec", "apiGrade"],
  "properties": {
    "heatNumber":         { "type": "string" },
    "mill":               { "type": "string" },
    "millCertUrl":        { "type": "string", "format": "uri" },
    "apiSpec":            { "type": "string" },
    "apiGrade":           { "type": "string" },
    "jointSerialNumber":  { "type": "string" },
    "pressureRatingPsi":  { "type": "number" },
    "scheduleNumber":     { "type": "string" },
    "wallThicknessIn":    { "type": "number" },
    "lengthFt":           { "type": "number" },
    "sourSvc":            { "type": "boolean" },
    "hydrotestPressurePsi": { "type": "number" }
  }
}
```
Promoted facets: `heatNumber`, `apiSpec`, `apiGrade`, `jointSerialNumber`.

---

## Closing notes

This proposal is **the smallest schema change that buys multi-industry coverage indefinitely**. It preserves every property the current StockReceipt has, adds a small number of universal promoted columns, and pushes everything else into a configurable jsonb attributes column governed by a profile catalog.

It also threads cleanly into the platform's other in-flight bets:
- **Voice-AI co-pilot (Sprint 5):** profiles + JSON Schema = prompt context the LLM uses to build queries.
- **Item Master Expansion (Sprint 7):** `Item.DefaultReceiptProfileId` auto-selects the right form.
- **Multi-Dim Inventory (Sprint 8):** universal lot/serial promotion aligns with D365 F&O's tracking-dimension model; industry-specific lot characteristics live as attributes.
- **ADR-014 voice-ready foundation:** `IStockReceiptService` evolution stays inside the existing `Result<T>` + `IIdempotencyMediator` + `AuditLog` envelope; only the request DTOs change.

Recommended next step: write **ADR-015 "Industry-Agnostic Receipt Schema"** that ratifies the recommendation here, then schedule a one-PR migration before the receipts table grows beyond ~10k rows in prod.

---

## Sources

ERP / inventory frameworks:
- [SAP Batch Management Overview - Surety Systems](https://www.suretysystems.com/insights/sap-batch-management-overview-key-features-and-capabilities/)
- [Batch Determination in SAP MM - SAP Community](https://community.sap.com/t5/enterprise-resource-planning-blog-posts-by-members/batch-determination-in-sap-mm/ba-p/13938950)
- [Classification with Standard Characteristics - SAP MM](https://sapmm.wordpress.com/2013/02/04/classification-with-standard-characteristics/)
- [D365 F&O Storage and Tracking Dimensions](https://www.linkedin.com/pulse/storage-tracking-dimension-dynamics-365-fo-functional-syed-amir-ali-pbdfc)
- [Add new inventory dimensions through extension - Microsoft Learn](https://learn.microsoft.com/en-us/dynamics365/fin-ops-core/dev-itpro/extensibility/inventory-dimensions)
- [What are Inventory Dimensions in Dynamics 365 SCM - Stoneridge](https://stoneridgesoftware.com/what-are-inventory-dimensions-in-dynamics-365-supply-chain/)
- [Configure Lot and Serial Descriptive Flexfields - Oracle](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25d/faims/configure-lot-and-serial-descriptive-flexfields.html)
- [Extensible Flexfields in Oracle Fusion - Apps2Fusion](https://apps2fusion.com/old/oracle-fusion-online-training/165-oracle-articles/2513-extensible-flexfields-in-product-maagement)
- [NetSuite Item Receipt - Oracle Docs](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N3195956.html)
- [NetSuite Creating Custom Item Fields](https://docs.oracle.com/en/cloud/saas/netsuite/ns-online-help/section_N2827818.html)
- [Plex Quality Management Capabilities - ERP Research](https://www.erpresearch.com/erp/plex/quality-management)
- [Aptean Food Traceability](https://www.aptean.com/en-US/industries/food-and-beverage/capability/traceability)
- [Veeva vs TrackWise vs MasterControl - IntuitionLabs](https://intuitionlabs.ai/articles/veeva-vs-trackwise-vs-mastercontrol-pharma-qms)

Regulatory standards:
- [DSCSA Serialization Requirements 2026 - ColdChainCheck](https://coldchaincheck.com/guides/dscsa-serialization-requirements-wholesale-distributors)
- [DSCSA - GS1 US](https://www.supplychain.gs1us.org/standards-and-regulations/drug-supply-chain-security-act)
- [FDA DSCSA Product Tracing Requirements FAQ](https://www.fda.gov/drugs/drug-supply-chain-security-act-dscsa/drug-supply-chain-security-act-product-tracing-requirements-frequently-asked-questions)
- [FSMA Section 204 Final Rule - FDA](https://www.fda.gov/food/food-safety-modernization-act-fsma/fsma-final-rule-requirements-additional-traceability-records-certain-foods)
- [What You Need to Know About FSMA 204 - Trustwell](https://www.trustwell.com/resources/fsma-204-the-food-traceability-final-rule/)
- [EUDAMED UDI Devices User Guide](https://webgate.ec.europa.eu/eudamed-help/en/files/UDI%20Devices%20-%20user%20guide.pdf)
- [UDI Basics - FDA](https://www.fda.gov/medical-devices/unique-device-identification-system-udi-system/udi-basics)
- [METRC Plant and Package Tagging Best Practices](https://www.metrc.com/wp-content/uploads/2024/03/Plant-TaggingV3-1.pdf)
- [METRC Track-and-Trace Platform](https://www.metrc.com/track-and-trace-technology/)
- [IPC/JEDEC J-STD-033](https://www.navsea.navy.mil/Portals/103/Documents/NSWC_Crane/SD-18/Test%20Methods/jstd033.pdf)
- [Moisture Sensitivity Levels - Microchip](https://www.microchip.com/content/dam/mchp/documents/quality---reliability/MSL-Communication.pdf)
- [IATF 16949 Identification and Traceability](https://preteshbiswas.com/2023/08/01/iatf-169492016-clause-8-5-2-1-identification-and-traceability/)
- [GS1 Application Identifiers](https://ref.gs1.org/ai/)
- [GS1-128 Barcode - GS1 US](https://www.gs1us.org/upcs-barcodes-prefixes/gs1-128)
- [REACH Safety Data Sheets - HSENI](https://www.hseni.gov.uk/reach-safety-data-sheets)
- [ERP for Chemical Manufacturers - AppIT](https://www.appitsoftware.com/blog/erp-chemical-manufacturers-sds-hazmat-tracking)

Postgres performance:
- [PostgreSQL JSONB vs EAV - Raz Samuel](https://www.razsamuel.com/postgresql-jsonb-vs-eav-dynamic-data/)
- [PostgreSQL JSONB Performance Guide - SitePoint](https://www.sitepoint.com/postgresql-jsonb-query-performance-indexing/)
- [Understanding Postgres GIN Indexes - pganalyze](https://pganalyze.com/blog/gin-index)
- [Pitfalls of JSONB indexes in PostgreSQL - Vsevolod Solovyov](https://vsevolod.net/postgresql-jsonb-index/)
- [PostgreSQL JSONB Performance Best Practices - Elysiate](https://www.elysiate.com/blog/postgresql-jsonb-performance-best-practices)
