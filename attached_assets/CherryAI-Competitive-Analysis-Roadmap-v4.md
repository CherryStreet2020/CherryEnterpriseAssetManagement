# CherryAI EAM — Competitive Feature Gap Analysis & Prioritized Roadmap
## Version 4.0 — March 11, 2026
## Based on: Codebase audit (323 C# files, 378 Razor Pages, 65 models, 73 services) + market research

---

# PART 1: THE COMPETITIVE LANDSCAPE

## Who We're Up Against

The EAM/CMMS market breaks into three tiers:

**Tier 1 — Enterprise Heavyweights ($150-500+/user/month)**
- IBM Maximo Application Suite (MAS 9.1) — The 800-lb gorilla. AI/IoT/APM suite on Red Hat OpenShift. Trusted since 1996.
- SAP EAM (Plant Maintenance) — Deeply integrated with SAP ERP. Manufacturing powerhouse.
- IFS — #1 in EAM market share per Gartner 3 years running. AI-powered scheduling optimization.
- Oracle EAM (E-Business Suite) — Strong in utilities and government.
- Hexagon EAM (formerly Infor CloudSuite) — Acquired by Hexagon. Industry-specific configurations.

**Tier 2 — Modern CMMS ($16-85/user/month)**
- MaintainX — Mobile-first, AI-powered, #1 rated CMMS on G2/Capterra. 9,000+ companies, 650K+ workers.
- UpKeep — Asset operations platform. Strong mobile UX.
- Fiix (Rockwell Automation) — AI-driven, strong manufacturing integration.
- Limble — Ease of use champion. Self-implementing.
- eMaint (Fluke) — Established mid-market player.

**Tier 3 — Niche/Legacy**
- CHAMPS — 40 years in CMMS. Modular pricing.
- Ultimo (now IFS company) — AI-augmented, strong in healthcare/utilities.
- AVEVA Avantis PRO — Process industry focused.

## Market Trends Driving 2026

1. **AI is now table stakes** — MaintainX has AI procedure generation, voice transcription, anomaly detection, natural language querying. IBM has GenAI assistants for work orders and tickets. IFS has AI scheduling optimization. Everyone is shipping AI features.
2. **IoT/condition-based maintenance** — The market is moving past calendar-based PM into sensor-driven triggers. IBM leads with edge computing + visual inspection AI.
3. **Mobile-first is mandatory** — MaintainX has 2,300+ App Store ratings. Field technicians won't use desktop-only tools.
4. **Platform consolidation** — EAM is merging with APM (Asset Performance Management), FSM (Field Service Management), and ERP. Single-platform suites are winning.
5. **Users hate complexity** — Maximo users need 45-page quick guides. The market is hungry for power + simplicity.

---

# PART 2: FEATURE MATRIX — CherryAI vs. The Market

## Legend
- ✅ BUILT — Feature exists and is functional
- 🟡 PARTIAL — Feature exists but incomplete or needs fixing
- ❌ NOT BUILT — Feature does not exist
- 🔮 PLANNED — On roadmap, not yet started
- N/A — Not applicable to CherryAI's target market

---

## 2.1 ASSET MANAGEMENT

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Asset register (CRUD) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Asset hierarchy (parent/child) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Asset categories/classification | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Asset lifecycle (commission→decommission) | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Custom specifications per asset type | ✅ (10 custom fields) | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Location hierarchy | ✅ | ✅ | ✅ (Functional Location) | ✅ | ✅ | ✅ |
| Asset transfers between locations | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Barcode/QR code support | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Photo/document attachments | 🟡 (model exists, UI needs work) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Asset health scoring | ❌ | ✅ (AI-driven) | 🟡 | ✅ (AI) | ✅ (AI) | 🟡 |
| Digital twin | ❌ 🔮 | ✅ | ✅ | ✅ | ❌ | ❌ |
| Linear/network assets | ❌ | ✅ | ✅ | ✅ | ❌ | ❌ |
| GIS/spatial mapping | ❌ | ✅ (Maximo Spatial) | 🟡 | ✅ | ❌ | ❌ |

