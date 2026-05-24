# ADR-027 — Sales Order Lines + Releases Drive Production Orders (Not Headers)

**Status:** Accepted (architectural lock; entities ship in Sprint 19+)
**Date:** 2026-05-24
**Sprint:** 13.5 PRA-9 sibling (Master Files Baseline cascade ship #7 of 10)
**Author:** Dean Dunagan (architectural directive), Claude (drafting)
**Related:**
- ADR-013 (Production Order Architecture) — `ProductionOrder` entity that ADR-027 forward-references
- ADR-019 (WMS PostingProfile pattern) — line-level GL routing precedent
- `Models/Projects/CustomerProjects.cs` — existing CustomerProject entity (project-level, not line-level)
- `Models/PurchaseOrder.cs` — line-level shape this ADR mirrors on the demand side
- memory: `reference_dean_brainstorm_2026_05_24.md` (Theme B1)

---

## Context

The Master Files Baseline cascade lays down the masters that support both the **supply side** (Item / UOM / GL / Warehouse / Bin / Lot / Serial / Employee / WageGroup / Department) and the **revenue side** (Currency / PaymentTerm / Tax / PriceList / Discount / Rebate). What it deliberately defers — to Sprint 19 — is the actual **Sales Order** transactional schema. That deferral is intentional: SO is downstream of the masters, and the masters had to land first.

But before Sprint 19 starts writing code, one architectural decision needs to be locked: **does the Production Order trace back to the Sales Order header, or to a specific line / release on the Sales Order?**

This is a foundational shape decision. The wrong choice (header coupling) breaks the moment a customer order contains:

- More than one part number (a typical machining customer orders a kit of parts on one PO).
- Multiple delivery dates for the same part number (blanket orders / scheduling agreements / kanban replenishment contracts).
- Different price tiers or contract terms per line (one line at quoted MTO pricing, another line pulling from a standard PriceList).
- Different traceability requirements per line (one line is AS9102 FAI, another is standard production).

Dean's exact words in the 2026-05-24 brainstorm: *"For future if we decide to go full ERP, Make to order or Engineer to Order Production Orders will need to come from sales order lines/releases, not Sales Order header level."* This ADR is the structural commitment.

## Decision

The Sales Order schema, when it ships in Sprint 19+, **MUST** use a three-level hierarchy:

```
SalesOrder (header — contractual container)
  ├── SalesOrderLine (per-part-number, per-quoted-terms demand line)
  │     ├── SalesOrderRelease (per-delivery-date sub-line for blanket / scheduling-agreement orders)
  │     └── ProductionOrder.SalesOrderLineId FK (when MTO/ETO — demand traces back to the LINE)
  └── (header carries customer-side metadata — PO #, payment terms, ship-to, contractual T&Cs)
```

And **`ProductionOrder.SalesOrderHeaderId` (or any header-level coupling FK) MUST NOT exist.** Production demand always traces back to `SalesOrderLineId` (and, when the line has releases, also to `SalesOrderReleaseId`).

This decision applies forward — the entities don't exist yet, but when Sprint 19 implements them, this is the shape. PRA-9 (which ships today as the sibling to this ADR) lays the price-side substrate: `PriceListLine` carries the per-Item price granularity that `SalesOrderLine` will reference at order time.

### Required schema (Sprint 19 implementation)

`SalesOrder` (header):
- Standard tenant trio (`CompanyId`, audit fields).
- `SalesOrderNumber` (UNIQUE per CompanyId).
- `CustomerId` FK, `CustomerProjectId` FK (optional — links to existing project rollup).
- `CustomerPoNumber` (the customer's PO number).
- `OrderDate`, `RequiredDate`, `PromiseDate`.
- `PaymentTermId` FK (PRA-6), `CurrencyId` FK (PRA-6).
- `ShipToSiteId`, `BillToSiteId`.
- Contractual terms text + attachments.
- Header-level totals (sum of lines) for fast list views.

`SalesOrderLine` (per-part demand line):
- `SalesOrderId` FK (parent).
- `LineNumber` (UNIQUE within order).
- `ItemId` FK + `UomId` FK.
- `OrderedQuantity` + `UnitPrice` + `LineTotal`.
- `PriceListLineId` FK (optional — traces price provenance to the master).
- `RequestedShipDate`, `PromisedShipDate`.
- `LineType` enum (Stock | MakeToOrder | EngineerToOrder | Service | Subscription).
- `DiscountSchemaId` FK (when an applicable discount applied — historical record).
- `LineStatus` enum (Quoted | Booked | Released | InProduction | PartiallyShipped | Shipped | Cancelled | Closed).
- Per-line ship-to override (when one order ships to multiple addresses).
- Per-line tax code override.

`SalesOrderRelease` (per-delivery-date sub-line):
- `SalesOrderLineId` FK (parent).
- `ReleaseNumber` (UNIQUE within line).
- `ReleaseQuantity`.
- `ReleaseDate` (the specific delivery date).
- `ReleaseStatus` enum (Open | Released | Shipped | Cancelled).
- Optional `ProductionOrderId` FK back-pointer (cached for fast UI).

`ProductionOrder` (existing entity — net-add):
- `SalesOrderLineId int? nullable` FK (NULL for MTS / replenishment production; SET for MTO/ETO).
- `SalesOrderReleaseId int? nullable` FK (SET when the line uses release scheduling).

### Service-layer flow at order release time

```
1. Customer places order → SalesOrder + N SalesOrderLines created.
2. For each line with LineType in (MakeToOrder, EngineerToOrder):
     a. Resolve material structure (Bom/Recipe) for the Item.
     b. Resolve routing (RoutingMaster) for the Item.
     c. Create ProductionOrder with SalesOrderLineId = line.Id.
     d. If the line has releases, create one ProductionOrder per release
        (or one ProductionOrder with sub-batches per release, tenant config).
     e. PostingProfile resolution at fulfillment time keys on
        ProductionOrder → SalesOrderLineId → SalesOrder.PaymentTermId / etc.
3. For lines with LineType = Stock:
     a. Allocate from on-hand inventory (no ProductionOrder created).
     b. If short, trigger MRP replenishment (own SOP).
```

## Considered alternatives

### Alternative A — Header-level coupling (`ProductionOrder.SalesOrderHeaderId`) — **REJECTED**

The lazy pattern. One header → one ProductionOrder. Works for the demo case (one item, one delivery date) but breaks immediately for:

- Multi-line orders → can't split production across parts cleanly.
- Multi-release orders → can't schedule each release independently.
- Mixed line types → header can't be "both MTO and stock-allocated" simultaneously.
- Cost attribution → production costs collapse to the header, not the line that drove them.
- Reporting → "margin by SO line" becomes uncomputable.

This is the pattern that QuickBooks Enterprise and pre-S/4 SAP B1 implementations got stuck in. It's the "going backwards" Dean flagged in `user_dean`. Rejected.

### Alternative B — Line-level coupling without releases — **REJECTED**

Better than A — `ProductionOrder.SalesOrderLineId` exists, but no `SalesOrderRelease` entity. Works for plain MTO ("make 50, ship all 50 on one date") but breaks for:

- Blanket orders → customer says "ship me 50/month for 12 months" — needs 12 release dates with independent ship dates and production scheduling.
- Scheduling agreements (typical automotive) → release dates roll on a rolling-horizon basis (EDI 830 / 862 message types).
- Kanban replenishment contracts → release fires when consumption signal comes back from customer.

Without releases, every blanket order becomes 12 separate SalesOrder rows that share nothing. Reporting "remaining open against contract" is painful. Rejected.

### Alternative C — Four-level hierarchy (Order → Group → Line → Release) — **REJECTED**

SAP SD has this: Sales Document → Item Category → Item → Schedule Line. Oracle Cloud similarly has Header → Line Group → Line → Schedule Line. The Group/ItemCategory layer is for "bundle pricing" — buy a kit of 5 parts at $X total, where each part also has its own price.

This is genuinely useful for B2C bundle pricing + some B2B kit configurations, but it's overweight for the IndustryOS manufacturing-leaning multi-vertical target. The 3-level model (Header / Line / Release) covers 95% of MTO/ETO + blanket-order + kanban patterns without the extra layer's JOIN cost. Tenants that need bundle pricing can do it with a future `SalesOrderKit` companion table or by modeling the kit as a `MaterialStructure` / kit BOM and ordering the parent SKU.

Rejected as over-engineered for v1. Reserve the option to extend later.

### Alternative D (ACCEPTED) — Three-level: Header / Line / Release

Pros (the reasons it wins):

- **Familiar from every Tier-1 ERP** that customers come from — SAP SD `VBAK` / `VBAP` / `VBEP`, Oracle OM `OE_ORDER_HEADERS_ALL` / `OE_ORDER_LINES_ALL` / scheduling-lines view, Plex SO lines + ship schedules, NetSuite sales orders + delivery items.
- **Right granularity for production demand** — every line drives at most one ProductionOrder (or one per release for scheduled lines).
- **Right granularity for GL** — line-level revenue recognition, line-level COGS, line-level cost-rollup. Matches PostingProfile (PRA-7) resolution.
- **Right granularity for reporting** — "margin by line" is one JOIN. "Open against blanket" is one filter on releases.
- **Compatible with existing ProductionOrder + PurchaseOrder + PostingProfile substrate** — no rework needed.

Cons (acknowledged, mitigated):

- One more entity than Alternative B (SalesOrderRelease). Mitigated by making releases optional (`SalesOrderLine.UsesReleaseScheduling` flag; standard single-ship orders just have one implicit release or skip the table entirely).
- More JOINs in the cost-attribution query path. Mitigated by `ProductionOrder.SalesOrderLineId` (and optional `SalesOrderReleaseId`) being indexed FKs — query planner handles it cleanly.

## Consequences

### Positive

- **No header-coupling tech debt to undo later.** Sprint 19 starts from the right shape.
- **MRP planning targets correctly.** Demand signal is "this Item needs this Qty by this Date for this CustomerLine" — exactly what MRP needs.
- **Aerospace + medical compliance is line-traceable.** AS9100 / FDA traceability requires "what was this lot/serial produced for, and on whose authority" — the SalesOrderLine is the answer.
- **Multi-currency / multi-tax orders work cleanly.** A single SO header carries the currency + tax authority; each line can override (rare but valid for international consortia contracts).

### Negative / risks

- Schema is bigger than Alternative A or B. Net 3 new transactional tables + a couple of FK additions. Mitigated by the deferral to Sprint 19 — when SO is built, the rest of the masters are in place.
- Service layer at order-release time has more orchestration to do (resolve PriceListLine, resolve material structure, resolve routing, create N ProductionOrders). Mitigated by `OrderReleaseService` (Sprint 19) that owns the orchestration cleanly.

### Neutral

- `CustomerProject` (PR #2, `Models/Projects/CustomerProjects.cs`) semantics may shift slightly when Sprint 19 lands. Today, CustomerProject is the customer-side container with Program / Customer / Currency / Contract dates. When SalesOrder ships, CustomerProject becomes the PROGRAM-level rollup (one CustomerProject can span many SalesOrders over time — a multi-year program). The CustomerProject row stays; the semantic widens. No migration of existing rows needed.

## Validation

This ADR is a forward commitment. The Sprint 19 deliverable for SalesOrder ships will be validated against these gates:

1. `ProductionOrder.SalesOrderLineId` FK exists; `SalesOrderHeaderId` FK does NOT exist on ProductionOrder.
2. `SalesOrderRelease` entity exists with a `SalesOrderLineId` FK parent.
3. `SalesOrderLine` carries `PriceListLineId` FK (price provenance) and `DiscountSchemaId` FK (discount provenance) — auditable to the customer at invoice time.
4. `OrderReleaseService.CreateProductionOrderForLineAsync(SalesOrderLineId)` exists and is the only path that creates MTO/ETO ProductionOrders downstream of an order.
5. Existing `CustomerProject` rows survive the ship without data loss — the semantic widening is additive.

## Future work (out of scope for this ADR)

- `SalesOrderKit` companion (Alternative C feature) — deferred until bundle-pricing customer demand surfaces.
- EDI 850 (order in) / 855 (order ack) / 856 (ASN out) / 810 (invoice out) integration — Sprint 21 (MCP Server + Agentic AI Integrations).
- Multi-ship-to per line — `SalesOrderLineShipment` companion (Sprint 19+ stretch).
- Subscription / recurring billing lines (`SalesOrderLine.LineType = Subscription`) — Sprint 22+ feature, modeled but not transacted in v1.
- Drop-ship handling (line ships from supplier direct to customer, never touches our warehouse) — Sprint 19+ stretch.

---

*This ADR is the architectural prophylactic against future "let's just attach the ProductionOrder to the SalesOrder header" simplifications. The header-level coupling is the QuickBooks pattern; we are not building QuickBooks. We are building best-in-class for manufacturers who run blanket orders, scheduling agreements, and per-line MTO/ETO traceability — and that requires line + release granularity from day one.*
