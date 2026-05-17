# ADR-013 — Production Order Architecture for Multi-Vertical Manufacturing

**Status:** Proposed — awaiting Dean sign-off
**Date:** 2026-05-17
**Author:** Architecture
**Supersedes:** N/A
**Builds on:** ADR-012 v0.2 (Unified WorkOrder + classification satellites)

---

## Question

Production looks radically different across verticals — machine shops, equipment / capital-goods OEMs, repetitive discrete, process / batch (chemical, food, pharma), engineer-to-order. Outside processes (heat treat, plating, laser cut), cut lists, nesting plans, recipes, takt time, lot genealogy — these vary completely.

How do we model Production Orders so the platform can serve all of them on a single backbone without becoming SAP-grade complex, and without ossifying into vertical-specific forks?

---

## State of practice (May 2026)

Production looks more different across verticals than any other module in manufacturing software — more than maintenance, more than quality. Every serious vendor has wrestled with this, and there is a clear, almost boring pattern in how the winners landed.

**SAP S/4HANA — two adjacent worlds on a shared substrate.** PP (discrete) and PP-PI (process) are separate transactions (CO01 vs COR1) and separate functional modules with their own user-facing concepts: discrete uses Routings + BOMs and produces *Production Orders*; PP-PI uses Master Recipes + phases on Resources and produces *Process Orders* with co-products, by-products, and control recipes for DCS handoff. Underneath, though, the database is *largely shared*: AUFK is the universal order header (used for production orders, process orders, maintenance orders, internal orders, networks), AFKO holds scheduling and assignment to routing/recipe, and AFPO holds the principal-material + co-product positions. A field on AUFK (AUART = order type) discriminates, and order **category** (10 = production, 30 = network, 40 = process, etc.) is what actually drives behavior. So SAP's pattern is: **one universal "Order" backbone, a discriminator (category + type), and per-category specialization on top.** ([SAP4Tech AUFK/AFKO/AFPO](https://sap4tech.net/sap-production-order-tables/), [SAP help — Process Orders](https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/21aead0c98bd4755abdacd91c99e3393/ef72be532789b44ce10000000a174cb4.html), [PortSAP — PP discrete/repetitive/process](https://portsapblogging.com/2024/06/04/sap-pp-discrete-vs-repetitive-vs-process-industry-how-to-choose-and-run-the-right-one/))

**Oracle Cloud Manufacturing — unified by design.** The cleanest example of a single model. `WIS_WORK_DEFINITIONS` and `WIE_WORK_ORDERS` hold *both* discrete and process orders. A column called **`WORK_METHOD`** is the type discriminator (values: `DISCRETE_MANUFACTURING`, `PROCESS_MANUFACTURING`). Operations, resources, items, and item-output rules all live in the same tables; what changes is how component-quantity validation works (discrete = strict 1:1 with item structure; process = flexible, since you can scale a recipe). Oracle effectively bet that one wide table + one discriminator was enough. ([Oracle docs — How You Create Work Definitions](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25a/faumf/how-you-create-work-definitions.html), [Oracle WIS_WORK_DEFINITIONS](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/24d/oedsc/wisworkdefinitions-6341.html))

**Plex (Rockwell), Aptean, IQMS — pre-configured vertical SKUs on the same platform.** Plex markets pre-configured packages for Discrete, CPG, Aerospace/Defense, Food & Beverage, and Auto. Under the hood it's the same Plex Manufacturing Cloud schema with industry-specific config bundles (lot/serial defaults, quality rules, regulatory fields, BOM-vs-formula UI). Named a Leader in the IDC MarketScape MES 2024–2025 partly *because* of these vertical configs. ([Plex MES](https://plex.rockwellautomation.com/en-us/products/manufacturing-execution-system.html), [Plex IDC MarketScape 2024–2025](https://plex.rockwellautomation.com/en-us/company/newsroom/rockwell-automation-named-leader-2024-2025-idc-marketscape-manufacturing-execution.html))

