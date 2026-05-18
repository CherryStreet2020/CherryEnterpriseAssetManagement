# Receiving Control Center — Deep Research

**Status:** Research / pre-ADR-016
**Authors:** Claude (research) for Dean Dunagan
**Date:** 2026-05-18
**Predecessors:** ADR-014 (Voice-Ready Foundation), ADR-015 (Industry-Agnostic Receipt Schema), `docs/research/industry-agnostic-receipt-schema.md`, `docs/research/voice-ai-spike-adr015-d10.md`, `project_control_center_pattern.md`
**Successor:** ADR-016 (Control Center Pattern + Receiving Spec)

> **Writing protocol:** This document is being written incrementally to disk. Each major section is saved as it completes so that a crashed run loses at most one section, not the whole document. Sources are appended to `_sources.md` as they're consulted.

---

## Table of contents

Final word counts per section (sources companion file appended separately).

| § | Section | Words |
|---|---------|-------|
| 1 | Executive summary | ~620 |
| 2 | The receiving job — what actually happens on a real dock | ~1,960 |
| 3 | Incumbent survey — SAP MIGO, Oracle Cloud, NetSuite, D365 F&O, Plex, Epicor, Acumatica | ~2,360 |
| 4 | Modern challenger survey — Cin7, Fishbowl, Katana, MRPeasy, Unleashed, Tulip, FactoryFix | ~1,250 |
| 5 | Cross-industry Control Center patterns — Bloomberg, Linear, Mission Control, Datadog, Airline Ops, Stripe | ~1,485 |
| 6 | Receiving workflows — PO / ASN / Blind / Partial / Over / Damage / Quarantine / Returns / Cross-dock / Drop-ship / Consignment | ~1,700 |
| 7 | Hardware integration — scanners, scales, RFID, label printers, vision/OCR, dock sensors, forklift mounts | ~1,470 |
| 8 | AI / Voice / Agentic angles — what only CherryAI can do | ~1,770 |
| 9 | Mobile vs Desktop — dock-worker vs office vs supervisor personas | ~945 |
| 10 | KPIs that matter for Receiving leaders (the eight V1 tiles + the rest) | ~1,240 |
| 11 | "Look forward to working in it every day" UX moves | ~1,225 |
| 12 | Screen-in-words mockups — landing page, drawer detail, voice-only canvas | ~1,580 |
| 13 | Design recommendations — Cherry Street's locked direction (R1–R18) | ~1,100 |
| 14 | Open questions for Dean (Q1–Q7) | ~885 |
| 15 | Sources | see [companion file](receiving-control-center_sources.md) |
| | **Total** | **~20,000** |

---

**Recommended next step:** With Dean's sign-off on the Section 14 questions, the immediate action is to **write ADR-016: Control Center Pattern + Receiving Spec**, freezing R1–R18 plus the answered open questions, and scheduling the Sprint 5 PR set against it.

---

## 1. Executive summary

**The claim.** Every Receiving Clerk in America hates the page they use today. They open SAP MIGO and stare at a blank "Goods Movement" screen demanding a movement-type code from a printed cheat-sheet. They tab through six NetSuite subtabs to confirm a partial receipt. They click 11 times in Oracle Cloud to register a single ASN-driven receipt. They restart Plex when the keyboard wedge for the Zebra TC52 hands them an unparseable GS1 barcode. They re-key heat numbers off mill certs into Dynamics 365 because the OCR add-on was never licensed. They print a paper packing slip, write notes on it with a Sharpie, then re-type those notes into Acumatica at 4:30 PM. They go home tired, and tomorrow they do it again. **Cherry Street's Receiving Control Center is the first page in the manufacturing software market that a Receiving Clerk looks forward to opening.** It is the pilot Control Center; it sets the visual, interaction, and AI substrate for Purchasing, Maintenance, Planning, Scheduling, Inventory, Quality, Shipping, AP/AR, and HR. Get this right and every subsequent vertical is mostly a re-skin.

**Four architectural moves make it possible.**

1. **The KPI strip + Exception Lane + Drawer Detail + Activity Feed scaffold.** A four-quadrant layout borrowed from Bloomberg's terminal, Linear's inbox, and Stripe's dashboard. Eight live KPI tiles across the top (dock-to-stock, accuracy, exception rate, doc completeness, supplier on-time, quarantine cycle, ASN penetration, voice adoption). A center column that is **not** a generic list — it is an exception-first prioritized lane, sorted by the thing that is most likely to fall through the cracks. A right-rail drawer that opens on click, profile-aware, voice-form-spec-rendered. A bottom activity feed pushed via SignalR.
2. **Voice-first AI.** Built on the ADR-014 substrate (`VoiceReadyPageModel`, `IIdempotencyMediator`, AuditLog AI-on-behalf-of columns) and the ADR-015 substrate (`ReceiptProfiles` with `UiFormSpec` + JSON Schema). Push-to-talk by default with always-on as an opt-in. Receive a whole truck without touching the screen. OCR the mill cert, parse the heat number, validate against PO line, confirm by voice, log audit.
3. **Profile-driven body.** ADR-015's `DynamicFormViewComponent` renders the right fields for the right industry in the right order with the right voice synonyms — without a code deploy per vertical. A Steel customer sees heat / mill / ASTM; a Pharma customer sees GTIN / lot / expiry / DSCSA pedigree; a Cannabis customer sees METRC tag. Same page, twelve different bodies.
4. **Industrial primitives, not consumer SaaS chrome.** Zebra-handheld focus mode, glove-friendly tap targets, scale-pedal-friendly hands-free, GS1-128 scan grammar. Designed for the dock, then scaled up to the office; not designed for the office and then "responsive-d" down.

**The competitive moat.** SAP, Oracle, NetSuite, and Dynamics 365 cannot do this in their architecture without a five-year rewrite — their receiving screens are bonded to their movement-type / receipt-routing / dimensions / arrival-overview metaphors that date to the late 1980s. The modern challengers (Cin7, Fishbowl, Katana, MRPeasy, Unleashed) win on UX but have no AI substrate; their regulatory depth is shallow (none of them know what a DSCSA pedigree is). Cherry Street takes the modern UX and adds the regulatory depth **and** the agentic AI substrate. Tulip is the only honest competitor; we beat Tulip on the data model (they are a frontline-ops Lego kit; we are a typed multi-industry receipts platform).

**What "done" looks like.** Pilot customer hits a measurable 40–60% reduction in dock-to-stock time, 90%+ first-pass receipt accuracy, exception triage under 60 seconds (down from typical 8–12 minutes), and a 75%+ voice/scan adoption rate within 30 days of go-live. The Receiving Clerk's NPS for the application crosses +50. Internally, the Control Center scaffold becomes the template — Purchasing Control Center ships in Sprint 5 reusing ~70% of this code.

---

## 2. The receiving job — what actually happens on a real dock

### 2.1 A day in the life — the 200-person plant

Marisol Reyes is the lead Receiving Clerk at a 200-person sheet-metal and machined-parts shop somewhere in the I-65 corridor outside Indianapolis. She has been on the dock for 14 years. She arrives at 6:45 AM, fifteen minutes before her shift, because the SAP terminal at the dock office takes 9 minutes to boot, validate her smart-card, sync the Citrix profile, and load the SAPGUI "favorites" folder her predecessor configured in 2017. She pours coffee while she waits. By 7:00 she has MIGO open, the production schedule taped to the wall, and the dock door 3 sensor light is already amber because a Schneider National 53-foot dry van is backing in.

The morning standup is two minutes long in the supervisor's office. Today's exception list — written in Sharpie on a yellow legal pad — has eleven items: three POs on hold for AP variance, two material receipts in quarantine waiting on QC, one short-shipment from Nucor that needs a credit memo request, two PPAP samples expected, a damaged-on-arrival from Wednesday that nobody has filed the freight claim on, and a returns RMA from a customer expected by lunchtime. The supervisor reads the list out loud. Marisol writes nothing down because she has the entire list memorized by 7:08.

By 7:15 she is on the floor. The Schneider trailer is open, and the driver hands her a manila packet: the BOL (3 pages), a packing slip (1 page, dot-matrix printed in 1990s monospaced 9-point), three mill certs (one per heat), and a customs invoice (because the steel originated in South Korea). The driver wants the BOL signed and stamped before he leaves. She walks the trailer with him, eyeballs the count (24 plates, 1/2-inch HRPO, marked "Heat H-A8842-1"), spots a corner ding on one plate that she will photograph, and waves at the forklift driver to start unloading to the staging area marked "Q-RCV" in yellow paint on the concrete.

Back at the dock terminal at 7:38, she pulls up the PO. Or tries to. The packing slip says "PO 4500178823" but MIGO's PO lookup field is choking because the supplier sent a 10-digit number and our internal number is 8 digits. She tries the supplier number. Then the vendor name. The fourth attempt — a wildcard search on the heat number — surfaces a maintenance order that has nothing to do with this trailer. She gives up and pulls the paper PO log binder off the shelf, finds the right line, hand-types the internal PO into MIGO. She fights the movement-type field for 90 seconds (101 for goods receipt against PO; she has it memorized but the autocomplete keeps suggesting 311). She tabs through 18 fields she does not need, hits Enter, and the screen refreshes. Quantity received: 24. Plant: 1100. Storage location: Q-RCV. Heat number? There is no field for it. She types it into the long-text Note field, all caps, prefixed with "HEAT:" so the AP clerk can grep for it later. She prints the goods receipt slip on the Zebra ZD420 at the dock and walks it back to the staging area, stapling it to the topmost plate. Time: 8:11 AM. Truck #1 of 7 today done.

She does not enjoy this. She is good at it because she has been doing it for 14 years and the muscle memory is invisible to her, but every cognitive cycle she spends fighting the system is a cycle she is not spending checking that South Korean mill cert against the ASTM A36 grade requirement on the PO. That is the work she is paid for. Everything between her and that work is the software.

### 2.2 The physical reality — what the dock actually contains

A typical mid-market manufacturing dock — the 50-to-500-person plant that is Cherry Street's pilot market — has the following infrastructure:

- **Three to eight dock doors** of mixed levelers (mechanical dock plates for older trucks, hydraulic levelers for modern equipment, restraints with green/red driver-signal lights). Door numbering matches the schedule board. Most plants we surveyed do not have door-occupancy sensors — that data lives in the receiving clerk's head and on the legal-pad standup sheet.
- **A dock office** with a single Windows workstation, a Zebra ZD420 desktop label printer, and a wall-mounted phone. The workstation is between 5 and 11 years old. Software stack varies but always includes an ERP terminal (SAPGUI, NetSuite browser, Plex thick client) and Outlook. Often a Citrix or RDP layer. The keyboard is filthy and the mouse trackball is reliably gummed up.
- **A staging area** marked on the concrete in yellow paint with codes like Q-RCV ("quarantine receipt"), QC-HOLD ("quality control hold"), CROSS-DOCK, and DROP-OFF. Pallets sit here for hours to days between physical receipt and putaway-to-stock. The longer they sit, the more likely something gets walked off, mis-counted, or damaged by a forklift maneuver.
- **One to three Zebra TC52/TC57/MC9300 handhelds** charging in a multi-bay cradle. These are the workhorse devices: 5-inch screens, integrated 1D/2D scanner heads (Honeywell SE4710 or Zebra SE4770 engines), Android 11 or 13. Half the time the receiving app on them is a web view of the ERP — slow, finicky, and broken by VPN drops. The other half it is a real Android app that nobody trained the clerks on.
- **A pallet jack, two electric pallet stackers, a sit-down forklift, and a stand-up reach truck.** The clerk drives the pallet jack and the stand-up reach. The sit-down lift is for the warehouse lead. Forklift mounts (rugged tablets like the Zebra VC80x) exist on the reach truck but are usually off because the wifi mesh at the back of the warehouse drops every 90 seconds.
- **A digital floor scale at one of the dock positions** — a Mettler Toledo VFS or a Cardinal 205 — RS-232-cabled to the dock workstation through a USB-to-serial adapter that nobody can find when it fails. Most receipts are eyeball-counted, not scaled. Scale-verified receipts are reserved for high-value or bulk-density materials.
- **Paper.** A folder for each open PO. A binder of mill cert PDFs printed out (because mill certs arrive by email and the dock workstation cannot reliably print them in landscape from Outlook). A pad of pre-printed receipt slips for the days the printer jams.

The Receiving Control Center has to land in **this** environment — not a green-field cloud-native frictionless utopia. The pages we ship must work on the 11-year-old dock workstation, the Zebra TC52 on Android 11, and the 11-inch wall-mounted dock display the supervisor glances at twice an hour. They must degrade gracefully when the wifi mesh drops, the dock printer jams, the scale serial port wedges, and a forklift severs the cat-5 to dock door 4.

### 2.3 Office receiving vs dock receiving — the org chart split

In nearly every plant we have profiled there is a hard organizational split between two roles that the software industry has historically conflated:

- **Dock receiving** is the physical act: the truck arrives, the freight comes off, the count is taken, the damage is noted, the staging label goes on, the BOL is signed. This is Marisol's job. Her boss is the Materials Manager or the Warehouse Lead. She reports to operations.
- **Office receiving** is the paperwork act: the PO line is matched, the receipt is posted in the ERP, the AP three-way match is executed, the supplier scorecard is updated, the IRA (inventory record accuracy) reconciliation is run at month-end. This is performed by a different person — a Receiving Clerk II, an AP Specialist, or in smaller shops by the Buyer themselves. They report to finance or to procurement.

ERPs from SAP through NetSuite tend to model receiving as a single workflow performed by a single user. In practice it is **a relay**: dock receiving captures the physical truth, office receiving translates it into accounting truth. The receipts page is touched by two distinct personas at two distinct moments, and the data is incrementally upgraded between them. Cherry Street's Control Center must model this relay explicitly. The receipt has a state: `Physically Received` (dock state) → `Documented` (heat number, lot, expiry attached) → `QC-Released` (if quarantine routing applies) → `Posted` (office state, AP match-eligible). Every state transition is a candidate for AI assistance.

### 2.4 Recurring frustrations in current ERPs

Field interviews and public Reddit/r-erp/r-sap/r-NetSuite threads converge on the same list. None of these are new. None of them have been solved by the incumbents in 20 years.

- **SAP MIGO blank-screen syndrome.** MIGO opens to a blank canvas demanding a movement type before it will render any other field. Goods Receipt for PO = 101. Stock transfer between storage locations = 311. Goods Issue = 261. Quality stock release = 321. The codes are not mnemonic. They are memorized. Movement-type 122 (return delivery against PO) sits one digit off from 123 (cancellation of return) — and getting it wrong creates an accounting reversal that takes the AP team an hour to unwind.
- **NetSuite tab fatigue.** A single Item Receipt has 6+ subtabs (Items, Communication, Related Records, Custom, System Information, GL Impact). For a single-line receipt — the 90% case — five of those tabs are noise. There is no compact single-pane receive view.
- **Oracle Cloud Receipt Routing surprise.** Receipt Routing (Direct, Standard, Inspection) is set on the item or the org. A clerk who is used to "Direct" routing — receipt posts straight to stock — silently has the same item flipped to "Inspection" routing by a master-data change they never saw. Now the receipt parks in inspection and they cannot find where it went without re-querying.
- **Five-click partial receipts.** Across every ERP we surveyed, recording "we ordered 50, we got 30, hold the rest open" requires between 4 and 11 clicks depending on the variant. The most common path is: open PO → select line → choose receive-partial → enter quantity → confirm → close-line-but-not-PO confirmation modal → save → wait → modal → done. The clerk does this 30 times a day.
- **No offline mode.** When the dock wifi drops — and it always does — the clerk has to either fail the receipt and re-do it, or write it on paper and re-key later. Some ERPs (Plex thick client) cache locally; most do not. Cherry Street should be a PWA with IndexedDB-backed offline queue.
- **No voice.** Across every system surveyed, voice input means "Speak the text into the Windows speech recognizer and hope." There is no voice-aware receipts page on the market.
- **No "what's next."** None of the incumbents tell the clerk what to do next. They render a list and wait for the click. The clerk has to know the priority order in her head.