**CherryAI's edge:** Most CMMS tools (MaintainX, UpKeep, Limble) have flat or shallow asset hierarchies. CherryAI already has multi-level parent/child + location hierarchy + asset transfer workflows. That puts us at enterprise depth.

**Gap to close:** Asset health scoring and condition monitoring. This is where AI fits.

---

## 2.2 WORK ORDER MANAGEMENT

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Work order CRUD | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Multi-operation structure | ✅ | ✅ (Job Plans) | ✅ (Operations) | ✅ | ❌ | ❌ |
| Per-operation labor tracking | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Per-operation parts/materials | ✅ | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Per-operation tools | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Work request → WO workflow | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Safety permits | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Closeout intelligence | ✅ (CloseoutService) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Hierarchical failure codes | ✅ (4-level: Problem/Cause/Action/Failure) | ✅ | ✅ | ✅ | 🟡 | ❌ |
| Priority/criticality matrix | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Graphical scheduling | ❌ 🔮 | ✅ (5 scheduling apps) | ✅ | ✅ (AI-optimized) | 🟡 (calendar) | 🟡 (calendar) |
| Operation predecessors | ❌ 🔮 | ✅ | ✅ | ✅ | ❌ | ❌ |
| Mobile work execution | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ (#1 rated) | ✅ |
| AI work order generation | ❌ 🔮 | ✅ (GenAI) | ❌ | ✅ | ✅ | ❌ |
| Voice-to-work-order | ❌ 🔮 | ❌ | ❌ | ❌ | ✅ | ❌ |

**CherryAI's edge:** Multi-operation WO structure with per-operation labor/parts/tools + safety permits + 4-level failure codes. MaintainX and UpKeep have NONE of this depth. We're already at Maximo/SAP level for WO structure.

**Gap to close:** Graphical scheduling and mobile. These are the #1 and #2 user-facing gaps.

---

## 2.3 PREVENTIVE MAINTENANCE

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Calendar-based PM | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Meter-based PM triggers | ✅ (model exists) | ✅ | ✅ | ✅ | ✅ | ✅ |
| PM templates with revision control | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| PM schedule auto-generation | 🟡 (scheduler service exists, trigger unclear) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Meter reading entry UI | ❌ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Condition-based maintenance | ❌ 🔮 | ✅ (IoT) | ✅ | ✅ (IoT) | ✅ (IoT) | 🟡 |
| Route-based PM | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | ❌ |
| PM compliance tracking | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Predictive maintenance (ML) | ❌ 🔮 | ✅ (Predict) | 🟡 | ✅ (IFS.ai) | ✅ (anomaly detection) | ❌ |

**CherryAI's edge:** PM templates with revision control is something MaintainX and UpKeep don't have. We version-control maintenance procedures like engineering documents.

**Gap to close:** Meter reading UI is critical — without it, meter-based PM is dead. This is a must-fix.

---

## 2.4 INVENTORY & PROCUREMENT

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Parts catalog (17 item types) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Multi-warehouse inventory | ✅ | ✅ | ✅ | ✅ | ✅ (per-location) | 🟡 |
| Stock level tracking | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Min/max reorder points | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Buyability scoring | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Purchase orders | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Goods receiving | 🟡 (UI built, POST handlers need verification) | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Accounts payable | ✅ | ✅ (via ERP) | ✅ (native) | ✅ | ❌ | ❌ |
| 3-way matching (PO/Receipt/Invoice) | 🟡 | ✅ | ✅ | ✅ | ❌ | ❌ |
| Approved vendor list (AVL) | ✅ (395 AVL records) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Requisition → PO workflow | 🟡 (requisitions exist, conversion unclear) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Kits/BOM | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| Inventory deduction on WO issue | 🟡 (needs verification) | ✅ | ✅ | ✅ | 🟡 | ❌ |
| Auto-reorder | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ (AI) | ❌ |
| Cross-site transfers | ❌ 🔮 | ✅ | ✅ | ✅ | ❌ | ❌ |

**CherryAI's edge:** Buyability scoring is UNIQUE — no competitor has this. AVL with 395 records across 25 real vendors is deeper than any CMMS. AP integration puts us at SAP/Maximo level while MaintainX and UpKeep have zero AP capability.

**Gap to close:** Verify goods receiving POST handlers work. Verify inventory deduction on WO part issue.

---

## 2.5 FINANCIAL / FIXED ASSETS (CherryAI's KILLER ADVANTAGE)

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Fixed asset register | ✅ | ❌ (separate system) | ✅ (FI-AA) | ✅ | ❌ | ❌ |
| Depreciation calculation | ✅ (22 methods) | ❌ | ✅ | ✅ | ❌ | ❌ |
| Multi-book support | ✅ (GAAP + Tax + Custom) | ❌ | ✅ | ✅ | ❌ | ❌ |
| 12 depreciation conventions | ✅ | ❌ | ✅ | 🟡 | ❌ | ❌ |
| Section 179 expensing | ✅ | ❌ | 🟡 | ❌ | ❌ | ❌ |
| Bonus depreciation | ✅ | ❌ | 🟡 | ❌ | ❌ | ❌ |
| Canadian CCA system | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| US Form 4562 generation | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Canadian T2 Schedule 8 | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| GL integration (journal entries) | ✅ | ❌ (relies on ERP) | ✅ (native) | ✅ | ❌ | ❌ |
| Fiscal year/period management | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ |
| CIP (capital project management) | ✅ | 🟡 | ✅ | ✅ | ❌ | ❌ |
| Multi-level GL hierarchy | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ |
| Depreciation preview | ✅ | ❌ | ✅ | 🟡 | ❌ | ❌ |

**CherryAI's MASSIVE edge:** This is where we DESTROY the competition. No CMMS (MaintainX, UpKeep, Limble, eMaint, Fiix) has ANY fixed asset accounting. IBM Maximo has ZERO depreciation — it relies on a separate ERP for financials. Only SAP PM has comparable financial depth because it's part of SAP FI-AA. CherryAI is the ONLY standalone product that combines full EAM + production-grade fixed asset accounting with 22 depreciation methods, multi-book, Section 179, Canadian CCA, and tax form generation. This is our nuclear differentiator.

---

## 2.6 REPORTING & ANALYTICS

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Dashboard with KPIs | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Pre-built reports | ✅ (depreciation, compliance, tax) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Report builder (dynamic) | ✅ (ReportBuilderService 263 lines) | ✅ | ✅ | ✅ | 🟡 | ❌ |
| Excel export | ✅ (ClosedXML) | ✅ | ✅ | ✅ | ✅ | ✅ |
| PDF export | ✅ (QuestPDF) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Custom dashboards | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | 🟡 |
| Scheduled report delivery | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | ❌ |
| Natural language query | ❌ 🔮 | ✅ (Maximo Assistant) | ❌ | ❌ | ✅ (MaintainX AI) | ❌ |
| MTBF/MTTR analytics | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 2.7 INTEGRATIONS & API

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| REST API | ✅ (Assets, Items, Barcode, Auth, Analytics) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Outbound webhooks | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Inbound webhooks | ✅ (with signature verification) | ✅ | ✅ | ✅ | ✅ | 🟡 |
| ERP integration | ❌ 🔮 | ✅ | ✅ (native) | ✅ | ✅ (SAP, Oracle) | 🟡 |
| SSO (SAML/OIDC) | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | ✅ |
| IoT sensor ingestion | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ | 🟡 |
| CSV/Excel bulk import | ✅ (15 templates, MasterDataImportService 799 lines) | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 2.8 AI CAPABILITIES

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| AI assistant (chat) | ✅ (OpenAI integrated) | ✅ (GenAI) | ❌ | ✅ | ✅ | ❌ |
| AI work order intelligence | ❌ 🔮 | ✅ | ❌ | ✅ | ✅ | ❌ |
| AI procedure generation | ❌ 🔮 | ❌ | ❌ | ❌ | ✅ | ❌ |
| AI inventory forecasting | ❌ 🔮 | ✅ | ❌ | ✅ | ✅ | ❌ |
| Predictive failure detection | ❌ 🔮 | ✅ (Predict) | ❌ | ✅ (IFS.ai) | ✅ (anomaly) | ❌ |
| Visual inspection AI | ❌ 🔮 | ✅ (MVI) | ❌ | ❌ | ❌ | ❌ |
| AI scheduling optimization | ❌ 🔮 | 🟡 | ❌ | ✅ (#1) | 🟡 | ❌ |
| AI depreciation optimization | ❌ 🔮 | ❌ | ❌ | ❌ | ❌ | ❌ |
| Voice transcription | ❌ 🔮 | ❌ | ❌ | ❌ | ✅ | ❌ |

**CherryAI's AI opportunity:** AI depreciation optimization (recommending the best depreciation method/convention to minimize tax liability or maximize book value) is something NOBODY does. Combined with the AI assistant already integrated, this could be a first-to-market feature.

---

## 2.9 MULTI-TENANT / MULTI-COMPANY / MULTI-SITE

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Multi-tenant architecture | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Multi-company hierarchy | ✅ (N-level with VisibleCompanyIds) | ✅ | ✅ | ✅ | 🟡 | ❌ |
| Multi-site per company | ✅ (site scoping wired across 17 files) | ✅ | ✅ | ✅ | ✅ | 🟡 |
| Org → Company → Site → Location | ✅ | ✅ | ✅ | ✅ | 🟡 | ❌ |
| Per-user scope isolation | ✅ (AssignedCompanyId + VisibleCompanyIds) | ✅ | ✅ | ✅ | 🟡 | 🟡 |
| Cross-site reporting | 🟡 | ✅ | ✅ | ✅ | ✅ | ❌ |

**CherryAI's edge:** Full hierarchical multi-tenancy with N-level company hierarchy and per-user scope isolation. MaintainX has basic multi-site but nothing close to our organizational depth.

---

## 2.10 USER EXPERIENCE

| Feature | CherryAI | Maximo | SAP PM | IFS | MaintainX | UpKeep |
|---------|----------|--------|--------|-----|-----------|--------|
| Modern web UI | ✅ (dark/light, design tokens) | 🟡 (modernizing) | ❌ (legacy) | ✅ | ✅ | ✅ |
| Dark mode | ✅ (838 rules) | ❌ | ❌ | ❌ | ❌ | ❌ |
| Command palette (Ctrl+K) | ✅ (50+ routes) | ❌ | ❌ | ❌ | ❌ | ❌ |
| Guided tours | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Mobile app | ❌ 🔮 | ✅ | ✅ | ✅ | ✅ (#1) | ✅ |
| PWA | ❌ 🔮 | ❌ | ❌ | ❌ | ❌ | ❌ |
| Self-service setup | 🟡 | ❌ | ❌ | 🟡 | ✅ | ✅ |

**CherryAI's edge:** Dark mode with 838 CSS rules, command palette, and guided tours. NO competitor has all three. Maximo users need 45-page quick guides. SAP PM's UI is from the 2000s. CherryAI's UX is years ahead of every enterprise EAM.

---

# PART 3: PRIORITIZED ROADMAP

## Tier 1 — MUST-HAVE FOR APRIL LAUNCH (Fixed Assets for ABS Machining)

These must be 100% working before ABS Machining goes live:

| # | Item | Status | Effort |
|---|------|--------|--------|
| 1 | Fixed asset CRUD (create, edit, view, search, filter) | ✅ Working | — |
| 2 | Multi-book depreciation (all 22 methods, 12 conventions) | ✅ Working | — |
| 3 | Section 179 + Bonus Depreciation | ✅ Working | — |
| 4 | Canadian CCA | ✅ Working | — |
| 5 | Depreciation schedule export (Excel + PDF) | ✅ Working | — |
| 6 | Depreciation preview | ✅ Working | — |
| 7 | Form 4562 + T2 Schedule 8 | ✅ Working | — |
| 8 | GL account management | ✅ Working | — |
| 9 | Journal entries | ✅ Working (verify) | Small |
| 10 | Asset transfers, disposals, capital improvements | ✅ Working (verify) | Small |
| 11 | Multi-company scope isolation | ✅ Working | — |
| 12 | Multi-site scope isolation | ✅ Working | — |
| 13 | User authentication + role-based access | 🟡 Auth disabled in Program.cs | Medium |
| 14 | Data import for ABS's existing assets | ✅ Import wizard backend built | Medium (UI) |
| 15 | End-to-end regression testing all Fixed Asset workflows | 🟡 Needs formal test pass | Medium |
| 16 | Production deployment (off Replit to real hosting) | ❌ Not started | Large |

**Assessment:** Fixed Assets is 85-90% done. Remaining work is auth re-enable, import wizard UI, testing, and deployment.

---

## Tier 2 — MUST-HAVE FOR FULL EAM LAUNCH

These complete the full maintenance/purchasing/inventory story:

| # | Item | Priority | Status | Effort |
|---|------|----------|--------|--------|
| 1 | Meter reading entry UI | CRITICAL | ❌ Missing | Medium |
| 2 | Goods receiving POST handlers (verify/fix) | CRITICAL | 🟡 | Medium |
| 3 | PM auto-generation trigger (verify/wire) | CRITICAL | 🟡 | Medium |
| 4 | Inventory deduction on WO part issue (verify) | HIGH | 🟡 | Medium |
| 5 | Requisition → PO conversion workflow | HIGH | 🟡 | Medium |
| 6 | Failure code admin pages | HIGH | 🟡 | Small |
| 7 | CloseoutService wired to WO UI (verify) | HIGH | 🟡 | Small |
| 8 | WO detail pages reconciliation (2 pages → 1?) | HIGH | 🟡 | Medium |
| 9 | 3-way matching completion | MEDIUM | 🟡 | Medium |
| 10 | Data Management cockpit (consolidate 3 sidebar links) | MEDIUM | ❌ | Medium |
| 11 | Dashboard charts (recharts) | MEDIUM | ❌ 🔮 | Medium |
| 12 | Print/PDF export for POs and WOs | MEDIUM | ❌ 🔮 | Medium |
| 13 | MTBF/MTTR analytics | MEDIUM | ❌ 🔮 | Large |

**Assessment:** Items 1-8 are the "eight critical functional gaps" identified in the code audit. These should be audited and fixed BEFORE building anything new.

---

## Tier 3 — DIFFERENTIATORS (What Makes CherryAI Win)

These are features that put CherryAI ahead of the competition:

| # | Feature | Why It's a Differentiator | Effort |
|---|---------|--------------------------|--------|
| 1 | **AI depreciation optimization** | NOBODY does this. Recommend best method/convention to minimize tax or maximize book value. First-to-market. | Large |
| 2 | **Unified EAM + Fixed Assets** | Only product combining full CMMS/EAM + production-grade fixed asset accounting. Maximo can't do depreciation. MaintainX can't do GL entries. | Already built |
| 3 | **Ultra-luxury UX** | Dark mode + command palette + guided tours + design tokens. Makes Maximo look like 1995, makes SAP look like a spreadsheet. | Ongoing |
| 4 | **Graphical scheduling (FullCalendar resource timeline)** | Technician-by-week board + asset timeline. Visual like MaintainX but structurally deep like Maximo. | Large |
| 5 | **AI-assisted scheduling** | "Here's what I'd recommend and why" — conversational scheduling via Claude API. Nobody does this well. | Large |
| 6 | **Canadian compliance** | CCA + T2 Schedule 8. Zero EAM competitors serve the Canadian tax market properly. | Already built |
| 7 | **Buyability scoring** | Unique inventory intelligence feature. No competitor has it. | Already built |
| 8 | **PM template revision control** | Version-controlled maintenance procedures. Enterprise rigor that CMMS tools lack. | Already built |
| 9 | **Asset-level BOMs** | Per-asset bill of materials showing every part that machine needs. Deeper than MaintainX/UpKeep flat lists. | Medium |
| 10 | **Operation predecessor relationships** | Sequence dependencies between WO operations. SAP/Maximo level depth. | Medium |

---

## Tier 4 — FUTURE VISION (12-24 months)

| # | Feature | Market Signal | Effort |
|---|---------|---------------|--------|
| 1 | Mobile app (PWA → native) | MaintainX's #1 advantage. Mandatory for field technicians. | Very Large |
| 2 | IoT sensor ingestion | Market is moving to condition-based. Need MQTT/OPC-UA bridge. | Very Large |
| 3 | Predictive maintenance (ML) | IBM/IFS lead here. Failure prediction from sensor + WO history data. | Very Large |
| 4 | Digital twin visualization | 3D asset models with real-time sensor overlay. IBM Maximo leads. | Very Large |
| 5 | SSO (SAML/OIDC) | Enterprise requirement for companies >500 employees. | Medium |
| 6 | ERP connectors (SAP, Oracle, QuickBooks, Sage) | Integration marketplace. Start with QuickBooks for SMB. | Large |
| 7 | AI visual inspection | Camera/drone-based defect detection. IBM MVI leads. | Very Large |
| 8 | Emissions/sustainability tracking | IBM's 2025 roadmap includes carbon emissions module. Regulatory pressure growing. | Large |
| 9 | Field service management (dispatching + routing) | IFS and IBM both adding FSM. Natural extension of WO scheduling. | Very Large |
| 10 | Natural language analytics | "Show me all overdue PMs for Site 3 this month" → instant chart. MaintainX already has this. | Large |

---

# PART 4: THE COMPETITIVE POSITIONING STATEMENT

## What CherryAI IS:
**The only EAM platform that combines enterprise-grade maintenance management with production-grade fixed asset accounting in a single, beautiful interface.**

## What CherryAI IS NOT:
- Not a lightweight CMMS (we have multi-operation WOs, 4-level failure codes, AP, GL integration)
- Not a legacy ERP bolt-on (we're cloud-native with modern UX)
- Not a one-size-fits-all tool (we're built for asset-intensive industries)

## Our pitch to a $500M company:
"You currently run Maximo for maintenance and SAP FI-AA for fixed assets. That's two systems, two vendors, two integration projects, two training programs. CherryAI gives you both in one platform — with a UI your team will actually want to use. Your Maximo admin needs a 45-page quick guide. Our command palette gets users anywhere in 2 keystrokes."

## Our pitch to a growing manufacturer:
"MaintainX is great for tracking work orders on your phone. But when your CFO asks for a depreciation schedule, when you need to capitalize a CIP project, when you need 3-way matching on a $50K bearing order — MaintainX can't help. CherryAI can. And it still has the modern UX you love."

---

# PART 5: IMMEDIATE NEXT STEPS

1. **Run the Full App Scan** (Task file provided) — Get the complete current state into a proof bundle
2. **Upload the scan bundle here** — I'll review every evidence file and build the exact punch list
3. **Audit the 8 critical functional gaps** — Verify which ones are actually broken vs. working
4. **Fix what's broken** — One by one, with proof bundles for each
5. **Re-enable auth** — Flip the switch in Program.cs, test all 3 roles
6. **Import wizard UI** — I'll write the complete .cshtml for the Data Management cockpit
7. **April launch prep** — Production deployment planning for ABS Machining

The scan is Step 1. Everything else flows from knowing the exact current state. Let's go.