**DELMIA Apriso — unified BPM substrate, per-process composition.** Apriso's pitch is literally "manufacturing-aware BPM with a unified data model" — production, quality, warehouse all sit on one schema, and Process Builder composes operation flows per industry. Same pattern: one model, configuration on top. ([DELMIA Apriso 2025 docs](https://www.3ds.com/support/documentation/delmia-apriso-2025-documentation))

**Composable MES movement (Tulip, Factbird, FactoryFour, Fulcrum, 2024–2026) — the direction.** Tulip uses a **Common Data Model** with a single Work Orders table that every app — discrete, pharma, R&D, logistics — reads from; vertical behavior is composed via no-code apps, not via separate tables. **Gartner's May 2025 MES Market Guide explicitly named composability, low-code extensibility, and a shared data model as the dominant 2025 trend.** Fulcrum, the modern job-shop ERP, does the same thing: one Job/WorkOrder backbone, with cut-lists and DXF-driven nest planning attached as related objects on the *item*, not as a fork in the order schema. ([Tulip Common Data Model](https://support.tulip.co/docs/common-data-model-for-discrete), [Gartner 2025 MES Market Guide via Tulip](https://tulip.co/press/tulip-recognized-in-2025-gartner-market-guide-for-manufacturing-execution-systems/), [Fulcrum sheet-metal nesting](https://fulcrumpro.com/article/fulcrum-for-fabricators-workflows-for-custom-sheet-metal-shops))

**ISA-95 / IEC 62264 / B2MML — the standard already prescribes this.** The standard's foundational object is a **Production Segment** (a unit of work — same primitive for any industry) tied to abstract Material, Equipment, and Personnel models. A `JobOrder` is one work request against a segment; recipes, routes, and discrete steps are all expressed as compositions of segments. The standard *deliberately does not split* discrete vs process at the model level — it splits them at the behavior level. ([ISA-95 standard](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard), [OPC ISA-95 Job Control](https://reference.opcfoundation.org/ISA95JOBCONTROL/v200/docs/4.2))

### Narrow features — where every vendor agrees

- **Cut-list / nesting:** Fulcrum, JETCAM, Lantek, AlmaCAM all treat the **Nest** as a *first-class entity that lives between the item and the work order* — a nest aggregates parts (often across orders) onto a sheet, and shop-floor operators see the nest, not the individual parts. Cost is allocated proportionally back to each WO. Cut-lists are part-line children of the item, not the WO.
- **Outside processing:** SAP distinguishes **External Operation (PP02)** = an operation within a normal production order is marked external, auto-generates a purchase requisition, the order continues; vs **Subcontracting** = the entire output is purchased with component supply. Best-in-class systems model outside-process as a flag on the *Operation*, not a different WorkOrder type.
- **Recipe vs BOM in hybrid (e.g., packaged food, cosmetics):** Datacor and BatchMaster's pattern, now mainstream, is **two linked structures**: a Formula/Recipe for the bulk stage and a packaging BOM for the discrete stage, joined by a "bulk material" intermediate item. One order, two structure links.
- **Lot/serial split for regulated industries (FDA 21 CFR 820/821, AS9100 aerospace):** Regulators require *traceability* and *Device History Record* — they do **not** require separate tables. Both FDA and aerospace shops run on the same lot/serial schema as everyone else; the difference is which fields are *mandatory*, which approvals are gated, and what audit trail is retained. **Enable-via-flag, not fork-the-schema.**

### Where the best-in-class diverge — and why

SAP optimizes for *regulatory defensibility*: separate transactions for process orders mean an FDA or REACH auditor sees a different validated codepath. Oracle optimizes for *implementation cost*: one model, one set of training, one consultant pool. Plex optimizes for *time-to-value*: pre-configured industry SKUs let a food plant go live in 90 days. Tulip optimizes for *flexibility-at-the-edge*: composable apps mean every customer's "production order UI" is different even though the table is the same. **The composable + ISA-95 camp has clear momentum in 2025–2026 Gartner commentary.**