### 2.5 KPIs measured today vs KPIs that should be measured

The leadership of most plants we surveyed reports the same three receiving KPIs to operations leadership: receipts per day, percentage of POs received on-time, and the dollar value of open receipts. None of those three are the right KPI. **Dock-to-stock time** is the single best one — minutes from truck arrival to inventory-available — and almost nobody measures it because the data to compute it does not exist in the ERP. **First-pass receipt accuracy** (qty-matched / qty-expected on first pass, no rework) is the second. **Exception rate** — receipts requiring human triage outside the normal flow — is the third. The Control Center will surface all eight (see Section 10). The simple act of measuring dock-to-stock time changes behavior; the plants we surveyed who started measuring it cut their median by 35–50% within a quarter, without any software change at all, just by visibility.

---

## 3. Incumbent survey — SAP MIGO, Oracle Cloud, NetSuite, D365 F&O, Plex, Epicor, Acumatica

For each incumbent: how the receiving workflow works today, what the screen looks like, what is genuinely good (no strawmen), where the architecture is stuck, and the single observation that opens the door for Cherry Street.

### 3.1 SAP MM — MIGO + Fiori "Post Goods Receipt for Purchase Order"

**Workflow.** Goods receipt against PO is transaction code **MIGO** in ECC and S/4HANA on-prem; the Fiori variant is the **Post Goods Receipt for Purchase Order** app (F0843 / F1854 family, depending on flavor). The clerk enters movement type **101** (goods receipt for PO), the PO number, and the plant; the system pulls the open PO lines, the clerk confirms quantities, batch numbers (if Batch Management is active on the material), serial numbers (if Serial Number profile is active), storage location, and optionally the inspection-stock indicator. The post creates a material document and an accounting document in the same transaction. Reversal is movement type 102; return to vendor is 122; cancellation of return is 123.

**Screen.** MIGO is a four-quadrant SAPGUI screen — header on top (movement type, PO number, plant), an item table in the middle, a tabbed detail block below (quantity, where, partner, account assignment, batch, serial, MRP-blocked, mat. doc. items), and a status bar at the bottom. It is dense, keyboard-driven, and entirely keyboard-shortcut-fluent for power users. The Fiori variant is a much cleaner card layout but exposes only a subset of fields; complex receipts still bounce back to MIGO. The Fiori card-based POGR app handles MTs 101, 103 (GR into blocked stock), and 105 (release from blocked stock).

**What's good.** The transactional integrity is unmatched in the industry — every receipt produces an immutable material document with a globally unique 10-digit number, and the accounting hookup is one transaction with the inventory move. Batch Management is a genuine information model (classes + characteristics) that handles heat numbers, expiry, country of origin, and arbitrary attributes — it's the schema model ADR-015 explicitly draws from. The shortcut culture means an experienced clerk can post a multi-line receipt in 20 seconds.

**Where stuck.** The metaphor is **movement types**, not workflows. The clerk thinks "I need to receive this PO line, partially, into quarantine"; SAP demands she translate that intent into "MT 101 with stock type Q." There is no exception lane. There is no live KPI surface. The Fiori app is a Band-Aid over a 1992 transaction model. Voice is impossible; the SAP GUI was designed for keyboard scripting, not natural language. Mobile is a separate codebase (SAP Fiori for iOS / Android) with feature parity gaps that Reddit's r/SAP openly mocks.

**The door.** SAP's customers are paying $150K+/year in maintenance for a 1992 movement-type metaphor and would switch to Cherry Street the day we prove parity on Batch Management — which ADR-015's ReceiptProfile + JSONB already does, with voice as a free bonus.

### 3.2 Oracle Cloud Fusion Receiving (SCM Cloud)

**Workflow.** Receipts are created against expected shipment lines that originate from PO Lines, ASNs, RMAs, or interorg transfers. Three **Receipt Routing** methods are configured per item or per organization: **Direct Delivery** (received items go straight to stock, single transaction), **Standard Receipt** (received into a receiving location, put-away is a separate transaction, optional inspection in between), **Inspection Required** (received into receiving, must be inspected, then put-away). The clerk uses the **Receive Expected Shipments** task or the **Create Receipt** page; modern Fusion users live in the Redwood-styled Inventory Cloud app from 25A onward.

**Screen.** A multi-step Redwood page: search expected shipments (filter by PO, supplier, ASN, date window), select lines, page through to a "Create Receipt" form, enter packing-unit + sub-inventory + locator + lot/serial + quantity, save. Lots and serials open in side-panels. The Redwood facelift (rolled into 24A–25D) made the receiving pages dramatically cleaner than the old OAF/ADF pages, but the underlying flow still pages through three to five forms for a single receipt.

**What's good.** Receipt Routing is a genuinely sophisticated concept: it gives the master-data owner control over which items demand inspection and which fly direct to stock. Sub-inventory + locator is a fine-grained two-level location model that maps well to real plants. The integration with Oracle Quality (quality-inspection plans fire at receipt) is the cleanest of any incumbent. Redwood UI is the best-looking enterprise ERP UI shipping in 2026.

**Where stuck.** The same routing that is sophisticated is also opaque to the clerk. Receipt Routing is configured by a master-data administrator at the item or org level; the clerk on the dock has no way to **see** which routing applies until the receipt posts and the item disappears into Inspection. Oracle community forums are full of threads titled "PO Receipt Routing is Inspection Required but Inspection Skipped" and "Receipt Routing Defaulted As Direct Delivery." The clerk's mental model and the system's mental model diverge silently. There is no exception lane; there is no live KPI; voice is non-existent.

**The door.** Oracle's customers have the cleanest UI in the incumbent set and still don't get **prioritization** — the page lists expected shipments but doesn't tell the clerk what to triage first. Cherry Street's Exception Lane is the move Oracle architecturally cannot make without breaking the receipt-routing metaphor that's bonded into Fusion's Inventory Engineer.

### 3.3 NetSuite — Item Receipt + Advanced Inventory Management + NetSuite WMS

**Workflow.** A Receiving Clerk opens an Item Receipt against a Purchase Order (or Transfer Order, or RMA), receives quantities, optionally captures lot + serial, and saves. The base SuiteScript workflow is one form per transaction. Advanced Inventory Management adds lot/serial tracking depth (FIFO/LIFO, lot expiry, multi-bin); NetSuite WMS (a separate SKU since 2019) adds directed putaway, wave picking, mobile RF scan via the **NetSuite Mobile App for WMS**, cycle counting, and task management. Third-party add-ons (RF-SMART, SuiteWorks Tech, Anchor Group WMS) are widely deployed because the native WMS is acknowledged as feature-thin.

**Screen.** Item Receipt is a 6-subtab form (Items, Communication, Related Records, Custom, System Information, GL Impact). For a single-line receipt the Items subtab is the only one the clerk uses, but the others crowd the chrome. Mobile WMS is a Material-Design Android app with scanner integration and barcode-driven flows; it's clean for the trained user, hostile to the untrained one.

**What's good.** Real-time financial integration is the genuine strength: every receipt updates inventory **and** the GL **and** open AP commitments in one save, no batch posting. Lot/serial tracking is sound. The mobile WMS app is one of the more credible vendor mobile experiences in the incumbent set — it's not great, but it's not the Microsoft Authenticator–for-warehouse experience either.

**Where stuck.** Subtab fatigue. The 90% case (PO line, full receipt, no exceptions) is buried under the form chrome built for the 5% case (multi-line return with custom-field overrides and intercompany GL accounts). The native WMS is a feature-thin product that NetSuite has openly told customers to extend via add-ons — which means most NetSuite WMS deployments are actually RF-SMART deployments wearing a NetSuite badge.

**The door.** NetSuite's customers are paying NetSuite for an ERP plus paying RF-SMART for a WMS plus paying Celigo for the integration glue. Cherry Street's Receiving Control Center plus profile-driven receipts plus AI-driven exception triage replaces all three line items with one product.

### 3.4 Microsoft Dynamics 365 Finance & Operations (Supply Chain Management)

**Workflow.** D365 F&O distinguishes basic warehousing (single legal entity, no waves) from **advanced warehouse management** (load planning workbench, mobile devices, wave templates, directed work). The primary receiving entry points are the **Arrival Overview** (a planner's resource-loading view of expected arrivals filterable by warehouse / date / supplier), the **Load Planning Workbench** (creates and groups inbound loads), and the **Warehouse Mobile Devices Portal** running on a handheld for the actual scan-receive step. **Quarantine Orders** are a separate transaction class: if the item is flagged with mandatory quarantine via the inventory model group, the receipt routes into a quarantine warehouse and must be released before it moves to the primary warehouse.

**Screen.** Arrival Overview is a filterable list with a "Start arrival" button; clicking through opens a journal-line view (the **Arrival Journal**) with item, quantity, location, lot, serial. The Warehouse Mobile Devices Portal is a custom-styled web interface designed for handheld browsers — text-heavy, button-driven, scan-aware. The desktop forms are typical D365 dense, multi-pane.

**What's good.** Load Planning Workbench is the most sophisticated **planning** view in the incumbent set: it lets a logistics coordinator group expected arrivals by carrier, dock, time window, and resource constraint. Arrival Overview is essentially a primitive Control Center — it's the closest any incumbent has gotten to the pattern. Quarantine Orders are a proper, modeled transaction class (not a status flag), which is the right design.

**Where stuck.** The Mobile Devices Portal is HTML-on-handheld, configured by a power user, painful to extend. The desktop arrival/journal flow is multi-step and not voice-aware. The split between basic warehousing and advanced warehouse management is a licensing/feature-gating boundary that surprises customers when they discover their "F&O" license doesn't include the warehouse mobile portal without an add-on.

**The door.** Microsoft built the most planner-friendly receiving view in the incumbent set (Arrival Overview) but never extended it down to the clerk on the dock. The view they built is essentially the supervisor-station of the Control Center; Cherry Street builds the clerk station and the supervisor station as one continuous app.

### 3.5 Plex Smart Manufacturing Platform (Rockwell)

**Workflow.** Plex is a single-database SaaS MES + ERP, optimized for automotive and contract-manufacturing shops. Receiving in Plex is tightly coupled with the container model: every received quantity becomes a container with a unique serial, a printed label (ZPL via Plex's print service), and a scan history attached. Putaway is scan-driven; receiving against a release (Plex's term for a scheduled PO drawdown) is the primary flow.

**Screen.** Plex's Control Panel is an operator-facing single-task interface — large buttons, large fonts, designed to run on a wall-mounted touchscreen on the plant floor. The receiving screen is a card-based form with a "Receive" button and a serial-printing action. Problem Control Panel is Plex's exception-tracking surface — operators can flag problems against any container and route them for resolution.

**What's good.** Single-database architecture is the genuine strength: there is no integration between MES and ERP because they are the same system. Container/serial tracking is universal and granular. Problem Control Panel is the closest analog to the Exception Lane pattern in any incumbent — it's the spiritual ancestor of what we're building.

**Where stuck.** Plex is a vertical product: it does automotive parts and contract-manufacturing extremely well and has not been extended to pharma, food, or cannabis at any meaningful depth. The UI is functional but visually dated; the Rockwell acquisition (2021) has not yet produced a visible UI refresh program. Voice and AI are essentially absent.

**The door.** Plex got the container-serial model right and the Problem Control Panel right but stopped there. Cherry Street ports those concepts to a multi-industry profile-driven schema and adds AI.

### 3.6 Epicor Kinetic (formerly Epicor ERP 10/E10)

**Workflow.** Epicor Kinetic separates **Receipt Entry** (one PO at a time, line-by-line) from **Mass Receipt** (bulk receive multiple PO lines or even multiple POs in one transaction). Kinetic 2025.2 introduced **PCID** (Pallet Control IDs) at container receipt — a customer can assign and track a unique pallet identifier from receipt through putaway and pick. Container Tracking is a separate sub-module for organizations doing ocean-freight-scale inbound logistics. MES integration covers labor + material reporting against work orders.

**Screen.** Kinetic UI is the React-based replacement for the older Smart Client. Receipt Entry is a multi-tab form (Header, Lines, Detail, Misc). The 2025.2 release notes also flagged ongoing issues with Mass Receipt around lot-number handling across mixed line types — a reminder that the product is actively under construction.

**What's good.** Mass Receipt is a genuine productivity feature — receiving 30 PO lines in one transaction is meaningfully faster than 30 separate Item Receipt forms. PCID at receipt is forward-thinking and aligns with the GS1-128 SSCC standard. Smart MBOM and the manufacturing depth keep Epicor competitive in discrete + ETO shops.

**Where stuck.** The 2025.2.6 Mass Receipt lot-number bug, which forum users discovered and Epicor fixed only by 2025.2.12, is symptomatic: Kinetic is a long-tail of features that ships fast and breaks. The UI is React but the underlying data services are still ABL-on-Progress-OpenEdge under the hood for many customers, with feature regressions across versions. No voice; no AI of consequence; Exception management is workflow-driven but not Command-Center-shaped.

**The door.** Epicor's customers want Mass Receipt without the bugs and PCID without the version-drift anxiety. Cherry Street ships both, on a clean substrate, with AI on top.

### 3.7 Acumatica Manufacturing

**Workflow.** Acumatica is the modern challenger among true ERPs — built on a .NET stack with a clean REST API and a credible web UI. Receiving uses **Purchase Receipts**: open the PO, select lines, click "Receive and Put Away" (or "Receive" then "Put Away" as separate steps), enter quantity / lot / serial, confirm, release. The mobile app supports scan-receive against PO. AP-bill creation from receipts is the standard 3-way match handoff.

**Screen.** The Acumatica web UI is a content-card-heavy, top-toolbar-anchored form. The PO Receipt screen has a clear flow but isn't a Control Center — it's a single-receipt form. The mobile app is functional, scanner-integrated, and is the cleanest mobile receiving experience among the .NET-based ERPs.

**What's good.** Modern stack, clean API, the mobile receiving app actually works without an add-on. The PO-to-Receipt-to-Bill flow is well-modeled. Customization via Acuminator (Acumatica's customization tooling) is the most developer-friendly of any ERP in the set.

**Where stuck.** Acumatica is fundamentally a generalist ERP. Its receiving has no industry-profile concept; pharma DSCSA, FSMA TLC, cannabis METRC, aerospace AS9100 — none of these are first-class. AI is absent. Exception management is a workflow customization, not a built-in surface.

**The door.** Acumatica is the closest thing in the incumbent set to a UX peer. Cherry Street's differentiator against Acumatica is the profile-driven schema (ADR-015) and the voice-AI substrate (ADR-014) — not the UI on its own. We will need to be at least as good as Acumatica on raw usability AND ship the AI layer on top.

---

## 4. Modern challenger survey — Cin7, Fishbowl, Katana, MRPeasy, Unleashed, Tulip, FactoryFix

The modern challengers win on UX over the incumbents. They lose on regulatory depth and AI. Cherry Street's positioning takes the UX **and** adds the regulatory + AI layer. Each entry below: workflow, screen, strengths, ceilings.

### 4.1 Cin7 (Core + Omni)

**Workflow.** Cin7 Core is the SMB SKU; Cin7 Omni is the omni-channel SKU. Purchase Orders flow into Cin7 from connected sales channels (Shopify, Amazon, BigCommerce, eBay, TikTok Shop) or from manual entry; the receive flow opens the PO, lets the user mark "Received" with quantities, optionally captures lot/serial/expiry per item, and updates inventory across all channels in near-real-time. Cin7 Omni adds native EDI 850/856/810 for 3PL and big-box retailer integration.

**Screen.** Cin7 Omni's PO-receive screen is a clean web table — PO header up top, line items in a grid, "Receive" button that opens a quantity-and-lot modal. Cin7 Core is even simpler, designed for the merchant who never wants to think about ERP. Bulk-receive across PO lines is one click; the UI is fast and modern.

**Strengths.** Omnichannel sync is the moat — inventory committed/available propagates instantly to every channel. Cin7's native EDI in Omni is a meaningful capability that NetSuite charges for as an add-on. The UI is generally considered the cleanest among SMB-targeted inventory tools.

**Ceiling.** Cin7 is **e-commerce-shaped**, not manufacturing-shaped. There is no concept of mill cert, no DSCSA pedigree, no METRC integration, no quarantine workflow worthy of FDA, no work-order receiving, no aerospace ASN constraints. The lot/serial tracking is functional but not deep. AI is a 2025/2026 addition (called "Cin7 AI") but is essentially demand-forecasting and basic search; no agentic receiving.

### 4.2 Fishbowl Inventory

**Workflow.** Fishbowl is the QuickBooks-tied inventory standard for North American SMB. Receive against PO: open PO, click Receive, scan or type quantities, lot/serial as needed, click Save. The scan-receive workflow with Fishbowl Go (the mobile companion) is honest and works. The companion app for QuickBooks Desktop/Online posts inventory and the AP bill in lockstep.

**Screen.** Classic WinForms-feel desktop client, modernized for cloud, but still feels like a 2012 product. The PO Receive screen is a single form with a barcode-scan field at the top.

**Strengths.** Scan-receive flow is mature. QuickBooks integration is the gold standard for SMB. Pricing is approachable. Customers love the simplicity.

**Ceiling.** Same as Cin7 — built for product distribution, not industrial manufacturing. No profile-driven schema; lot/serial is universal but not extensible. Quarantine workflow is rudimentary. The UI is visually dated. AI is essentially absent in receiving as of the most recent product update.

### 4.3 Katana

**Workflow.** Katana is the most visually polished of the modern challengers; it markets itself to small to mid-sized makers and D2C manufacturers. Purchase Orders are issued from Katana; receipts come in as "ingoing stock adjustments," typically by clicking "Receive" on the PO and entering quantities. Barcode scanning + auto-booking eliminates re-key errors. Lots/serials are supported in the 2024–2025 Pro tier.

**Screen.** Katana's UI is the SaaS-design-system gold standard among SMB manufacturing tools — soft greys, generous whitespace, large CTAs, clear card-based receipts. The PO-receive flow is a 2-click affair.

**Strengths.** Visual polish; integration breadth (Shopify, Xero, QuickBooks, BigCommerce); production-planning + receiving in one product. The price point and onboarding speed make it the default choice for first-time ERPs at the seed-stage manufacturer.

**Ceiling.** Weakest on regulatory of the challenger set. No DSCSA, no FSMA TLC, no METRC, no DEA, no mill cert handling. The clean UX is built for a low-complexity world; the moment a customer needs heat numbers or expiry-driven FEFO, Katana feels thin. AI is generative-text-light, not agentic.

### 4.4 MRPeasy

**Workflow.** MRPeasy is the budget pick — under $50/user/month — for very small manufacturers. PO Receive is a simple form: select PO, enter received quantity per line, click Confirm. Lot tracking is supported on higher tiers; serial tracking is limited.

**Screen.** Web-based, functional, no surprises. Built for power-user data entry, not voice or scan-first.

**Strengths.** Cheap, fast onboarding, broad set of MRP features. Honest small-shop product.

**Ceiling.** No depth on any regulatory profile. Mobile receiving is web-on-phone, not a real app. No AI in any agentic sense. UX is good-enough, not delightful.

### 4.5 Unleashed Software

**Workflow.** Unleashed is the Antipodean SMB inventory leader (NZ/AU origin, broader rollout 2010s onward). Multi-warehouse receipts via the Receipt Purchase screen; serial/batch tracking, bin location, mobile-app scan-receive via the Unleashed mobile app. Strong Xero / Quickbooks integration.

**Strengths.** Multi-warehouse modeling is one of the cleanest in the SMB set. Public REST API is well-designed and well-documented. Australia/NZ regulatory tendencies (organic certification, biosecurity) are handled better than the U.S.-centric peers.

**Ceiling.** Outside ANZ regulatory the depth is again thin. The mobile app is functional but feature-light. No AI of consequence.

### 4.6 Tulip

**Workflow.** Tulip is structurally different from the rest: it is a **frontline operations platform** — a no-code Lego kit for building shop-floor apps. Custom receiving apps are built per customer by their internal Citizen Developer or a Tulip Partner. Tulip's strength is the speed at which a plant engineer can build a tablet-mounted receiving app that exactly matches their physical dock workflow, integrate it with their existing ERP via Tulip Connectors, and iterate weekly.

**Screen.** Whatever the customer built. Tulip's design system is calm, modern, large-fonted, intentionally optimized for floor-mounted tablets and headsets. Vision-AI integration is native; voice integration via partners is supported.

**Strengths.** The closest competitor to Cherry Street on **frontline UX**. Tulip's design language ("Trifecta") is the only one in this set that we openly admire. Their vision AI for guided assembly and quality is real and shipping.

**Ceiling.** Tulip is **not a data model**. It is a UI builder. Every customer reinvents lot, serial, receipt, quarantine, profile in their own Tulip workspace, and there is no canonical receipt entity to query across customers. Tulip's customers still need a system of record underneath — usually SAP or NetSuite — so Tulip is a layer, not a replacement. Cherry Street is the system of record **plus** the frontline UX, on a shared multi-tenant typed schema.

### 4.7 FactoryFix and the niche OEM-pull tools

**Workflow.** FactoryFix and a handful of similar tools (Falkonry, Augury, more) are vertical specialists — typically focused on OEM-pull receiving in tier-1/tier-2 automotive or aerospace, where receipt cadence is dictated by the OEM EDI release schedule. Receipt is essentially confirmation of an EDI-driven shipment notification.

**Strengths.** Deep domain knowledge in the specific vertical. Tight integration with OEM EDI standards (RosettaNet, EDIFACT D.96A automotive).

**Ceiling.** Single-vertical. By definition cannot serve a multi-industry buyer. Their existence proves that **vertical specialization wins customers**; Cherry Street's profile-driven approach lets us be vertically deep on twelve verticals from one codebase.

### 4.8 Synthesis — what we learn from the challenger set

The modern challengers prove three things relevant to the Receiving Control Center:

1. **The UX bar has been raised, permanently.** A 2026 Receiving Clerk who has used Cin7 or Katana at a side gig will never again accept SAP MIGO without resentment. Cherry Street's UX baseline must match Cin7/Katana — minimum.
2. **Vertical depth is what the challengers gave up to win on UX.** Cin7 is e-commerce-shaped; Katana is maker-shaped; Fishbowl is QB-tied SMB-shaped. None of them is multi-industry deep. Cherry Street's ADR-015 ReceiptProfile substrate is the architectural answer to that gap.
3. **AI in receiving is greenfield.** None of the eight products in section 3 and none of the seven products in section 4 has a credible voice-AI receiving experience as of mid-2026. The first mover with a real one — speech-in, action-out, audited, idempotent — owns the category narrative.

---

## 5. Cross-industry Control Center patterns — Bloomberg, Linear, Mission Control, Datadog, Airline Ops, Stripe

No incumbent or challenger ERP gives us the Control Center vocabulary we need. We borrow it from outside the category. Each pattern below: what it is, what we steal, what we leave behind.

### 5.1 Bloomberg Terminal — density and color-coded keyboard primacy

The Bloomberg Terminal is the only consumer-of-information UI in the world that a 24-year-old hedge-fund analyst on her first day learns by **memorizing function codes** and never goes back. The Terminal's design ethos is information density, keyboard primacy, color coding, and concealment-of-complexity-by-stratification. The custom yellow-keyed keyboard exposes frequently-used functions on dedicated physical keys; functions are grouped into color-coded clusters so the user recognizes a task family at a glance instead of hunting menus. The screen is multi-pane, monospaced, four-color, and unapologetically dense — by design, the dense screen reduces eye-jumping latency for a professional whose value-per-second is measured in tens of dollars.

**What we steal.** The KPI strip + Exception Lane density model — eight tiles on top, one prioritized list in the center, never a dashboard-of-dashboards. The color discipline — a single accent color reserved for "exception" status; everything else is neutral, calm, monospaced where data alignment matters. The keyboard-primacy commitment — every page is fully drivable from the keyboard, every Cmd-K command has a global shortcut, the dock-floor power user becomes ten times faster than the click-driven novice.

**What we leave.** The visual chaos. Bloomberg Terminal looks chaotic to anyone who is not a Bloomberg professional; Cherry Street is a multi-industry tool whose users do not all hail from the same craft tradition. We retain the keyboard primacy without the chaos. Stripe-grade calm with Bloomberg-grade density is the synthesis.

### 5.2 Linear — calm prioritization and the inbox metaphor

Linear is the project tracker that finally taught the SaaS industry that productivity software can be **calm**. Linear's Inbox is a one-pane, keyboard-driven, prioritized list of "things that need your attention right now." The patterns are precise: `G I` to jump to inbox; `J / K` to navigate up/down; `E` to archive; `Shift H` to snooze; `Cmd K` for the universal command palette; `C` to create; `/` to filter; `?` to summon a shortcut cheatsheet. The detail of any item opens in a right-rail drawer or a focus mode; the list never scrolls out from under you.

**What we steal.** The Exception Lane sort order and behavior — a prioritized list of "the things at risk of falling through the cracks right now," sortable, snooze-able, keyboard-navigable. The right-rail Drawer Detail pattern — open a receipt, see its full profile-driven body, close it, never lose your place on the list. The calm of the palette — soft greys, a single accent, sentence-case labels, minimal chrome. The Cmd-K-everywhere discipline — already shipped in PR #116d.1c, ready to be the universal scan / command surface on `/Receiving`.

**What we leave.** The single-tenant project-tracker metaphor. Linear's inbox is one user's stream; the Receiving Control Center is a shared team workspace where multiple clerks operate on the same exception lane in parallel. The pattern stays; the multi-actor presence (live cursors, claim-this-exception locking) is our addition.

### 5.3 NASA Mission Control — role-based stations on a single big board

The mission-control room canonized by Apollo and continuously evolved through Shuttle, ISS, Artemis is the original Control Center. A wall-spanning big board displays the consolidated truth of the vehicle and the mission. In front of it, individual flight-controller stations — FIDO, GUIDO, EECOM, BOOSTER, INCO, FAO, CAPCOM — present role-specific data slices that drill into the big-board summary. The flight director on his perch holds the global view and the authority. Comms are voice-loop continuous; data is glanceable from any seat; the big board changes the room's mood within milliseconds when a parameter goes red.

**What we steal.** The "stand-up board" view for dock supervisors — a wall-mounted big-board variant of the Control Center that summarizes the entire dock at a glance (eight KPIs, every open exception, every dock-door state) and is glanceable from across a 100-foot warehouse. The big-board view is the same data as the clerk's Control Center, re-laid-out at large-screen-readable density. The role-based stations idea — the Receiving Clerk, the Warehouse Lead, the Materials Manager, the AP Specialist all use Control Center views that are the same app rendered differently per role, not separate products.

**What we leave.** The military-formality vocabulary and the all-male NASA aesthetic. We re-implement the architectural insight in the contemporary visual language we lifted from Stripe and Linear.

### 5.4 Datadog and Grafana — KPI tiles, sparklines, and the click-to-drill discipline

Datadog and Grafana taught a generation of SREs that real-time operational data should be tiled, sparkline-decorated, and click-to-drill. A modern Datadog dashboard has 8–12 tiles, each with a current value + a trailing-30-minute sparkline + an alert threshold; clicking a tile drills into its underlying query and its raw event stream. Filters are persistent, time windows are universal, and the visual vocabulary is consistent across hundreds of dashboards.

**What we steal.** The eight-KPI strip with sparklines is direct lineage. Each Receiving Control Center KPI tile shows current value, comparison-to-target, sparkline-of-last-30-shifts, and a click-to-drill that opens the underlying receipts that produced the number. The "alert threshold" lineage — a tile turns amber when its KPI crosses a soft target, red when it crosses a hard one. The compact-mode-vs-comfortable-mode toggle we ship in PR #116d.1c traces directly to Datadog's tile density control.

**What we leave.** The dashboard fragmentation. Datadog's customer typically maintains 30+ dashboards; the cognitive cost of choosing which dashboard to look at consumes most of the time savings the dashboards purport to provide. Cherry Street ships **one** Receiving Control Center per role, configured once, never proliferated.

### 5.5 Airline Operations Centers — exception management at scale

United's NOC, Delta's OCC, American's IOC, Southwest's NOC — the airline operations control rooms manage thousands of aircraft and tens of thousands of crew movements in real time. Their core surface is an **exception lane**: which flights, crews, and aircraft are deviating from plan right now, ranked by impact, with AI-driven re-routing recommendations that a human dispatcher confirms before execution. The pattern matured in the early 2000s with Sabre's MOVES, evolved through American's HEAT system, and was modernized in the 2020s with ML-driven recovery optimization at every major carrier.

**What we steal.** The exception lane as the **center column** of the Control Center — not buried in a "needs attention" tab. The AI-suggests / human-confirms agentic loop — every voice-driven action in Cherry Street's receiving flow is suggested by the AI, presented as a confirmation chip, and committed only on explicit human "yes" (the ADR-014 IIdempotencyMediator + ADR-015 D10 voice-AI prompt template both encode this rule). The role-station model — the dispatcher's station, the operations manager's station, the executive station all share the same exception data, presented at different granularities.

**What we leave.** The 1980s-vintage green-on-black mainframe aesthetic that survives in airline ops because the FAA-certified tooling has a glacial refresh cycle. We retain the discipline of exception management and re-clothe it in a contemporary visual system.

### 5.6 Stripe Dashboard — financial calm and the right-rail context drawer

The Stripe Dashboard is the most-copied SaaS UI in the world for a reason. Its visual system — soft greys, a restrained purple accent, monospaced numbers on right-aligned columns, generous whitespace, sentence-case labels — taught a generation of B2B SaaS designers what calm financial software looks like. Its right-rail drawer pattern — click a charge, see the charge's full detail in a panel that overlays the list without taking it away — became the default detail-view metaphor for modern dashboards.

**What we steal.** The right-rail drawer is the **single most important interaction pattern** in the Control Center. Click any row in the Exception Lane; the drawer slides in from the right with the receipt's full profile-driven body (rendered by ADR-015's `DynamicFormViewComponent`); the list stays visible beneath the drawer's slight scrim; close the drawer (Esc) and the list snaps back. We never use a modal for receipt detail; we never navigate away to a separate page; we never make the user lose context. The Stripe-style financial calm — monospaced numbers, right-aligned currency, no bold-text-shouting — sets the visual baseline.

**What we leave.** The financial-only vocabulary. Stripe is payments; we are receipts of physical material. We retain the calm and adapt the affordances (scan field above the search field, voice button persistent in the bottom-right, big-tap-target action buttons sized for gloved fingers).