---

## Options

### A. Config-driven single model
WorkOrder + ProductionType discriminator + per-type satellite (same pattern as ADR-012 v0.2's CIP/Quality/Engineering/HSE).

### B. Sibling tables per vertical
`DiscreteProductionOrder`, `ProcessProductionOrder`, `ProjectProductionOrder`...

### C. Hybrid (recommended)
Single header (Option A pattern) **except** for the one structural exception — *Material Structure* (BOM vs Recipe) — modeled as a polymorphic pair because the columns truly differ.

---

## Trade-off table

| Dimension | **A. Config-driven** | **B. Sibling tables** | **C. Hybrid (recommended)** |
|---|---|---|---|
| Schema surface area | Smallest. 1 header + N small satellites. | Largest. Duplicated headers, duplicated FKs. | Medium. 1 header + 2 structure subtypes + ~5 satellites. |
| Reporting / dashboards | Trivial — one query. | Painful — UNION across N tables. | Easy — same as A for header queries. |
| Adding a new vertical (e.g., pharma) | New satellite + new enum value. ~1 PR. | New table + new repository + service + UI module. ~6 PRs. | New satellite + maybe new structure subtype. ~2 PRs. |
| Regulatory defensibility (FDA, aerospace) | Auditor sees one table with conditional rules — needs strong audit trail. | Auditor sees a dedicated table — easiest to defend. | Strong: dedicated *satellite* for GMP is per-record evidence. |
| Performance at scale | Sparse columns risk on header; fine if satellites are real tables. | Each table stays narrow; best raw perf. | Same as A. |
| Domain-language fidelity | Process people grumble "WorkOrder" isn't a "Batch" — fixable in UI. | Each vertical sees its own noun. | Same as A; "Production Order" already reads natively in both worlds. |
| Risk of getting it wrong | Low — easy to migrate to B later. | High — sibling tables ossify. | Low. |
| Time to ship a working CIP machine shop | Days. | Weeks. | Days. |
| What ISA-95 prescribes | Matches. | Diverges. | Matches. |
| What Oracle / Tulip / Apriso / Fulcrum / DELMIA do | This. | Nobody modern. SAP partially, historical. | This (modern SAP-on-S/4 extensions, ETO add-ons). |

---

## Recommendation

**Adopt Option C — Hybrid, biased toward A.** Concretely:

1. **One `ProductionOrder` header** sitting alongside `WorkOrder` (sibling, per ADR-012 v0.2 — production has different status machine, OEE concerns, event cadence). One `ProductionType` discriminator enum: `JobShop`, `RepetitiveDiscrete`, `CapitalETO`, `ProcessBatch`, `Hybrid`. This is your `work_method` — borrowed directly from Oracle Fusion's proven model.

2. **Per-type satellites** (1:0..1 with the header), exactly like the CIP/Quality/Engineering/HSE pattern from ADR-012 Phase D. Start with two: `ProductionJobShopDetail` (cut-list ref, nest plan ref, outside-process flags) and `ProductionProcessDetail` (recipe ref, batch ID, co/by-products). Add `ProductionETODetail` and `ProductionRepetitiveDetail` later as needed — same shape, same migration pattern, no architectural change.

3. **The one structural exception: Material Structure.** A BOM and a Recipe are *different enough* that cramming them into one table creates the same NULL-column hell that bit SAP in the 90s. Model as a polymorphic `MaterialStructure` with two concrete subtypes (`Bom`, `Recipe`), both pointing at a shared `MaterialStructureLine` with a `LineKind` enum (component, co-product, by-product, scrap, packaging). `ProductionOrder` has a single `MaterialStructureId` FK. This is exactly the Datacor / BatchMaster hybrid pattern.

4. **Nests and cut-lists as first-class entities, not WO subtypes.** `Nest` and `CutListLine` live as their own tables, FK from the part/item, and a join table `NestWorkOrderAllocation` proportionally cost-allocates back to whichever production orders are consuming the nest. Fulcrum / JETCAM pattern.

5. **Outside processing as a flag on `Operation`, not a new order type.** `Operation.IsExternal`, `Operation.VendorId`, `Operation.AutoGeneratePR` — auto-creates a purchase requisition when the WO releases. SAP PP02 pattern.

6. **Lot/serial regulation as policy, not schema.** One `Lot` table, one `Serial` table (already in your model). Per-type validation rules and audit-retention policies live in a `RegulatoryProfile` config — FDA mode, AS9100 mode, REACH mode toggle different gates, same schema. Plex pattern.

### Why this is the right call

It is the architecture the standard (ISA-95) prescribes, that Oracle/Tulip/Apriso/Fulcrum/DELMIA have all converged on, that the May 2025 Gartner MES Market Guide identifies as the dominant 2025–2026 direction, and that mirrors the pattern you *already shipped* in ADR-012 v0.2 — so onboarding new contributors and new verticals doesn't require a new mental model.

### Why not pure A

BOM vs Recipe is genuinely different — co-products, scaling rules, by-products, phase timing — and forcing a `MaterialStructureLine.Type` to express all of it makes the table 40 columns wide with 60% nulls. The polymorphic structure pair earns its complexity.

### Why not B

You'd be the only modern vendor doing it, you'd own the integration tax forever, and you'd violate the very ADR-012 pattern that's already working for WorkOrder classification.

---

## Phase E ship plan (if Option C is approved)

- **PR #119.12 (E.1)** — `ProductionOrder` table inheriting from `WorkOrder` pattern with `ProductionType` enum + `ProductionJobShopDetail` satellite + `Operation.IsExternal/VendorId` columns. Lands a working CIP machine shop end-to-end.

- **PR #119.13 (E.2)** — `MaterialStructure` polymorphic parent with `Bom` subtype + `Nest`, `CutListLine`, `NestWorkOrderAllocation` entities. DXF-driven nest planning wired into the JobShop satellite (Fulcrum parity).

- **PR #119.14 (E.3)** — `Recipe` subtype + `ProductionProcessDetail` satellite (batch ID, co/by-products, phase timing) + `RegulatoryProfile` config table. Unlocks process / batch verticals (food, chem, pharma) and FDA / AS9100 mode without touching `ProductionOrder` itself.

Phase E becomes 3 PRs, not 6. Phase F (renderer + field-visibility + seed-data sweep) still applies on top — unchanged from the existing plan.

---

## Decisions still open (for Dean)

1. **Approve Option C?** (Default recommendation.)
2. **Phase E ordering** — ship JobShop first (machine shops, equipment OEMs), then Nest/CutList, then Process/Recipe? Or reorder if a specific design partner needs Process first?
3. **`Hybrid` value in `ProductionType` enum** — do we need it, or is it covered by JobShop + linked Process bulk-stage? Defer until first hybrid customer.
4. **Recipe vs Formula naming** — both terms are used in industry. ISA-95 uses "Master Recipe." Bias to "Recipe" unless food/pharma design partner pushes back.

---

## References

- [SAP Help — Process Orders (PP-PI-POR)](https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/21aead0c98bd4755abdacd91c99e3393/ef72be532789b44ce10000000a174cb4.html)
- [PortSAP — SAP PP discrete vs repetitive vs process (2024)](https://portsapblogging.com/2024/06/04/sap-pp-discrete-vs-repetitive-vs-process-industry-how-to-choose-and-run-the-right-one/)
- [SAP4Tech — Production Order Tables (AUFK/AFKO/AFPO)](https://sap4tech.net/sap-production-order-tables/)
- [SAP S/4HANA Learning — Outlining Production Types](https://learning.sap.com/learning-journeys/discovering-the-basics-of-sap-s-4hana-manufacturing/outlining-production-types_be2a8a49-fa57-4e47-94f4-f1f4d2a8b8f2)
- [SAP Community — Subcontracting in Production Planning (S/4HANA)](https://community.sap.com/t5/enterprise-resource-planning-blog-posts-by-members/subcontracting-process-in-production-planning-sap-s-4hana/ba-p/13578805)
- [Oracle Docs — How You Create Work Definitions](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25a/faumf/how-you-create-work-definitions.html)
- [Oracle Docs — WIS_WORK_DEFINITIONS table](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/24d/oedsc/wisworkdefinitions-6341.html)
- [Oracle Docs — Overview of Manufacturing Cloud (25D)](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25d/faumf/overview-of-oracle-manufacturing-cloud.html)
- [VM-Oracle — Discrete vs Process in Oracle Cloud Manufacturing](https://www.vm-oracle.com/clouderp/mfgcloud_discreteversusprocess/)
- [Plex (Rockwell) — Manufacturing Execution System](https://plex.rockwellautomation.com/en-us/products/manufacturing-execution-system.html)
- [Plex/Rockwell — IDC MarketScape MES 2024–2025 Leader](https://plex.rockwellautomation.com/en-us/company/newsroom/rockwell-automation-named-leader-2024-2025-idc-marketscape-manufacturing-execution.html)
- [DELMIA Apriso 2025 Documentation](https://www.3ds.com/support/documentation/delmia-apriso-2025-documentation)
- [Andea — DELMIA Apriso Process Builder](https://www.andea.com/resources/blog/process-builder-in-delmia-apriso-how-its-key-features-increase-your-control-of-manufacturing-processes-at-decreased-cost/)
- [Tulip — Composable MES Overview](https://support.tulip.co/docs/composable-mes-overview)
- [Tulip — Common Data Model for Discrete](https://support.tulip.co/docs/common-data-model-for-discrete)
- [Tulip — Recognized in 2025 Gartner MES Market Guide](https://tulip.co/press/tulip-recognized-in-2025-gartner-market-guide-for-manufacturing-execution-systems/)
- [Tulip — 2025 Smart Factory MES Guide](https://tulip.co/blog/2025-mes-vendor-comparison-guide/)
- [Fulcrum — Sheet Metal Fabrication ERP](https://fulcrumpro.com/article/fulcrum-for-fabricators-workflows-for-custom-sheet-metal-shops)
- [Fulcrum — CNC Machine Shop workflow](https://fulcrumpro.com/workflows/cnc-machine-shop)
- [JETCAM — Integrating Nesting Software with ERP](https://pages.jetcam.net/blog/integrating-sheet-metal-nesting-software-with-an-erp-system-unlocking-efficiency-and-saving-time)
- [ISA — ISA-95 / IEC 62264 Standard](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard)
- [OPC Foundation — ISA-95 Common Object Model](https://reference.opcfoundation.org/ISA-95/v100/docs/4)
- [OPC Foundation — ISA-95-4 Job Control Information Model](https://reference.opcfoundation.org/ISA95JOBCONTROL/v200/docs/4.2)
- [MESA — B2MML XML schemas](https://mesa.org/topics-resources/b2mml/)
- [Automation World — ISA-95 Production Models & Segments](https://www.automationworld.com/products/control/news/13301218/isa-95-implementation-best-practices-production-models-and-segments)
- [Datacor — Bill of Materials for Process Manufacturing](https://www.datacor.com/resources/bill-of-materials-process)
- [Tayana Solutions — Packaging BOM](https://www.tayanasolutions.com/packaging-bill-of-material-bom/)
- [FDA — UDI Basics](https://www.fda.gov/medical-devices/unique-device-identification-system-udi-system/udi-basics)
- [Tech-Clarity / Critical Manufacturing — MES Buyer's Guide 2024](https://www.criticalmanufacturing.com/wp-content/uploads/2024/05/Tech-Clarity-MES-Buyers-Guide-2024.pdf)