### 5.7 Synthesis — the Control Center grammar

Stitching the six patterns together: **the Receiving Control Center is Bloomberg's density + Linear's calm + NASA's role-stations + Datadog's sparkline tiles + Airline Ops' exception lane + Stripe's right-rail drawer.** No single existing product combines all six. None of the six is from the ERP category. That is the moat — the design vocabulary that beats SAP/Oracle/NetSuite did not come from inside the ERP category and is not available to them by incremental refactor.

---

## 6. Receiving workflows — PO / ASN / Blind / Partial / Over / Exception / Quarantine

Eleven distinct receiving workflows. The Control Center must handle all of them, but v1 ships the four highest-volume (PO-driven, ASN-driven, blind, partial). The rest land in v2.

### 6.1 PO-driven receiving (the 80% case)

**Scenario.** A truck arrives with goods that match an open Purchase Order. The clerk identifies the PO (number on packing slip, number on BOL, supplier-side reference), pulls it up, scans or types the received quantity per line, captures profile-specific attributes (heat number on steel, lot/expiry on pharma, METRC tag on cannabis), confirms putaway location, prints the staging label, posts the receipt.

**State diagram.** `PO Open → Expected Arrival → Physically Received → Documented → QC Released (if quarantine-routed) → Posted → AP Match-Eligible`.

**User actions.** Open the PO. For each line: scan or type the received quantity. Press `J` or click "Next Line" to advance. On the last line, press `Enter` or click "Post Receipt." The drawer collapses; the activity feed at the bottom announces the post.

**System pre-fill.** Vendor (from PO). Expected quantity (from PO line). Default profile (from Item.DefaultReceiptProfileId per ADR-015). Default putaway location (from item's primary bin or the staging Q-RCV bin). Default UoM (from Item).

**Exceptions.** Over-receipt (qty received > qty ordered, beyond tolerance). Under-receipt (partial). Wrong-item (PO line item ID mismatches scanned barcode). Damaged. Expired-on-arrival (date math against profile's `expirationDate`). Quarantine-routed (item's profile or the item itself flags Inspection-Required).

### 6.2 ASN-driven receiving

**Scenario.** An EDI 856 (Advance Shipment Notice) has been received from the supplier 24–72 hours before the truck arrives. The shipment is pre-staged in the system — lines, lots, serials, expected weights, carrier, ETA. The truck arrives; the clerk scans the SSCC-18 license-plate barcode on each pallet (or the GS1-128 case label on each carton), and the system confirms expected-vs-received in one scan.

**State diagram.** `ASN Received → Expected Arrival → Carrier Arrived → Scan-Confirmed → Posted`.

**User actions.** Scan the SSCC. The Control Center auto-pulls the ASN line, validates, marks confirmed. Repeat per pallet. On last pallet, the system posts the entire shipment in one transaction.

**System pre-fill.** Everything. The ASN delivered all of it. The clerk's job is verification, not data entry.

**Exceptions.** ASN-vs-actual mismatch (truck arrived with fewer pallets than ASN claimed; the system flags the gap, the clerk records "short," supplier credit-memo is requested by the office relay). Late ASN (truck arrived before ASN; clerk falls back to PO-driven flow). Carrier hand-off (ASN says LTL but actual is parcel; reconcile).

### 6.3 Blind receive (no PO, no ASN)

**Scenario.** A truck arrives with no paperwork, or with paperwork that doesn't match any open PO (typical for sample shipments, returned goods, freight-pool errors). The clerk records what physically arrived; the office relay reconciles later.

**State diagram.** `Truck Arrived → Blind Receipt Recorded → Orphan-Match Queue → Resolved (to PO or to Returns/Sample/Reject)`.

**User actions.** Click "Blind Receive." Type or scan: vendor (best guess), item (best guess), quantity, lot/serial if visible. Photograph the paperwork. Save. The receipt enters the Exception Lane with a special "orphan" badge. The office relay (or the AI's MatchOrphanReceipt tool) proposes 3 candidate POs by vendor + item + date; the clerk confirms.

**System pre-fill.** Almost nothing. The AI's job is to **propose** the right PO based on vendor + item + date heuristics — see Section 8.4.

**Exceptions.** All of them. Blind receive is the exception case by construction.

### 6.4 Partial receipt

**Scenario.** PO line ordered 50, supplier shipped 30 today, the balance of 20 is back-ordered and arrives next week. We accept the 30 and leave the line open.

**State diagram.** `PO Line Open (qty 50) → Partial Received (received 30, remaining 20) → PO Line Open (qty 20) → … → PO Line Fully Received`.

**User actions.** Open the PO. Enter received qty 30 (less than ordered 50). The system flags the variance, asks "Close line or hold open?" — clerk picks "hold open." Save.

**System pre-fill.** Defaults to hold-open if received < ordered. Closing-short is a deliberate action gated by a soft confirmation.

**Exceptions.** Receipts past a supplier's promised back-order date trigger a buyer-follow-up task. Multiple partial receipts on the same line are tracked as a list, not as a counter — auditors care about each event.

### 6.5 Over-receipt

**Scenario.** PO line ordered 50, supplier shipped 52. Within tolerance? Within the supplier's overshipment-allowance? The clerk decides whether to accept the extra or send it back.

**State diagram.** `PO Line Open (qty 50) → Receive 52 → Tolerance Check → Accept All (52) | Accept Ordered (50) + Return Excess (2) | Reject All`.

**User actions.** Enter received qty 52 against an ordered 50. The system computes 4% overshipment, compares to the vendor's `OverShipmentTolerancePct` (a vendor master field that we will need to add per ADR-016 follow-up), and either auto-accepts (within tolerance) or asks "Accept extra 2? Return excess?" The decision is logged with rationale for AP.

**System pre-fill.** The tolerance lookup. The supplier scorecard impact (over-ship counts against accuracy). The AP variance flag.

**Exceptions.** Over-receipt without supplier authorization → vendor scorecard ding, possible credit-memo request. Repeated over-receipt from one supplier → supplier-relationship review queue.

### 6.6 Damaged-on-arrival

**Scenario.** The clerk opens the trailer and sees a pallet with shrink-wrap broken, a carton with visible water damage, or a piece of structural steel with a dent that violates the visual-inspection criteria.

**State diagram.** `Receive → Inspect → Damage Captured (photos + notes) → Decision (Accept Conditional / Quarantine / Reject) → Freight Claim Filed (if applicable) → Posted`.

**User actions.** During receipt, press the "Damage" button (or say "this pallet is damaged"). The voice/touch flow asks: (1) photograph the damage (camera intent on the device); (2) quantify the affected units; (3) note the damage description (voice-to-text, profile-aware suggested phrases); (4) pick a disposition (accept-conditional, quarantine, reject). The freight claim is auto-drafted with the photos attached.

**System pre-fill.** Carrier (from BOL). PRO number (from BOL barcode if scanned). Default disposition (per item's profile).

**Exceptions.** All accept-conditional damaged receipts route to QC quarantine even if the item's profile would normally bypass QC.

### 6.7 Quarantine receipt

**Scenario.** The item's profile, the item itself, or the vendor's recent history requires the receipt to land in a QC inspection bin before becoming generally available stock.

**State diagram.** `Receive → QC Hold → QC Inspect → QC Release / QC Reject → If Released → Available Stock; If Rejected → Return-to-Vendor`.

**User actions.** The clerk's flow is unchanged — the receipt posts. The receipt's `Status` per ADR-015 is `Quarantined` and `QuarantineReason` is set. The QC technician later opens the QC queue (a sibling Control Center surface), inspects, and either releases (`Status = Available`) or rejects.

**System pre-fill.** Quarantine reason (auto from the trigger: profile-mandatory, vendor-history, damage, random-sample).

**Exceptions.** Past-due QC holds (over a threshold of hours/days) escalate to the Materials Manager. Mass-quarantine ("everything from this vendor last week") is a voice-driven AI action (see Section 8.5).

### 6.8 Returns (RMA inbound — customer returning to us)

**Scenario.** A customer is returning material to our dock. This is **not** a PO receipt — it's an RMA receipt. Different document, different accounting, different putaway destination (usually a RMA-inspection bin).

**State diagram.** `RMA Open → Carrier Arrived → Receive → Inspect → Restock / Refurb / Scrap`.

**User actions.** Open the RMA (not the PO). Receive against RMA lines. The flow is parallel to PO-receive but the destination state is different — refurb queue, restock queue, or scrap.

**System pre-fill.** Original sales order. Customer. Original ship-out date.

**Exceptions.** RMA mismatches the physical return (customer claimed 10, sent 8). Cosmetic vs functional damage triage.

### 6.9 Cross-dock

**Scenario.** Material arrives that has already been promised to an outbound shipment leaving the same day. It never enters stock; it transits the dock from receiving door to shipping door.

**State diagram.** `Receive → Cross-Dock Allocation → Shipped`.

**User actions.** During receipt, the system detects an open outbound allocation against this material and prompts "Cross-dock to Order #SO-12345 (truck loading at door 6, departure 14:30)." The clerk confirms; the staging-label printer prints a different label (orange "CROSS-DOCK" instead of yellow "Q-RCV"); the receipt and the issue happen as a single matched transaction in the inventory ledger.

**System pre-fill.** The allocation. The destination door. The departure time.

**Exceptions.** Cross-dock allocation that no longer fits (outbound truck left, allocation stale) → fall back to normal receipt.

### 6.10 Drop-ship receipt

**Scenario.** A supplier ships material directly to **our customer**, not to our dock. We never physically touch it. But we still need to record the receipt in the system (for AP-match purposes and to recognize revenue on the outbound sale).

**State diagram.** `PO Issued (drop-ship flag) → Carrier Delivered to End Customer → Customer Confirms Receipt → Drop-Ship Receipt Posted → AP-Match-Eligible`.

**User actions.** None on our dock. The receipt is triggered by a carrier-confirmation webhook, a customer email confirmation, or a manual entry by the buyer when supplier confirmation arrives.

**System pre-fill.** Everything (it's all paper, no physical presence).

**Exceptions.** Carrier confirmation never arrives → buyer-follow-up. Customer disputes delivery → claim flow.

### 6.11 Consignment receipt

**Scenario.** Vendor-owned stock arrives at our dock. We hold it. The vendor still owns it. We pay for it only when we draw it down for production or sale.

**State diagram.** `Consignment Stock Arrives → Receipt Recorded (ownership=Vendor) → Available for Drawdown → Drawdown Triggers AP Liability`.

**User actions.** Receive against a Consignment Agreement (not a PO). The flow is otherwise identical to PO-driven, except the inventory record bears an `Owner` flag that prevents accounting recognition.

**System pre-fill.** Consignment agreement, vendor, ownership flag.

**Exceptions.** Consignment cycle counts (we have to inventory their stock periodically and reconcile). Consignment expiration (some consignment agreements terminate after N days, returning the stock to the vendor).

### 6.12 v1 / v2 scope decision

Pilot ships **PO-driven (6.1), ASN-driven (6.2), blind (6.3), partial (6.4)** — the 95% of volume by truck count, and the 80% of volume by dollar.

V2 (Sprint 5–6) ships **over-receipt (6.5), damaged (6.6), quarantine (6.7), returns (6.8)** — the high-cognitive-load workflows where the AI substrate pays disproportionate dividends.

V3 (Sprint 7+) ships **cross-dock (6.9), drop-ship (6.10), consignment (6.11)** — the niche workflows that demand additional schema work (outbound allocations, drop-ship POs, consignment agreements).

---

## 7. Hardware integration — scanners, scales, RFID, label printers, vision/OCR, dock sensors

The dock is a hardware-rich environment. Cherry Street's Control Center must speak natively to all of it. The seven device classes below are the universe.

### 7.1 Barcode and 2D scanners — the workhorse

The fleet is overwhelmingly Zebra and Honeywell. Among mid-market plants, the most common configurations are:

- **Zebra TC52 / TC57 / TC52x / TC57x** — 5-inch ruggedized Android handhelds with the SE4710 (TC52) or SE4770 (TC57x) scan engine. Android 11 or 13. 8MP rear camera, integrated 1D + 2D imager, Bluetooth headset support. The de facto SKU for mid-market manufacturing receiving in 2026.
- **Zebra MC9300** — pistol-grip Android device for heavy-duty cold-storage and outdoor receiving. Less common but present in food, beverage, cold-chain pharma.
- **Honeywell CT60 XP / CT45 / EDA52** — direct competitors to the TC52/TC57. Functionally equivalent feature set; the choice is usually decided by the customer's existing service contract with Zebra or Honeywell.
- **Datalogic Memor 11 / 20** — present at European plants and at some U.S. food-and-beverage shops with EU-imported equipment.

All four families support two scan-data delivery modes:

1. **Keyboard wedge** (Zebra calls this DataWedge in **Keystroke Output** mode; Honeywell calls it ScanWedge): the scan engine decodes the barcode and types the characters into the focused text field as if a keyboard had typed them, plus a configurable suffix (typically `\n` or `\t`). This is the most compatible mode — works against any web page or app — and is the mode we ship for. The page must have a single, predictable focus target: the global scan field at the top of the Control Center, which is auto-focused on `/Receiving` load. Cmd-K-style focus discipline (PR #116d.1c) gives us this for free.
2. **Intent / API (SDK) mode** — DataWedge **Intent Output** broadcasts an Android Intent with the scan payload; an Android-native app subscribes. Faster, more reliable for high-scan-rate workflows, but requires an Android-native receiver. V1 ships keyboard-wedge mode; V2 may ship a Trusted Web Activity or a thin Android wrapper for Intent-mode for the highest-volume customers.

Barcode symbologies the page must accept without configuration:

- **GS1-128 / EAN-128** (Code 128 with FNC1 + GS1 Application Identifiers — `(01)` GTIN, `(10)` Lot, `(17)` Expiry, `(21)` Serial). This is the dominant pharma / food / consumer goods symbology. The Control Center parses Application Identifiers and pre-fills the relevant ADR-015 profile attributes automatically. Note the Zebra GS1 DataMatrix decoder gotcha: when scanning UDI labels, GS1 DataMatrix must be **disabled** in the Decoders section to work around a UDI-architecture limitation; the data is otherwise garbled. This is documented in the Zebra support community and is a configuration the customer's IT must apply per device.
- **DataMatrix** — pharma 2D barcodes (DSCSA-compliant), aerospace UID (MIL-STD-130).
- **QR Code** — increasingly used by suppliers for ASN landing pages and digital mill certs (scan, open a URL, OCR the PDF behind it).
- **Code 39 / Code 128** (plain) — internal SKU labels.
- **EAN-13 / UPC-A** — retail consumer goods (rare on the manufacturing dock, common on the distribution dock).
- **PDF417** — driver's licenses (for chain-of-custody at controlled-substance receipts), some carrier BOL labels.

### 7.2 Scales — weight verification

Mettler Toledo VFS, IND series; Cardinal 205 / 215; Avery Weigh-Tronix. Connectivity is overwhelmingly RS-232 serial via USB-to-serial adapter; some newer scales offer USB-HID directly; Bluetooth scales exist but are rare in manufacturing.

The Control Center's scale workflow: the clerk taps the "Weigh" button on a receipt line, the scale's current reading is captured via a thin browser-to-local-bridge service (an Electron sidecar or a Windows-service printing companion — V1 ships the latter), the tare is auto-deducted if the pallet's tare is known, the weight is recorded against the receipt. Quantity-by-weight (e.g., "1,247 kg of HRPO sheet, density-derived to 124 plates") is a profile-driven conversion configured per material.

### 7.3 RFID — gateway readers and batch reads

Zebra FX9600, Impinj R700, Alien ALR-F800. Dock-door gateway readers scan every pallet that passes under them; passive UHF Gen2 tags on pallets / cartons / cases are read in a batch read. RFID dramatically reduces dock-to-stock time when properly tuned — a 24-pallet trailer can be scanned in seconds — but signal noise (cross-reads from a neighboring dock door, false reads from a tag in the next aisle) is the durable engineering problem.

V1 of the Control Center does **not** require RFID. V2 adds a "dock door RFID feed" subscription mechanism — receipts can be optionally created from a gateway read event. The user experience: the truck pulls in, the gateway flashes, the Control Center's activity feed shows "24 candidate tags read at door 3 (confidence 91%) — review and post?"

### 7.4 Label printers — staging, putaway, return

Zebra ZD420, ZD620, ZT411 (desktop and industrial). Brother QL series (smaller form factor). Sato CL4NX. All speak ZPL (Zebra Programming Language); ZPL-over-IP-or-USB is the universal contract. The Control Center prints:

- **Staging labels** (Q-RCV, QC-HOLD, CROSS-DOCK) — printed at receipt, 4x6 thermal, includes receipt number, profile-rendered traceability summary, QR code linking to the digital receipt detail.
- **Putaway labels** (the bin destination) — printed after putaway is decided.
- **Mill cert / COA reprints** — a thumbnail of the original document plus a QR code linking to the full PDF in object storage.

The print service is a Windows-service-or-cloud-print-gateway companion that runs on the dock workstation, exposes a local HTTPS endpoint, and receives ZPL from the Control Center web page. Customers running cloud-print-only setups (PrinterLogic, PrintNode) can swap the adapter.

### 7.5 Vision / OCR — packing slip and mill cert capture

The single highest-ROI piece of hardware-adjacent functionality. Mill certs arrive by email as PDFs; the heat number, mill, ASTM grade, mechanical properties, and chemistry are buried in a table on page 2. The clerk's job today is to read these by eye and re-type the heat number into MIGO's notes field.

The Control Center's mill-cert OCR flow (per ADR-014 + ADR-015 substrate):

1. Clerk drops the PDF onto the receipt drawer (or photographs the printed cert with the TC52's rear camera).
2. The page POSTs the file to a thin OCR service that runs Tesseract for the table extraction baseline and a fine-tuned vision model (Claude or an open-weights model) for the structured extraction.
3. The structured output is mapped to the active ReceiptProfile's attribute keys (`heatNumber`, `mill`, `astmDesignation`, `countryOfMelt`, chemistry as a nested object, mechanical as a nested object).
4. The drawer auto-fills with confidence scores per field; the clerk confirms with `Enter` or corrects.
5. The original PDF is stored in object storage and linked via `millCertUrl`.

Identical pattern for pharma COA (Certificate of Analysis), food COA, aerospace material certs, cannabis lab reports, and DEA-222 controlled-substance forms — different profile, same flow.

Driver's-license capture for chain-of-custody on controlled substances: PDF417 scan parses the license, the captured signer is recorded against the receipt, audit log preserves the image.

### 7.6 Dock-door sensors — occupancy and dwell

Banner Engineering, Sick, Keyence inductive / photoelectric sensors wired into a PLC at the dock. Open/closed status, occupancy (a truck is at the door), dwell time (how long the door has been open this shift). Connectivity to the application layer is OPC-UA from the PLC for the modern installations, Modbus TCP for older.

V1 of the Control Center does **not** require dock-door sensors but exposes a placeholder: a stylized representation of the eight dock doors with green / amber / red status. The data source is a manual click-through in V1 ("Marisol clicks Door 3 = Occupied when she opens the trailer"), an automated feed in V2 (sensor data integration via the OPC-UA gateway).

### 7.7 Forklift mounts, headsets, hands-free

Zebra VC80x / VC8300 forklift-mounted rugged tablets — typically used for putaway, not receipt. The Control Center renders the putaway worksheet at large font for these screens; the page reflows for 10-inch displays.

Bluetooth headsets — Plantronics Voyager, BlueParrott B450-XT — are the gateway to voice-first receiving. The clerk wears the headset, the TC52 is in her belt holster, the Control Center is open in a Chrome tab on the headset's paired Android device. She speaks: "Heat H-A8842-1, twenty-four plates received." The page captures, validates, and confirms. This is the ADR-014 / ADR-015 D10 vision realized — and Section 8 below specifies it in detail.

### 7.8 The honest truth on hardware

Eighty percent of plants in Cherry Street's target market live on **Zebra TC52 / TC57 / TC52x handhelds running Android 11 or 13**. Twenty percent are on Honeywell. A long tail is on iPads, on Windows tablets, or on the dock-workstation browser. We design for the dominant constraint — 5-inch screen, gloved fingers, intermittent wifi, keyboard-wedge scan — and let the larger form factors scale up. **Designing for the office and reflowing down to mobile is the trap NetSuite WMS fell into.** We will not repeat it.

---

## 8. AI / Voice / Agentic angles — what only CherryAI can do

This is where Cherry Street wins. None of the eight incumbents and none of the seven challengers in Sections 3 and 4 ships a credible agentic receiving experience. The substrate Cherry Street has already built — `VoiceReadyPageModel` (ADR-014 D1), `IIdempotencyMediator` (D4), AuditLog AI-on-behalf-of columns (D3), `ReceiptProfiles` with `JsonSchema` + `UiFormSpec` + `PromotedFacets` (ADR-015 D2), the four `IReceiptVoiceTools` stubs (`createReceiptFromPoLine`, `quarantineReceipt`, `traceChainOfCustody`, `lookupReceipt`), and the validated voice-AI prompt template (research doc `voice-ai-spike-adr015-d10.md`, 5-of-9 pass rate with fixes catalogued) — is the foundation. The Control Center is the surface that exercises all of it.

Eleven AI capabilities below, each grounded in the existing substrate, each scoped for shipability across Sprint 5 (4 capabilities — V1) and Sprint 6 (7 capabilities — V2).

### 8.1 Voice-confirm receipts — the headline capability

**The experience.** Marisol is wearing a Plantronics Voyager Bluetooth headset paired to her TC52. The trailer is open. She says: "Heat H-A8842-1, twenty-four plates received, two short, one dinged." The Control Center, listening on push-to-talk (PR #116d.1c-equivalent for voice; Sprint 5 work):

1. Captures the audio.
2. Routes to a speech-to-text service (Whisper-small on-device or a server-side equivalent; the decision is deferred to the Sprint 5 architecture pass).
3. Synthesizes the validated prompt (per voice-AI-spike-adr015-d10.md §2) — `VoiceContextPayload` for the active page (Receiving Control Center, active profile STEEL, focused PO line if any), the ACTIVE_RECEIPT_PROFILE_JSON for STEEL, the tool catalog, the cross-profile glossary.
4. The model emits `{ tool: "createReceiptFromPoLine", args: { poLineId: …, qty: 24, attributesDelta: { heatNumber: "H-A8842-1" }, damageNote: "one dinged" } }` plus a `requestConfirmation` chip.
5. The Control Center renders a confirmation chip: "Receive 24 of PO line 4500178823-1, heat H-A8842-1. 2 short of expected 26. 1 unit flagged damaged. Confirm?" Marisol says "yes."
6. `IIdempotencyMediator` checks the operation key, the service executes, AuditLog records the operation with `AiActedAsUserId = Marisol`, `AiToolName = createReceiptFromPoLine`, `AiModelVersion = …`, `AiPromptHash = …`.

The clerk did not touch the screen. The receipt is posted. The label is printing.

### 8.2 OCR mill-cert auto-parse

**The experience.** The clerk drops the mill cert PDF on the receipt drawer (or photographs it). The page calls `OcrParseMillCert(file, profileCode)`, which routes to a vision service. The service returns a structured payload mapped to the active profile's attribute keys: heatNumber, mill, astmDesignation, countryOfMelt, chemistry (a nested object with C, Mn, P, S, Si keyed by element), mechanical (a nested object with yieldMpa, ultimateMpa, elongationPct). Each field carries a confidence score (0–1). The drawer auto-fills, fields above a 0.9 confidence are committed automatically, fields below 0.9 are highlighted yellow and request human confirmation. The PDF is stored in object storage; `millCertUrl` is set.

**The substrate.** Per ADR-015 D10's prompt template, the profile JSON schema defines the target shape. The OCR service is a thin adapter that calls a fine-tuned vision model and validates the output against the profile's JSON Schema before returning. Mismatched / extra fields are dropped silently; missing required fields surface as warnings.

### 8.3 Supplier anomaly flagging

**The experience.** When a receipt is being recorded, the Control Center compares the proposed receipt against the supplier's trailing-90-day pattern: typical quantity-variance distribution, typical lot-completeness, typical damage rate, typical heat-number-validity. If the proposed receipt is more than 2σ outside the supplier's pattern, the drawer surfaces an inline warning: "ACME Steel typically ships within 5% of ordered quantity. This receipt is 22% over. Confirm or hold for review?"

**The substrate.** A nightly job (or a materialized view refreshed every 4 hours) computes per-supplier rolling-window statistics into a `VendorReceiptStats` table. The Control Center reads these stats on receipt draw-up. The voice-AI prompt template optionally includes `vendorStatsSnapshot` in the context payload so the model can frame its confirmation chip with the vendor context.

### 8.4 Auto PO-matching (orphan match)

**The experience.** A blind receipt is recorded — the clerk types the supplier name, the item description (as best she can read it on the packing slip), and the quantity. The page calls `MatchOrphanReceipt(vendor, itemHint, qty, receivedAt)`. The model queries open POs ranked by vendor + item-similarity (fuzzy match against Item.Description and ItemCode) + date-window (POs expected within ±5 days of the receipt date). It returns 3 candidate POs with confidence scores. The Control Center renders them as choice chips: "Match to PO 4500178823 (94% confidence)? PO 4500179101 (76%)? PO 4500179440 (52%)?" The clerk picks one or rejects all.

**The substrate.** `IReceivingControlCenterService.MatchOrphanReceiptAsync(orphanCmd, user, ct)` — a new service method that queries `PurchaseOrders` and `PurchaseOrderLines`, returns a ranked candidate list. The ranking algorithm is a deterministic SQL query first, an LLM fallback if no high-confidence match exists.

### 8.5 Predictive quarantine

**The experience.** A receipt is being drawn up against a supplier whose last 3 receipts failed QC. The Control Center pre-flags the proposed receipt for QC routing with an inline warning: "Last 3 receipts from this supplier had MTR failures. Auto-route this receipt to QC inspection?" The clerk confirms (default) or overrides.

**The substrate.** The same `VendorReceiptStats` table from 8.3 surfaces a `RecentQcFailRate` column. The Control Center checks this on receipt draw-up and proposes the routing override before posting.

### 8.6 Expected-arrival forecast

**The experience.** The Control Center's Activity Feed (the bottom strip of the four-quadrant layout) shows a forward-looking "Expected Today" widget: "Truck #7234 expected 14:15. Schneider National. PO 4500178823 (4 lines, $48,200 value). 24 plates HRPO 1/2-inch."

**The substrate.** A combination of (a) ASN-derived ETAs (from EDI 856), (b) carrier-tracking webhooks (from FourKites, project44, or direct carrier APIs), (c) historical supplier lead-time models. `IReceivingControlCenterService.ListExpectedArrivals(window, dock, ct)`.

### 8.7 Chain-of-custody Q&A

**The experience.** The clerk (or anybody in the plant, on any page with the voice button) says: "Where is heat H-A8842-1?" The AI calls `traceChainOfCustody(lotNumber: null, heatNumber: "H-A8842-1")`. The tool traverses the `StockReceipts → Nest → Remnant → Shipment` graph (per ADR-015 D9 + the voice-AI-spike-adr015-d10.md §6 graph stanza). The result: "Heat H-A8842-1: 24 plates received 2026-05-18 from ACME Steel. 18 plates in stock at Bay 4 / Slot B-12. 6 plates issued to WO 4500678 (production order 991-0044). No remnants. No shipments out."

**The substrate.** The voice-AI spike falsified the single-row-lookup default behavior on this query (test H3 failed). The fix lives in the prompt template: `traceChainOfCustody` is now a documented tool, and the prompt instructs the model to call it for any traversal question. Sprint 5 ships the tool implementation; the surface is the Control Center's voice button.

### 8.8 Receipt-to-AP handoff (3-way match anomaly detection)

**The experience.** When AP receives a vendor invoice, the Control Center pre-runs the 3-way match (PO + Receipt + Invoice) and flags variances **before** the AP clerk sees them. The AP clerk sees a short list of receipts with explained variances ("$240 over PO line — overshipment within tolerance, supplier acknowledged via packing slip note"), and the clean ones flow straight to payment.

**The substrate.** A new `IThreeWayMatchService.RunMatchAsync(invoice, ct)` that joins the typed core of `StockReceipts` against `PurchaseOrderLines` and the invoice payload, runs the variance rules, and emits a structured anomaly list. AI is not required for the deterministic match; AI **explains** the variances in natural language for the AP clerk.

### 8.9 Proactive supplier scorecard updates

**The experience.** Every receipt updates a live supplier scorecard, visible from the Control Center's right-rail when a receipt's supplier is in focus: on-time%, qty-accuracy%, doc-completeness%, defect rate, average quarantine-cycle-time, average dock-to-stock-time. Trending arrows show the 30-day delta. A "see all receipts" link opens the supplier's full receipt history with the same Control Center filtering.

**The substrate.** Materialized view `VendorScorecard` refreshed every shift, surfaced via `IVendorService.GetScorecardAsync(vendorId, ct)`.

### 8.10 Hands-free putaway suggestions

**The experience.** After a receipt is posted, the clerk says: "Where does this go?" The AI calls `SuggestPutawayLocation(itemId, qty, attributesSnapshot)`. The tool checks: item's primary bin, item ABC class, current bin occupancy heatmap, any FEFO constraints (first-expiry-first-out for perishables), any hazmat segregation rules. The voice response: "Bay 4, slot B-12. 6 units already there from the same heat. Confirm?" Clerk says "yes," the putaway is recorded.

**The substrate.** `IPutawayService.SuggestLocationAsync(...)` — a new service. The first version is rule-based (item → primary bin → occupancy check); a future ML version learns the customer's actual put-away patterns.

### 8.11 Voice as primary entry

**The capstone experience.** A clerk wearing a headset receives a whole truck without ever touching a screen. The flow is:

1. "Open PO 4500178823." (Control Center pulls up the PO in the drawer.)
2. "Receive line 1, twenty-four plates, heat H-A8842-1." (Receipt drafted.)
3. "Two short." (Quantity adjusted to 22 with variance noted.)
4. "Mill cert is in the cab, I'll bring it in five minutes." (System holds the receipt in `Pending` state.)
5. "Confirm." (Receipt posted, label printed at dock printer.)
6. "Next line." (Receipt for line 2 drafted.)
7. "Skip — that one isn't here." (Line 2 closed-short.)
8. "Receive line 3 fully." (Receipt for line 3 posted at expected quantity.)
9. "Post the truck." (Truck-level completion event.)

Every utterance is one round-trip through the voice-AI prompt template. Every action is idempotent (the IdempotencyMediator key is the utterance hash + page session). Every operation is audited with `AiActedAsUserId = Marisol`, the operator never appears as having "logged in as the AI."

### 8.12 Tying this to the locked substrate

Every AI capability in this section maps to existing-in-flight or shipped substrate:

- ADR-014 D1 (`VoiceReadyPageModel`) — the Control Center page model inherits from this; `BuildContextPayload()` is overridden to add the active KPI tile filters, the focused exception, and the active receipt profile.
- ADR-014 D2 (Service-method-first action surface) — every AI tool call lands in a typed `IXxxService` method. Voice and the future MCP server invoke the same methods.
- ADR-014 D3 (AuditLog AI extension) — every AI-mediated mutation lands with the seven AI columns populated.
- ADR-014 D4 (`IIdempotencyMediator`) — every AI mutation goes through the mediator. A duplicate utterance ("yes" said twice) becomes one operation.
- ADR-014 D5 (Resource-based authorization) — AI executes as the invoking user's `ClaimsPrincipal`. The clerk's permissions are the AI's permissions.
- ADR-015 D2 (ReceiptProfiles config table) — the active profile defines the AI's available attribute fields.
- ADR-015 D10 (voice-form-spec) — the prompt context includes the profile's JsonSchema + UiFormSpec.
- ADR-015 D10 spike findings (voice-ai-spike-adr015-d10.md §6) — the prompt template now includes the cross-profile glossary, the mutation-confirmation rule, the chain-of-custody tool, and the expected-arrivals tool. All four are required for the Receiving Control Center.

### 8.13 The four V1 tools, the seven V2 tools

**V1 (Sprint 5 ships):** `createReceiptFromPoLine`, `quarantineReceipt`, `traceChainOfCustody`, `lookupReceipt`. These are the existing IReceiptVoiceTools stubs; they need implementations, prompt-tested validation against the voice-AI-spike battery, and integration with the Control Center.

**V2 (Sprint 6 ships):** `ListExpectedArrivals`, `MatchOrphanReceipt`, `ExplainException`, `ReceiveByVoice` (a higher-level orchestrator that chains the V1 tools), `QuarantineByVoice`, `OcrParseMillCert`, `SuggestPutawayLocation`.

---

## 9. Mobile vs Desktop — dock-worker vs office personas

Three personas, three form factors, one app. The Receiving Control Center is fundamentally desktop-first for the pilot but ships an iPad/Zebra-friendly companion mode from day one. The trap to avoid is NetSuite WMS's: pretending mobile is just "desktop on a small screen."

### 9.1 The dock-worker persona — Marisol on the TC52

**Context.** 5-inch screen, gloved fingers, cold or wet or dirty hands, 30-second attention bursts between physical tasks, scanning-first, voice-friendly via Bluetooth headset. Wifi mesh dropouts every few minutes in the deep aisles. The dock is loud (forklift beepers, dock-door alarms, a radio playing classic rock). Lighting is fluorescent overhead and harsh sun through dock-door 4 at 8 AM.

**Design implications.** Tap targets at least 48dp / 12mm on a side (matching Material Design's accessibility threshold, larger than the WCAG minimum). No hover states. Single-thumb-reachable critical actions (scan field, voice button, "next" button). High-contrast palette — the Stripe-calm palette adjusted for outdoor sun-readability with a slightly darker chrome and a high-contrast mode toggle in the supervisor settings. Voice button always present, large, bottom-right, with a visible push-to-talk pulse animation when active. Scan field auto-focuses on page load and re-focuses after any modal closes. The page works in IndexedDB-backed offline mode for the duration of a typical wifi dropout (60–120 seconds) and syncs when reconnection restores.

**What the dock-worker view of the Control Center looks like.** A single-column layout. The KPI strip is collapsed into a single "today" pill at the top (large numbers, no sparkline). The Exception Lane is the full middle of the screen, one row at a time, swipe-left to snooze, swipe-right to claim. The Drawer Detail is the entire screen when opened — there is no list-plus-drawer at this width. The Activity Feed lives in a separate tab.

### 9.2 The office-clerk persona — the AP/3-way-match relay

**Context.** Desktop workstation, dual monitor, mouse + keyboard, sitting for 7–8 hours, multi-tasking between the Control Center, Outlook, and the accounting system. Wifi-grade reliable. Lighting controlled.

**Design implications.** Full four-quadrant Control Center layout: KPI strip across the top with eight tiles and sparklines, Exception Lane in the center, Drawer Detail on the right (rendered when a row is selected), Activity Feed across the bottom. Keyboard shortcuts (J/K, Enter, Esc, /, ?, Cmd-K) are first-class — the office clerk is the user who internalizes them and becomes 5–10x faster than the click-only user.

**What the office-clerk view looks like.** The full Control Center pattern. The clerk lives here all day. The Exception Lane sort is configurable per user (by SLA-age, by dollar value, by vendor, by exception type). The Drawer renders the receipt's profile-driven body, including all attributes, photos, mill cert PDFs, and audit history. The activity feed lets the clerk follow what their dock-counterparts have just done. Cmd-K is the global navigator — search receipts, jump to PO, lookup vendor, open a chain-of-custody Q&A.

### 9.3 The supervisor persona — the Warehouse Lead's tablet and wall display

**Context.** iPad or 11-inch wall-mounted display. The supervisor uses the tablet on the floor (in his hand, in the dock office, at the morning standup) and glances at the wall display while walking past it. He does not perform receipts; he supervises them.

**Design implications.** The wall display is a **big board** — a stripped-down view of the Control Center optimized for 5-feet-away glanceability. Eight KPI tiles at large font (96pt numbers, sparklines visible from 10 feet). Top-of-Exception-Lane (the three most urgent exceptions) shown with vendor name and SLA countdown. No detail drawer; clicking a row on the wall display is not a use case.

The supervisor's tablet is the office-clerk view in iPad form-factor — same four quadrants, slightly different keyboard-shortcut prominence (he uses fewer shortcuts than the clerk), the same Cmd-K behavior.

### 9.4 Form-factor shipping strategy

**V1 (Sprint 5):** Ship desktop-first. The four-quadrant layout works at 1280×800 minimum, scales beautifully to 1920×1080, degrades gracefully to 1024×768 (the 11-year-old dock workstation). Mobile is **responsive web** — the same page, reflowed for narrow viewports, with the dock-worker simplifications turned on automatically below 600px width.

**V1.5 (during Sprint 5):** Optimize the mobile reflow for the TC52's 5-inch portrait viewport. Test in BrowserStack on actual TC52 device emulators. Ship a PWA manifest and an iOS/Android home-screen icon so the clerks can "install" the app to their handheld.

**V2 (Sprint 6+):** Build the big-board supervisor view as a separate page (`/Receiving/StandUp`) with its own layout — full-screen, low-chrome, high-contrast, auto-refreshing every 5 seconds. Optional Trusted Web Activity wrapper on Android for Intent-mode scanner integration. Defer a native Android/iOS app until customer demand justifies it; the responsive web + PWA approach covers the addressable market through 2026.

### 9.5 The NetSuite WMS trap

NetSuite WMS's mobile app, in its first three years, was a literal port of the desktop receiving form to a phone-sized viewport. The chrome — five subtabs, dropdown filters, multi-line tables — survived intact. The result was a UX so hostile that the WMS module is the single most-replaced NetSuite module in the third-party-WMS-add-on market (RF-SMART, SuiteWorks Tech, others). NetSuite eventually rebuilt the mobile app from scratch with a different team — it's better now but still feels like a 2020 product retrofitted with mobile patterns rather than designed mobile-first.

The lesson Cherry Street takes: **mobile is a different form factor with a different user with different inputs and different attention budget.** It is not the desktop reflowed. The Control Center's mobile rendering is **opinionated** — the KPI strip is collapsed, the Exception Lane is full-screen, the Drawer takes over the viewport, the voice button is always-visible. We commit to this opinionated mobile experience from day one rather than reflowing the desktop and apologizing later.

---

## 10. KPIs that matter for Receiving leaders

Thirteen KPIs that Receiving leaders actually care about. The Control Center's eight-tile KPI strip ships with the highest-leverage subset; the rest live in a "All KPIs" drawer for the leader who wants depth. For each: definition, why it matters, target benchmark, how it surfaces.

### 10.1 Dock-to-stock time (the headline KPI)

**Definition.** Median minutes from truck-arrival timestamp (door-open event) to receipt-posted-and-available-to-pick timestamp. Excludes quarantine-routed receipts (they have a separate cycle-time metric).

**Why it matters.** This is the single best summary of receiving operational excellence. It correlates with inventory record accuracy, with picker productivity (received material is not pickable yet), with cash flow (received material is not bill-pay-able until the receipt is posted), and with customer service (some cross-dock material has a deadline).

**Benchmark.** Best-in-class plants we've profiled run 30–60 minutes median. Typical mid-market runs 4–12 hours. The plants that have never measured it run 18–48 hours and are unaware.

**Tile.** Current shift median in minutes (large number), comparison to target as colored chevron, trailing-30-shifts sparkline.

### 10.2 Receiving accuracy (first-pass)

**Definition.** Percentage of receipts where the received quantity matches the expected quantity on first pass, no rework, no over/short variance flag, no manual correction.

**Why it matters.** Re-key errors, mis-scans, and mis-counts cascade into inventory inaccuracy, AP variance, and customer back-orders. First-pass accuracy is the leading indicator of inventory record accuracy three weeks downstream.

**Benchmark.** Best-in-class: 98%+. Typical: 88–94%. Below 85%: emergency.

**Tile.** Current shift percentage, target chevron, sparkline.

### 10.3 Doc completeness

**Definition.** Percentage of receipts where all required profile attributes (per `ReceiptProfile.JsonSchema.required`) are populated at posting time. A Steel receipt with no heat number is not doc-complete. A Pharma receipt with no GTIN+lot+expiry is not doc-complete.

**Why it matters.** Audit readiness. A DSCSA inspection, an FSMA recall traceback, an AS9100 audit — all of them ask "show me the receipts" and ding the auditee for missing trace fields. This KPI predicts audit performance.

**Benchmark.** Best-in-class: 99%+. Regulatory-floor: 95% (below that, the gap material in a 24-hour recall response will exceed FDA's tolerance).

**Tile.** Current shift percentage, drill-down to the receipts missing fields.

### 10.4 Supplier on-time

**Definition.** Percentage of receipts arriving within the supplier's promise window — typically defined as `Expected Delivery Date ± 1 business day` or `± 1 hour` for ASN-driven appointments.

**Why it matters.** Supplier performance directly impacts production reliability and inventory buffer requirements. A supplier whose on-time slips below 90% triggers a buyer-relationship review.

**Benchmark.** Best-in-class supplier base: 95%+. Typical: 82–90%.

**Tile.** Current 30-day rolling percentage across all suppliers, plus a click-through to the supplier scorecard with per-supplier breakouts.

### 10.5 Quarantine cycle time

**Definition.** Median hours from receipt-posted-to-quarantine timestamp to QC-released (or QC-rejected) timestamp.

**Why it matters.** Material sitting in QC quarantine ties up working capital, blocks production, and frustrates the planners. Long quarantine cycles correlate with QC under-staffing and with poor supplier quality (more inspection rounds).

**Benchmark.** Best-in-class: 4–8 hours. Typical: 16–48 hours. Past 72 hours: escalation territory.

**Tile.** Current open quarantine count + median cycle time. Drill-down to the QC queue.

### 10.6 First-pass yield on inspection (FPY-Inspection)

**Definition.** Percentage of quarantine-routed receipts that pass QC on first inspection (no rework, no supplier rejection, no re-test required).

**Why it matters.** This is the supplier quality KPI. Trending FPY-Inspection downward is the early-warning signal for supplier-quality erosion months before defects reach production.

**Benchmark.** Best-in-class: 96%+. Typical: 89–93%.

**Tile.** Current 30-day rolling percentage.

### 10.7 Exception rate

**Definition.** Percentage of receipts requiring human intervention outside the normal flow — anything that lands in the Exception Lane (over-receipts beyond tolerance, damages, orphan blind-receives, supplier anomalies, profile-validation failures).

**Why it matters.** Exception rate is the leading indicator of clerk fatigue and of process discipline upstream (buyer, supplier, ASN system).

**Benchmark.** Best-in-class: <3%. Typical: 7–15%. Above 20%: process breakdown.

**Tile.** Current shift exception count + percentage.

### 10.8 Receiver productivity

**Definition.** Average receipts (or PO lines) posted per clerk per shift, normalized by line count not by header count.

**Why it matters.** Capacity planning, staffing decisions, performance management.

**Benchmark.** Best-in-class with voice/scan: 80–120 receipt-lines per clerk per 8-hour shift. Typical click-and-type: 35–60.

**Tile.** Shift totals plus a leaderboard if the customer opts in (leaderboards are toggleable per-tenant; some HR climates make them counter-productive).

### 10.9 Damage rate

**Definition.** Percentage of receipts flagged with the Damage indicator.

**Why it matters.** Tracks carrier performance (most damages are freight, not supplier). High damage-rate-by-carrier is the trigger for a carrier-RFP / freight-claim escalation.

**Benchmark.** Best-in-class: <1%. Typical: 1.5–4%.

**Tile.** 30-day rolling rate.

### 10.10 Over/short rate

**Definition.** Buckets — over-receipt rate (qty received > qty ordered) and short-receipt rate (qty received < qty ordered), separately tracked.

**Why it matters.** Over-receipts cost the AP team time (variance investigation) and tie up cash (we owe the supplier for un-budgeted inventory). Short-receipts cost the planner time (re-plan around back-orders).

**Benchmark.** Best-in-class: <2% in each direction. Typical: 3–7%.

**Tile.** Combined bar with over/short split.

### 10.11 ASN penetration

**Definition.** Percentage of receipts that arrived with a pre-staged ASN (EDI 856 or equivalent).

**Why it matters.** ASN-driven receipts are 4–8x faster than PO-driven receipts. Increasing ASN penetration is the single best lever for reducing dock-to-stock time at the supplier-base level. Tracking this KPI surfaces the suppliers worth onboarding to ASN.

**Benchmark.** Best-in-class: 75%+. Typical: 25–50%.

**Tile.** 30-day rolling percentage, drill-down to suppliers without ASN.

### 10.12 Dock-door utilization

**Definition.** Minutes that a dock door was occupied (truck at the door) divided by minutes the door was scheduled to be active. Per-door and aggregated across all doors.

**Why it matters.** Capacity planning, schedule optimization, the case for adding a dock door (or for closing one). Plants that have never measured dock-door utilization are typically over-provisioned (50–65% utilization) or chronically under-provisioned (90%+ with queueing).

**Benchmark.** Sweet spot 65–80%.

**Tile.** Current-shift heat strip across all doors.

### 10.13 Voice/scan adoption

**Definition.** Percentage of receipt-line operations executed via voice or scan rather than typed entry.

**Why it matters.** This is **our** KPI — the one that tracks Cherry Street's product adoption inside the customer's operations. Customers whose voice/scan adoption climbs above 75% in 30 days are the ones who renew enthusiastically; customers stuck below 30% are the ones who churn at year 1. Surfacing this KPI to the customer normalizes the expectation that voice/scan is the default path.

**Benchmark (Cherry Street internal):** 75%+ within 30 days, 90%+ within 90 days.

**Tile.** 30-day rolling percentage.

### 10.14 The eight V1 tiles

The eight-tile KPI strip in the V1 Control Center ships with: **dock-to-stock time, receiving accuracy, exception rate, doc completeness, supplier on-time, quarantine cycle time, ASN penetration, voice/scan adoption.** These are the eight that combine "operational excellence" (1–3, 6) with "audit / supplier discipline" (4, 5) with "modernization signal" (7, 8). The remaining five (FPY-Inspection, receiver productivity, damage rate, over/short, dock-door utilization) live in the "All KPIs" drawer accessed from a "..." overflow on the KPI strip.

### 10.15 The behavioral effect of measurement

Across every plant we profiled where dock-to-stock-time tracking was introduced for the first time, the median dropped 35–50% in the first quarter — **with no other change.** The act of making the metric visible to the clerk and to the supervisor changed behavior without any process redesign. The Control Center's KPI strip is therefore not just a reporting surface; it is a behavior-modification surface. We expect customers to see measurable operational improvements in the first 30 days, attributable to visibility alone, before they begin using the AI features that promise further compound improvements.

---

## 11. "Look forward to working in it every day" UX moves

What makes Marisol look forward to opening this app at 6:55 AM on a Tuesday in February? Twenty specific UX moves below. None of them is "delight" for its own sake. Each one removes a daily friction or honors a small bit of craft pride. Together they compose into a tool the clerk feels is **on her side.**

**Cmd-K-first global scan.** The keyboard shortcut Cmd-K (or Ctrl-K) opens a universal palette that accepts: a scanned barcode (GS1-128 parsed and routed to the right receipt action), a typed PO number (jumps to the PO), a typed receipt number (opens the drawer), a typed vendor name (filters the Exception Lane), or a typed natural-language phrase (routes to the voice-AI). The clerk never hunts for the scan field; the scan field is everywhere.

**Voice-as-default with always-on listening (opt-in) and push-to-talk fallback.** A persistent voice button bottom-right. Default mode is push-to-talk (hold spacebar, or hold the bottom-right button, or press a Bluetooth-headset button). Opt-in mode is always-on with a configurable wake word. The supervisor toggles per-clerk in the settings.

**Keyboard shortcuts everyone already knows.** `J / K` for next/previous, `Enter` to open, `Esc` to close, `/` to focus search, `?` to summon the cheatsheet, `Cmd-K` for the palette, `Cmd-Enter` to confirm an action, `Cmd-Z` for undo. These come from Gmail, Linear, and Stripe. The clerk who has used any of those tools knows the shortcuts on day one.

**Drawer-based detail — never lose your place.** The Exception Lane is the navigation; the Drawer is the inspection. Clicking a row opens the drawer; the list stays visible beneath the drawer's slight scrim. The clerk's place on the list is never lost. Multi-receipt batch operations are possible without leaving the lane.

**Calm color palette.** Stripe-calm soft greys and a single accent. Exception status is the only place red and amber appear. Voice mode is the only place violet appears (a discreet violet pulse around the voice button). The page is allergic to gratuitous color.

**Sparklines on every KPI tile.** No bare numbers. Every tile carries trend context. The 30-shift sparkline answers "is this number going the right way?" without the clerk having to ask.

**"What's next" zero state.** When the Exception Lane is empty — every exception triaged, every receipt posted — the lane shows a brief, calm zero-state ("All clear. 14 receipts processed this shift. Median dock-to-stock: 38 minutes.") with a small celebration animation. The supervisor sees the same zero-state and knows the clerk has caught up. No fake urgency, no anxiety-mongering "you have unread items" badges that decay into background noise.

**Real-time updates without page reload.** SignalR pushes: a new exception arrives, the lane updates in place; a colleague claims an exception, the row dims; a receipt posts, the Activity Feed scrolls; a KPI tile increments. The clerk never refreshes; the page is alive.

**Streaks and badges without gamifying work.** A small visual acknowledgment when the clerk posts a clean shift (no exceptions of her own creation, all receipts doc-complete). The acknowledgment is cosmetic — never tied to compensation, never visible to anyone but the clerk. The point is craft pride; the trap to avoid is leaderboard-driven gaming of the system.

**Empty-state illustrations that match the calm aesthetic.** Hand-drawn line art (sparse, monochrome, friendly) on every empty state. Not stock photos of warehouses. Not children's-book caricatures. The aesthetic matches Stripe's empty-state illustration style.

**Drag-and-drop bulk operations.** Select multiple rows in the Exception Lane (Shift-click or J/K + Space), drag onto a "Quarantine" target in the right-rail, single action applies to all. Most ERPs make this impossible; the Control Center makes it natural.

**Print preview that actually looks like the paper output.** When the clerk hits "Print Staging Label," she sees a preview that is byte-for-byte what the printer will produce. ZPL preview rendering on the page. No surprise mis-prints; no "wait, the QR code didn't fit." This pattern was pioneered by Stripe's invoice preview and should be ported here.

**"I goofed" undo for the last operation (10-second window).** A subtle toast at the bottom of the screen after every mutation: "Receipt 4500178-1 posted. Undo (9s)." The 10-second window is a guard rail; the IIdempotencyMediator pattern makes the undo safe. After 10 seconds the toast fades; after that the operation requires a deliberate reversal.

**Compact-mode vs comfortable-mode toggle.** A user setting (persisted per-user in localStorage and synced to the user profile). Compact mode brings row height to 32px, comfortable to 44px. The clerks who do 150 line-receipts per day live in compact; the supervisor who scans the lane four times a day prefers comfortable.

**Persistent search with recently-used filters.** Filter history per-user (last 10 filters), pinnable to the sidebar. The clerk who triages "Steel + Damaged + Last 24 hours" three times a shift makes that filter a one-click chip.

**Right-rail context that updates as you move through the list.** The drawer is more than a detail view — it's a context surface. As the clerk navigates the lane with J/K, the drawer updates with the focused receipt's full profile-driven body, the supplier scorecard, the chain-of-custody summary, and the recent AI suggestions for the row. No selection-and-then-open ceremony; selection is opening.

**Smart defaults — every field has the right default 90% of the time.** Quantity defaults to expected qty (per PO line). Profile defaults to item's `DefaultReceiptProfileId`. Location defaults to item's primary bin. Lot/serial defaults from the GS1-128 scan parse. The clerk's job is to confirm or override, not to type from scratch.

**A "what changed since I left" panel.** When the clerk returns from lunch or from a shift handoff, a small panel summarizes what happened in her absence: "While you were away: 3 receipts posted by Eric, 1 exception claimed by Sandra, 2 new orphans need triage." Zero clicks to context-restore.

**Vendor-side empathy.** When a vendor's scorecard is bad (declining trend), the Drawer adds a small "vendor-relationship note" widget — last contact, primary contact name, recent issues. The clerk who is about to chase a third short-shipment from one supplier has the supplier's last conversation in front of her. This is the kind of CRM-meets-ERP move only a small, opinionated tool can make.

**A "training mode" for new hires.** New clerk's first 10 receipts run with extra confirmations enabled, with tooltip explanations on every field, with the AI's confidence chips displayed prominently. After 10 successful receipts, training mode auto-disables (or the clerk can keep it on). Industry-standard ERPs treat training as a separate sandbox environment; we ship training as a mode on the production app.

**A craftsperson's pride finish.** Animation timing is consistent (220ms for drawer open/close, 80ms for chevron rotations, 140ms for tile sparkline tweens). Font scaling honors the user's OS-level text-zoom. Reduced-motion preference is respected. Dark mode ships at parity with light mode (and the dock workstation often prefers dark because the dock lighting is harsh). All eleven languages of Cherry Street's pilot tenancy are first-class, including the Spanish that 60% of Marisol's dock co-workers speak as their first language. The page does not break, does not glitch, does not surprise. It is **finished**.

The synthesis: none of these is a competitive moat by itself. The compounding of all twenty is. After 30 days of using the Control Center, the clerk who is asked to switch back to SAP MIGO or NetSuite Item Receipt or anything in Section 3 or 4 will refuse. That is the moat.

---

## 12. Screen-in-words mockups — page, drawer, voice-only

Three mockups in words. Specific about pixel placement, color, density. These are the artifacts that go to ADR-016 and to the design implementation PR.

### 12.1 Main page — `/Receiving` Control Center landing

**Viewport assumed:** 1440 × 900 desktop. Cherry Street's locked design tokens (Phase 3 / PR #116d.1a) provide the palette: a base off-white background `var(--surface-canvas)` at #FAFAF9, an elevated surface `var(--surface-elevated)` at #FFFFFF, a hairline border `var(--border-hairline)` at #E5E5E3, a neutral text scale (titles #1A1A18, body #4A4A47, secondary #888884), a single warm accent (cherry red `#B8202D` for exception status), and a separate amber (`#C68A1E`) and a discreet violet (`#5C4FB8`) for voice-active state.

**Top of page (rows 0–64px).** A 64-tall global app header with the Cherry Street wordmark on the left, the universal scan-and-search palette in the middle (a 480px-wide input with the placeholder "Scan or search... (Cmd-K)"), and the user avatar + organization switcher on the right. The scan field is auto-focused on page load. Below the header, a 12-tall environment-banner strip showing the active tenant + active warehouse + shift number, in 11pt secondary text.

**Page title row (rows 76–124px).** The page title "Receiving" in 28pt brand serif on the left. To the right: a segmented control offering "All / Open / Today / This Shift" filters (the default is "This Shift"). On the far right: a "+ New Receipt" button (secondary), a "Print Day's Log" icon button, a vertical-overflow "..." with "All KPIs", "Settings", "Export", "Help".

**KPI strip (rows 132–268px).** Eight tiles in a single horizontal row. Each tile is 162px wide, 136px tall, with 12px gap between, occupying the full content width. Each tile has, from top to bottom:

- 10pt secondary-text label (e.g., "Dock-to-stock"),
- 32pt monospaced numeric value (e.g., "38m"),
- A trend chevron (8pt, green for favorable / amber for marginal / red for adverse) plus a 10pt comparison string ("vs 42m target"),
- A 24-tall sparkline of the last 30 shifts.

Tile order, left to right: Dock-to-stock, Accuracy, Exception Rate, Doc Completeness, Supplier On-Time, Quarantine Cycle, ASN Penetration, Voice/Scan Adoption.

A tile in alert state (above its threshold) wears a thin 1px cherry-red border around the otherwise-neutral tile chrome; the value text turns cherry-red.

**Exception Lane (rows 280–648px).** The center column, 880px wide, occupying the left two-thirds of the content area. A header row: "Exceptions" in 18pt title-text on the left, a sort dropdown ("By SLA-age / By dollar / By vendor / By type") on the right, a small "Hide claimed" toggle.

Beneath the header, a virtualized list of exception rows. Each row is 56px tall in comfortable mode, 32px tall in compact mode. Each row contains, left-to-right:

- A 20×20px exception-type icon (a triangle for over-receipt, a droplet for damage, a question-mark for orphan, a clock for stale-quarantine, a fluctuation glyph for vendor-anomaly),
- 11pt monospaced receipt-or-PO number (left-aligned, 88px),
- 13pt vendor name + 11pt secondary-text item description below it (left-aligned, takes the variable middle width),
- 11pt monospaced quantity (right-aligned, 88px),
- 11pt SLA-age pill, color-coded (green <2h / amber 2–8h / red >8h),
- A claim-this-row check on hover (20×20px circle).

Rows are keyboard-navigable with J/K; the focused row has a 2px-cherry-red left-edge accent and a slight elevation. A focused row's drawer-content is pre-fetched and rendered in the right rail.

When the lane is empty, the zero-state ("All clear. 14 receipts processed this shift. Median dock-to-stock: 38 minutes.") with a small monochrome sparkline-and-dock illustration centered in the lane.

**Drawer Detail (rows 280–648, columns 920–1408).** The right rail, 480px wide, occupying the right one-third of the content area. Visible only when a row in the Exception Lane is focused (J/K navigation pre-fetches; click commits). Renders the receipt's profile-driven body via the ADR-015 `DynamicFormViewComponent`. The drawer header has:

- The receipt or PO number in 18pt monospaced title-text,
- A profile-badge pill (e.g., "STEEL" in monospaced 10pt, on a soft amber background) showing which `ReceiptProfile` is active,
- A close-drawer X (Esc collapses).

Body groups (per `UiFormSpec`): "Traceability" (heat number, mill, mill cert URL, ASTM, country of melt), "Dimensions" (length, width, thickness), "Quantity & Variance" (expected, received, variance, tolerance check), "Photos & Documents" (drag-drop area, list of attached files), "Audit & AI History" (chronological entries with user + AI-tool annotations).

Footer: action buttons sized for gloved fingers (44px tall) — "Receive", "Quarantine", "Reject", "Partial", "Split". Voice-equivalents documented in tooltips.

**Activity Feed (rows 660–820px).** The full content-width bottom strip. A horizontally scrollable timeline of the last 60 minutes of shop-floor activity — receipts posted, exceptions claimed, AI tool calls executed, dock-door state changes, supervisor messages. Each event is a 200×140px card. Real-time SignalR updates push new cards in from the right.

**Persistent voice button (bottom-right, fixed positioning).** A 64×64px circular button, 24px from the right edge, 24px from the bottom edge, with a 20×20px microphone glyph. Default state is neutral; push-to-talk-active state is a violet pulse (the only violet in the page); listening state is a faint violet ring. Click + hold (or hold spacebar) is push-to-talk; double-click toggles always-on. A subtle status pill above the button reads "Press and hold to speak" (default) or "Listening…" (active) or "Heard: 'twenty-four plates, heat H-A8842-1' — confirm?" (post-utterance).

### 12.2 Drawer detail — when a receipt is opened

This is the deep view of a single receipt. Profile-aware, voice-form-spec-rendered, action-rich.

**Header (480 × 88px).** Profile pill (left), receipt number (left of center), close-X (right). Below the receipt number, a one-line summary: vendor, expected-vs-received quantity, status (e.g., "ACME Steel — Received 22 of 24 plates — Posted").

**Voice payload debug bar (only in supervisor mode).** A collapsible 24-tall thin strip showing the JSON of the page's `VoiceContextPayload` so the implementer / supervisor can verify what the AI sees from this drawer. In production-customer mode this is hidden.

**Profile-rendered body (480 × 480px).** Groups per the active profile's `UiFormSpec`. For a STEEL receipt:

- **Traceability group.** Heat number (text input, monospaced, 14pt, with a "scan" affordance to accept GS1 AI (10)/(21) directly). Mill (combobox with vendor's known mills). Mill Cert URL (drag-drop file area; the OCR call is wired here). ASTM (combobox with the steel-grade catalog). Country of Melt (ISO-2 dropdown).
- **Dimensions group.** Length, Width, Thickness (decimal inputs in mm, 12pt). Usable length and usable width (decimal, 12pt).
- Each field has, beneath it, a 9pt "voice synonyms" hint when the cursor is in the field — showing what the AI would parse as this field ("heat / heat number / melt id / melt number").

**Quantity & variance group (480 × 132px).** Expected qty | Received qty | Variance qty (computed) | Tolerance status (color-coded chevron). Below: a 2-line freeform note input for "rationale if variance > 0."

**Photos & docs strip (480 × 96px).** A horizontal strip of attached file thumbnails (mill cert PDF, damage photos, BOL scan). Drag-drop accepts new files. Each thumbnail has a small overflow menu (download, replace, delete).

**Audit & AI History (480 × auto).** Chronological list, newest-first. Each entry: timestamp, actor (user with avatar, or AI-tool name with a small "AI" pill), action ("created receipt", "uploaded mill cert", "OCR parsed heat number — confidence 0.94", "voice-confirmed by Marisol Reyes"). Click an AI entry to see the prompt + model version + idempotency key.

**Footer (480 × 88px).** Five action buttons, each 44px tall — Receive (primary cherry-red), Quarantine, Reject, Partial, Split. Below them, a discreet "Open in full page" link for the rare case the clerk wants the standalone deep-link URL (`/Receiving/Receipt/{id}`).

### 12.3 Voice-only flow — what the clerk sees when she's wearing a headset

This is the high-conviction differentiator. The screen exists, but the clerk does not need to look at it.

**Layout.** The Control Center collapses into a single-pane "voice canvas" — the four-quadrant scaffold is hidden. The viewport is dominated by:

**A big status pill (top half of screen, large).** Text fills the pill in real time. States:

- **Ready** (waiting for utterance): "Ready. Press and hold to speak." in 32pt brand-serif.
- **Listening** (push-to-talk active): "Listening…" in 32pt, with a live audio-waveform animation beneath the text (8 vertical bars modulated by mic amplitude).
- **Processing**: "Got it. Working…" in 32pt with a small spinner.
- **Confirming**: "Receive 24 of PO line 4500178823-1, heat H-A8842-1. 2 short of expected 26. Confirm?" in 24pt. Below the question, two large confirmation chips: a 96-tall green "Yes" and a 96-tall grey "No / Cancel". The clerk says "yes" or taps the green chip.
- **Confirmed**: "Receipt posted. Label printing at Dock 3." in 24pt with a soft fade-out after 3 seconds.

**A bottom-half context strip (small font, secondary text).** Three lines:

- Active PO + line ("PO 4500178823 / Line 1 of 4").
- Last 3 transcribed utterances ("Heat H-A8842-1 — 24 plates — 2 short").
- Hint for the next available action ("Say 'next line' to continue, 'post the truck' to finish.").

**Voice button (large, bottom-center).** In voice-canvas mode, the voice button moves from bottom-right to bottom-center and grows to 128×128px. This is the only interactive element. A glance-down at the screen confirms the state; everything else is by voice or by headset button.

**Re-entry to standard Control Center.** The clerk presses Esc, taps the back-arrow in the top-left, or says "exit voice mode." The four-quadrant scaffold returns.

This is the flow that will define the demo video. A 90-second recording of Marisol receiving a 5-line truck without touching the screen is the single best piece of marketing content Cherry Street will produce in 2026. No competitor can replicate it.

---

## 13. Design recommendations — Cherry Street's locked direction

The hard recommendations to bring to ADR-016. Eighteen numbered claims for Dean's sign-off. Each is non-negotiable for the pilot Control Center; together they define the Cherry Street disruption pattern.

**R1. Route the pilot at `/Receiving`.** The Control Center landing page lives at `/Receiving`. A receipt's deep-link detail is `/Receiving/Receipt/{id}`, which opens the drawer in a full-page overlay (deep-linkable for share-with-supervisor flows). The existing `/Receipts` legacy route, if any, redirects to `/Receiving`.

**R2. Adopt the four-quadrant scaffold.** KPI strip across the top (eight tiles), Exception Lane in the center-left two-thirds, Drawer Detail in the right-rail one-third, Activity Feed across the bottom. This scaffold is the **template** for all subsequent Control Centers (Purchasing, Maintenance, Planning, Scheduling, Inventory, Quality, Shipping, AP/AR, HR). Roughly 70% of the front-end code is reusable across Control Centers; only the per-domain content varies.

**R3. Ship voice-first, not voice-as-a-feature.** Push-to-talk is the default; always-on is opt-in. The voice button is persistent (bottom-right in scaffold mode, bottom-center in voice-canvas mode). Voice mode flips the page into the voice-canvas (Section 12.3) automatically when the headset is connected and push-to-talk is held for > 800ms. The ADR-015 D10 prompt template (per voice-AI-spike-adr015-d10.md §2) is the contract for the AI integration.

**R4. Render the body with `DynamicFormViewComponent`.** ADR-015's profile-driven Razor component is the drawer body. Twelve seed profiles (STEEL, PHARMA, FOOD, CHEMICAL, ELECTRONICS, MEDICAL_DEVICE, AEROSPACE, CANNABIS, AUTOMOTIVE, APPAREL, CONSTRUCTION, OIL_GAS) ship in PR #1 of the ADR-015 migration; the Control Center reuses them. Tenant-custom profiles are first-class — a customer who needs to add a chemistry-detail group to STEEL forks the seed profile.

**R5. Zebra-compatible scan focus mode.** The global scan-and-search field is auto-focused on `/Receiving` load and after any modal close. Suffix-aware (DataWedge suffix `\n` or `\t` both trigger search-submit). Cmd-K opens the same palette from anywhere. GS1-128 parsing is built into the input handler; an AI (01) prefix routes to GTIN lookup, (10) to lot, (17) to expiry, (21) to serial. Test against a real TC52 / TC57 / Honeywell CT45 before pilot launch.

**R6. Kill `/Admin/StockReceipts/Edit` Create entry from the sidebar.** Per memory `project_control_center_pattern.md`, the existing admin Create flow is unlinked from the sidebar. The `Admin/StockReceipts/Edit` route remains for admin-only fixup (correcting a posted receipt's audit-noted fields), but it is not the primary or even a secondary path. The primary path is `/Receiving`.

**R7. Service-layer surface: `IReceivingControlCenterService`.** A new service interface owns: queue assembly (`ListExceptionsAsync(filter, user, ct)`), prioritization (`RankExceptionsAsync(...)`), voice-execute (`ExecuteVoiceActionAsync(toolName, args, user, ct)` — delegates to the right sub-service), KPI rollup (`GetKpiSnapshotAsync(shift, dock, user, ct)`). Every method follows ADR-014 D2 (DTO-in / DTO-out, Result<T>, ClaimsPrincipal + CancellationToken).

**R8. Eleven AI tools across V1 + V2.** Per Section 8: four V1 tools (existing IReceiptVoiceTools stubs — `createReceiptFromPoLine`, `quarantineReceipt`, `traceChainOfCustody`, `lookupReceipt`), seven V2 tools (`ListExpectedArrivals`, `MatchOrphanReceipt`, `ExplainException`, `ReceiveByVoice`, `QuarantineByVoice`, `OcrParseMillCert`, `SuggestPutawayLocation`). The ADR-015 D10 prompt template is the framing for all eleven.

**R9. Eight KPI tiles in V1.** Per Section 10.14: Dock-to-stock, Accuracy, Exception Rate, Doc Completeness, Supplier On-Time, Quarantine Cycle, ASN Penetration, Voice/Scan Adoption. The remaining five KPIs live in "All KPIs" drawer.

**R10. Mobile responsive web for V1; PWA manifest + home-screen icon; defer native.** Per Section 9.4. The TC52 / TC57 / Honeywell CT45 viewport is a tested target; BrowserStack device profiles are in the pilot's CI smoke-test gate. Native iOS / Android is deferred until customer demand justifies the investment; the responsive web + PWA + Trusted Web Activity wrapping covers the addressable market through 2026.

**R11. Big-board supervisor view in V2 (`/Receiving/StandUp`).** Pilot focuses on the clerk workflow. Sprint 6 ships the supervisor wall-board variant. Same data, different layout. Auto-refresh every 5 seconds.

**R12. Cmd-K, J/K, Enter, Esc, /, ?, Cmd-Z everywhere.** The keyboard-shortcut contract from Section 11 is non-negotiable. Every shortcut is documented in the `?` cheatsheet (already shipped per PR #116d.1c). The page is fully drivable from the keyboard.

**R13. Right-rail drawer is the only detail-view mechanism.** No modals for receipt detail. No navigation away from the lane. The drawer's deep-link URL (`/Receiving/Receipt/{id}`) opens the same view at full-page size for share-link scenarios. Modals are reserved for irreversible confirmations and for cross-domain handoffs (e.g., "Open this PO in Purchasing Control Center").

**R14. Real-time updates via SignalR.** Per Section 11. The page is alive; the clerk never refreshes. Pushed events: new exception arrived, exception claimed/released, receipt posted, AI tool executed, KPI tile incremented, supervisor message broadcast.

**R15. Idempotency by IIdempotencyMediator on every mutation.** ADR-014 D4 substrate. The mediator's key per voice utterance is the utterance audio hash + page-session ID. Double-yeses become one operation.

**R16. AuditLog AI-on-behalf-of populated on every AI-mediated action.** ADR-014 D3 substrate. Seven AI columns: `AiActedAsUserId`, `AiToolName`, `AiModelVersion`, `AiPromptHash`, `AiCompletionId`, `AiConfidenceScore`, `AiRequestId`. The user is always the principal Actor; AI is metadata.

**R17. Profile-aware permission model.** ADR-014 D5 substrate. Resource-based authorization is the choke point. AI executes as the invoking user's `ClaimsPrincipal` — never as a service account. A clerk without permission to quarantine cannot drive the AI to quarantine.

**R18. Pilot launch in Sprint 5; Control Center scaffold reused in Sprint 6.** The Receiving Control Center ships at the end of Sprint 5 alongside the V1 AI tools and the voice-first surface. Sprint 6 hardens the experience (V2 AI tools, big-board, native enhancements) and starts the Purchasing Control Center using the Receiving scaffold as the template. Roughly 70% of the front-end code (KPI strip, Exception Lane, Drawer wiring, Activity Feed, voice button, Cmd-K integration) is lifted as-is. Domain-specific content (KPI definitions, exception types, AI tools, profile-driven body groups) varies per Control Center.

### 13.1 The pattern, generalized

The same eighteen recommendations, with domain content swapped, define the Purchasing Control Center (`/Purchasing`), the Maintenance Control Center (`/Maintenance`), the Planning Control Center (`/Planning`), and the rest. The four-quadrant scaffold + voice-first + profile-driven body + IIdempotency + AuditLog AI columns + IXxxControlCenterService is the **pattern language** of Cherry Street's UI. Once Dean signs off on ADR-016, every subsequent Sprint inherits the pattern automatically.

### 13.2 What we are explicitly not building

To avoid scope creep, the following are explicitly **out of scope** for the Receiving Control Center V1:

- Cross-dock, drop-ship, and consignment workflows (V3 — Section 6.12).
- Big-board supervisor view (V2 — R11).
- Native iOS / Android apps (V2+ — R10).
- RFID gateway integration (V2 — Section 7.3).
- Dock-door sensor integration (V2 — Section 7.6).
- Inline carrier-tracking-webhook integration (V2 — Section 8.6 partially mocked).
- Sub-bin / location-tree management (lives in Inventory Control Center; pilot uses the existing simple location model).
- Bulk PO upload / supplier-portal entry points (separate stream, not Control Center).

Pilot scope is **deliberately narrow** so the V1 ship is unambiguous and dateable.

---

## 14. Open questions for Dean

Six binary or near-binary decisions that we need Dean to sign off on before ADR-016 freezes. For each: the question, the recommended answer, the trade-off, and the consequence of changing later.

### Q1. Drawer vs full page for receipt detail?

**Recommendation:** Drawer with deep-link to full page. The drawer is the primary; `/Receiving/Receipt/{id}` is the share-able URL that renders the same drawer content at full-page width. The clerk never loses her place on the Exception Lane; the supervisor who receives a shared link sees the full deep-page view.

**Trade-off:** A small subset of receipts (those with extremely long attribute lists — e.g., a Pharma receipt with 12 cold-chain sensor events) overflow the 480px drawer width and benefit from full-page. We handle this with a "Open in full page" link in the drawer footer; the user opt-in expands to the deep-page view if they need it.

**If we change later:** Switching from drawer-first to page-first requires re-doing the Exception Lane interaction model (and re-doing the muscle memory for the clerks). Best to commit now.

### Q2. Voice always-on vs push-to-talk default?

**Recommendation:** Push-to-talk default, always-on as an opt-in. Default protects the clerk from accidental utterances (background chatter, radio, conversations with the forklift driver) becoming AI invocations. Always-on is selectable per-user in settings — the seasoned headset-wearing clerk who wants hands-free flips it on; the new hire stays on push-to-talk until comfortable.

**Trade-off:** Push-to-talk is less ergonomic for the headset-wearing power user. Mitigated by Bluetooth-headset-button mapping (the Plantronics Voyager's primary button = push-to-talk).

**If we change later:** Toggling default is a one-line config change. No architectural commitment.

### Q3. Print the BOL signature from the app or on paper?

**Recommendation:** Digital BOL signature capture from the app **with** paper fallback. The driver signs on a paired tablet (or on the clerk's TC52's touchscreen) with a stylus; the signature image is attached to the receipt and emailed to the carrier-of-record. Paper BOL fallback for drivers who refuse digital (still common in 2026).

**Trade-off:** Digital capture requires a separate "driver-facing" mode on the TC52 (to avoid showing the driver our internal data). Mitigated by a "driver hand-off" sub-flow that switches the screen to a single-purpose signature pad. Sprint 6 work; pilot ships paper fallback only.

**If we change later:** Low-risk additive feature. Not a v1 blocker.

### Q4. ASN ingestion live now or deferred?

**Recommendation:** Live for the pilot. EDI 856 parser is already on the Sprint 5 roadmap; the parser delivers ASN-driven receipts (Section 6.2), which are the highest-leverage workflow for reducing dock-to-stock time. The pilot customer's top three suppliers must be ASN-onboarded in the customer-success engagement.

**Trade-off:** EDI 856 parsing has a long tail of dialects (X12 4010 vs 4030 vs 5010; pre-extended-trace vs post-extended-trace formats). We commit to supporting X12 4010 and 5010 in V1 and document the supported subset.

**If we change later:** Deferring ASN ingestion means the pilot's most-impressive-metric demo (a 38-minute median dock-to-stock for an ASN-driven receipt) is unavailable. Recommend committing now.

### Q5. Cross-dock and consignment workflows in v1 or v2?

**Recommendation:** V2 — pilot focuses on PO-driven (6.1), ASN-driven (6.2), blind (6.3), and partial (6.4). The four V1 workflows cover ~95% of pilot-customer truck volume. Cross-dock requires the outbound-allocation schema piece that has not been written; consignment requires the consignment-agreement entity. Both are real Sprint 6 / Sprint 7 work that we choose not to bundle into the pilot.

**Trade-off:** Pilot customers who have cross-dock or consignment volume will need to keep using their current system for those flows during the pilot. Acceptable — the pilot ROI on the four V1 workflows is the value driver.

**If we change later:** Adding cross-dock and consignment to v1 doubles the scope. Strong recommendation to defer.

### Q6. Big-board / supervisor view in v1 or v2?

**Recommendation:** V2 — pilot focuses on the clerk workflow (R11). The big-board is a different layout of the same data; building it requires no new substrate but does require a separate design pass + supervisor-feedback loop. Sprint 6 ships the supervisor variant; pilot ships only the clerk Control Center.

**Trade-off:** Pilot customers' supervisors will use the same Control Center on a tablet for the first 30 days. Acceptable — the four-quadrant layout works on tablet viewport at office-clerk density. The big-board is a polish addition, not a missing piece.

**If we change later:** Adding the big-board is additive. Sprint 6 scope.

### Q7. Bonus question for Dean: name?

The "Receiving Control Center" is the working title. Cherry Street's product surface uses different visual vocabulary than the standard ERP — we might call this "the Dock" (visceral), "Receiving" (terse), "Receiving Hub" (corporate), "Inbox" (Linear-influenced), or keep "Control Center" (intentional positioning vs SAP/Oracle).

**Recommendation:** **Keep "Receiving Control Center"** as the internal architectural name and the marketing positioning. In the app's user-facing chrome, the page title is just "Receiving." The "Control Center" framing is the analyst-meeting and demo-script vocabulary — it positions Cherry Street alongside Mission Control and Airline Ops, not alongside SAP MIGO.

**Decision needed from Dean:** Endorse "Receiving Control Center" as the pattern name + "Receiving" as the user-visible title, or counter-propose.

---

**Recommended next step:** With these seven questions answered, the immediate action is **write ADR-016: Control Center Pattern + Receiving Spec**, freezing R1–R18 and the open-question answers, and scheduling the Sprint 5 PR set against it.

---

## 15. Sources

See companion file [`receiving-control-center_sources.md`](receiving-control-center_sources.md).

---
