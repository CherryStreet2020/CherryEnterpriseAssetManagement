# 09 — Disruption Playbook & 90-Day Roadmap

The full competitive analysis is in `08_COMPETITIVE_GAP_ANALYSIS.md`. This file is the action plan that comes out of it — what to build, in what order, with what kill shot against which competitor. Designed for Claude Code to execute one bet at a time.

---

## The strategic shape

Three things have to happen in order to dethrone IBM Maximo, SAP EAM, Hexagon HxGN EAM, IFS Cloud, MaintainX, and Accruent:

1. **Production hardening** (Sprint 0) — re-enable auth, upgrade password hashing, finish FK migration, ship OpenAPI spec. ~2 weeks. No customer touches the system until this is done.
2. **Wedge feature** (Sprints 1-3) — ship the AI-Native Mobile Voice-to-Work-Order flow that no incumbent can copy in under 18 months. This is the demo that ends the meeting.
3. **Defense in depth** (Sprints 4-6) — fill the gaps competitors will point to (calibration, warranty, IoT live, LOTO) so they have nothing left to attack with.

90 days. Six sprints of two weeks each.

---

## Sprint 0 — Production Hardening (Weeks 1-2, the gating sprint)

**Goal:** Make CherryAI EAM safe for the first paying customer.

### Tasks

| # | Task | Files | Effort | Why |
|---|---|---|---|---|
| 1 | Finish FK-bound dropdown migration on PO details | `Pages/Purchasing/Details.cshtml.cs` (`OnPostUpdateHeaderAsync`, status workflow buttons, `OnPostDuplicatePOAsync`) | M | Last gap in the FK pattern; everything else is migrated |
| 2 | Backfill FK values for legacy rows | New service `FkBackfillService` that walks every table with a `*LookupValueId` column, finds rows where FK is null but enum has a value, resolves FK by Code match | M | Closes the migration loop; lets us start removing legacy enum columns next quarter |
| 3 | Re-enable authentication | `Program.cs` ~line 282 — remove anonymous fallback, ensure all Razor pages require `[Authorize]` | S | Required before any production customer logs in |
| 4 | Upgrade password hashing to Argon2id | `Services/AuthService.cs` — replace SHA-256 with `Konscious.Security.Cryptography.Argon2`. Add migration to mark legacy hashes for re-hash on next login | M | SHA-256 for passwords is unacceptable in 2026 |
| 5 | Move secrets out of `appsettings.Development.json` | Move Azure SQL connection string to user-secrets or `dotnet user-secrets`; document the env var fallback | S | Plaintext password in source control is a leak waiting to happen |
| 6 | Configure OpenAI key (or fall back gracefully) | `AiAssistantService.cs` — add a fail-safe path; read from key vault in prod | S | Service throws today if key missing |
| 7 | Generate OpenAPI / Swagger spec | Add `Swashbuckle.AspNetCore` package; register `AddEndpointsApiExplorer` and `AddSwaggerGen`; expose `/swagger` (Admin role only in prod) | S | Required for any integration partner; SaaS table stakes |
| 8 | Add Serilog with Seq or File sink | Replace built-in `ILogger` config in Program.cs with Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.Seq`); preserve OTel traces | M | Production observability needs structured search |
| 9 | Add distributed lock to seed guard | `SeedGuardService.cs` — acquire a Postgres advisory lock before checking + setting the seed-already-ran marker | S | Prevents double-seed under concurrent startup |
| 10 | Strongly-type outbox event payloads | `Services/Webhooks/OutboxWriter.cs` — introduce `IDomainEvent` with versioned schemas; serializer enforces type | M | Schema drift is the #1 silent failure mode in webhook-based integrations |

### Done when
- Build still 0 errors
- Auth required on every Razor page
- Login succeeds with Argon2id-hashed test user
- `/swagger` returns the full API contract
- A second startup process can't double-seed (verified with a stress test)
- Swap Postgres connection string to a fresh DB and the FK backfill service runs to clean state in <60 seconds for a 100k-asset dataset

### Kill shot
None — this is just clearing the runway. But shipping all 10 of these in two weeks is itself a credibility statement.

---

## Sprint 1 — PWA Mobile Work Order Execution (Weeks 3-4)

**Goal:** Technicians on the shop floor stop carrying laptops.

**The thesis:** MaintainX's whole product is "mobile-first WOs that work offline." Maximo's mobile story is a $50K add-on (Maximo Mobile) that requires a separate IBM MobileFirst stack. Hexagon EAM Mobile is also a paid add-on. Accruent has Maintenance Connection Mobile but it's a thin wrapper. SAP requires SAP Field Service Management ($$$$). **Shipping a real PWA in CherryAI's existing Razor stack — no separate mobile team — is the wedge.**

### Tasks

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | Add a manifest.json + service worker to make the app PWA-installable | `wwwroot/manifest.json`, `wwwroot/sw.js` | S |
| 2 | Build a mobile-first WO execution view at `/m/wo/{id}` that fits a phone screen | New page `Pages/Mobile/WorkOrder.cshtml` + page model | M |
| 3 | Cache assigned WOs and asset master in IndexedDB; sync queue for offline edits | New JS module `wwwroot/js/offline-sync.js`; new controller `Controllers/OfflineSyncController.cs` for batch upload | L |
| 4 | Add a QR/barcode scanner using device camera (`getUserMedia` + `BarcodeDetector` API) — scan to open asset or WO | New JS module `wwwroot/js/scanner.js`; new page `/m/scan` | M |
| 5 | Add photo upload (camera roll or live capture) attached to WO operations | Extend `AttachmentService` to handle multipart; mobile UI for capture | M |
| 6 | Add e-signature pad for closeout sign-off | New JS canvas component; store as Attachment with category `signature` | S |
| 7 | Add a "next assigned WO" tile + accept/start/pause/complete flow | Mobile page additions | S |
| 8 | Add a one-tap labor-time tracker (start clock, pause, stop → adds to WO labor entry) | Mobile JS + new API endpoint | M |

### Done when
- A technician can install CherryAI to their phone home screen, open a WO, scan an asset QR, log labor time, attach 3 photos, sign on the screen, and close the WO — **all while in airplane mode** — and it syncs the moment connectivity returns

### Kill shot
**MaintainX.** Their whole pitch is "mobile-first." Now CherryAI's mobile is just as good *and* it's bundled with the depreciation engine and CIP that MaintainX doesn't have.

---

## Sprint 2 — AI-Native Voice-to-Work-Order (Weeks 5-6)

**Goal:** A technician can say "the south compressor is making a grinding noise, looks like a bearing, parts are in the shop, I'll need 2 hours" and CherryAI creates a complete WO with asset, failure code, action code, parts requisition, labor estimate, technician assigned — submitted in under 60 seconds.

**The thesis:** SmartAssistService is already wired up with keyword inference. Replacing the regex with a Claude API call is a 1-week build that makes the whole product feel five years ahead. **No competitor has voice-to-WO with full domain understanding. Maximo can't build this fast — their data model is fragmented across 5 apps; ours is one PostgreSQL schema.**

### Tasks

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | Add Web Speech API voice capture to mobile + desktop | New JS module `wwwroot/js/voice-capture.js`; "🎤" button on `/Maintenance/WorkRequests/Create` and mobile `/m/wo/new` | S |
| 2 | Replace `SmartAssistService.AnalyzeRequestAsync()` regex with a Claude API call | `Services/SmartAssistService.cs` — add `IAnthropicClient` (or use OpenAI for now), structured tool-use prompt with: list of visible assets, list of failure/action codes from LookupService, technician roster | M |
| 3 | Define a structured prompt that returns JSON: `{assetId, failureCode, actionCode, priority, estimatedLaborHours, suggestedParts[], technicianId, summary}` | Prompt in `Services/SmartAssistService.cs`; JSON schema validation | M |
| 4 | Pre-populate the new-WO form with the AI's draft; user reviews + tweaks + submits | Update `Pages/Maintenance/Create.cshtml.cs` to consume `SmartAssistResult` | S |
| 5 | Add a confidence badge ("⚡ AI suggested with 92% confidence") and "why?" hover that shows the reasoning | UI partial | S |
| 6 | Add an analytics counter — track AI-vs-manual WO creation, accept rate, edit rate | New table `AiSuggestionLogs`; new admin page `Admin/AiAnalytics.cshtml` | M |
| 7 | Add a kill switch in `Admin/SystemSettings` — turn off AI assist per company | Settings UI + service check | S |

### Done when
- "🎤 The roller bearing on conveyor 3 is squealing again, needs replacement, give me 4 hours" → AI creates a WO drafted with: asset = "Conveyor 3", failure = ROLL-BRG-FAIL, action = REPLACE, priority = High, labor = 4 hours, technician = (best match by skills/availability), parts requisition for "roller bearing" against the AVL, and a closeout note pre-filled with "Replace roller bearing on conveyor 3 (recurring — see WO #4517, #3892)" — all in one screen with one tap to confirm

### Kill shot
**IBM Maximo + SAP EAM.** Both are 15-year-old codebases. Bolting on a real Claude integration takes them quarters of work because their data model lives across modules. CherryAI does it in a sprint because the data is in one schema and we own the prompts.

---

## Sprint 3 — Conversational Asset Twin (Weeks 7-8)

**Goal:** Every asset has a "💬 Chat with this asset" button. Tap it, ask "why did you fail last month?", get a full answer pulling from work order history, IoT telemetry (when present), maintenance lessons learned, vendor manuals (attachments), and parts cross-references.

**The thesis:** This is the singular feature. **No EAM has it.** Maximo customers have to manually cross-query 5 modules. CherryAI's normalized PostgreSQL + AiAssistantService context aggregation already does most of the work. We just expose it as chat.

### Tasks

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | Add `Pages/Assets/Chat.cshtml` — chat UI scoped to a single asset | New Razor page + page model | M |
| 2 | Add "💬 Chat with this asset" button to `Pages/Assets/Asset.cshtml` (top-right of header) | UI partial | S |
| 3 | Build `AssetTwinContextService` that aggregates for an asset: full WO history, recent IoT telemetry (if IoTEnabled), all lessons learned, all attachments with metadata, parts BOM, depreciation schedule, transfer history, asset-specific PM templates | New service `Services/AssetTwinContextService.cs` | L |
| 4 | Push the aggregated context to Claude as system prompt; user message goes through unchanged | Update or extend `AiAssistantService` | M |
| 5 | Add a "tools" mode: Claude can call `CreateWorkOrder`, `RequestPart`, `EscalatePriority`, `OpenManual(attachmentId)` from inside the chat | Tool-use schemas; new API surface | L |
| 6 | Persist chat history per asset per user | New model `AssetChatThread`, `AssetChatMessage`; migration | S |
| 7 | Surface "asset twin insights" on the asset detail page: top 3 recurring failures, top 3 vendors, MTBF, last 5 PM events, current calibration status | UI partial | M |

### Done when
- A maintenance manager can open Asset 2045 (a CNC machine), tap 💬, ask "why has this failed three times this year?", and get back: "Three failures in 2026 — all related to spindle bearing wear (codes BRG-WEAR-101 in March, BRG-WEAR-205 in April, BRG-WEAR-301 in June). Average time-to-failure between events: 47 days. The vendor (Acme Bearings) has documented this issue in their 2025 service bulletin (attachment #3247). Recommend switching to alternate part #ALT-BRG-9871 from approved vendor SKF — buyability score 94. Want me to draft the requisition?"

### Kill shot
**Everyone.** This is the demo that closes deals. Schedule it for the last 5 minutes of every customer call. Then watch the Maximo champion in the room go quiet.

---

## Sprint 4 — Real-Time IoT Dashboard (Weeks 9-10)

**Goal:** Every asset with `IoTEnabled = true` gets a live tile on a "Pulse" dashboard with current OEE, alarms, and a link to the asset twin chat.

**The thesis:** The Asset model already has IoT and OEE fields (`IoTEnabled`, `IoTDeviceId`, `IoTProtocol`, `CurrentAvailability/Performance/Quality/OEE`, `LastIoTCommunication`). Hexagon HxGN EAM has this; it's their main wedge against Maximo. **We don't need to build the IoT collection — we just need the dashboard that consumes the data already in the schema.** Customers can wire up their own MQTT/OPC-UA collectors via the existing inbound webhook receiver.

### Tasks

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | New page `Pages/Pulse/Index.cshtml` — grid of live asset tiles | New page + model | M |
| 2 | Add SignalR hub for live updates | New hub `Hubs/PulseHub.cs`; register in Program.cs | M |
| 3 | When inbound webhook arrives with type `iot.telemetry`, broadcast to PulseHub subscribers + update Asset.Current* OEE fields | Update `InboundEventProcessorHostedService` + IntegrationMappingService | M |
| 4 | Add a per-tile sparkline (last 24h availability/performance/quality) | Chart.js sparkline; new endpoint to fetch 24h history | M |
| 5 | Add alarm badges — when OEE drops below `TargetAvailability/Performance/Quality`, tile turns red and triggers an outbox event `iot.alarm.fired` | Service logic in mapping service | M |
| 6 | Add a "create WO from alarm" button right on the tile | One-click flow that uses SmartAssistService with alarm context | S |
| 7 | Document MQTT collector pattern in `docs/IoT-Integration-Patterns.md` so customers can self-serve | Docs | S |

### Done when
- A plant manager opens `/Pulse` and sees 47 live asset tiles, 3 of them red. Tap a red one → drill into asset twin → see the spike in vibration → tap "Create WO" → AI drafts a WO with predictive-maintenance failure code → submitted

### Kill shot
**Hexagon HxGN EAM.** Their reliability story is the moat. Now ours is too — and we're $5K/year vs. their six-figure license.

---

## Sprint 5 — Calibration Management & LOTO Permits (Weeks 11-12)

**Goal:** Close the regulated-industries objection. Pharma, food/bev, aerospace, energy customers refuse to look at any EAM that doesn't have calibration management and lockout/tagout. Asset model already has calibration fields — we just need workflows.

### Calibration Management

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | New entity `CalibrationRecord` (assetId, performedDate, performedBy, calibrationType, results JSONB, certificate attachmentId, nextDue, status). Migration | `Models/CalibrationRecord.cs` | S |
| 2 | New service `CalibrationService` with `GetDueAsync`, `RecordCalibrationAsync`, `ScheduleNextAsync` | `Services/CalibrationService.cs` | M |
| 3 | Pages: `Calibration/Index.cshtml` (list of due/overdue), `Calibration/Record.cshtml` (record a calibration) | New pages | M |
| 4 | KPI tile on dashboard: "Calibrations due in 30 days" | KPI service | S |
| 5 | Outbox event `calibration.due`, `calibration.overdue`, `calibration.recorded` | Wire into existing outbox | S |
| 6 | Compliance report: "Calibration certificate library" — PDF export of all certificates for an audit | New report page using QuestPDF | M |

### LOTO (Lockout-Tagout) Permits

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | New entities: `LotoProcedure` (asset, hazard description, lockout points JSONB, required PPE, sequence), `LotoPermit` (procedureId, woId, issuedTo, issuedBy, issuedAt, returnedAt, status). Migration | `Models/Loto*.cs` | M |
| 2 | New service `LotoService` | New file | M |
| 3 | Pages: `Loto/Procedures.cshtml` (procedure library), `Loto/Permits.cshtml` (active permits), `Loto/Issue.cshtml` (issue a permit), `Loto/Return.cshtml` (return) | New pages | M |
| 4 | Mobile: technician can scan asset QR, see required LOTO procedure, tap each lockout point as completed, snap photo, get permit | Mobile additions | M |
| 5 | Block WO closeout if a LOTO permit is issued and not yet returned | Add check to `CloseoutService.CloseWorkOrderAsync` | S |

### Done when
- Calibration: 47 calibration records imported via CSV; dashboard shows 8 due in 30 days; tech records a calibration with certificate upload; certificate library PDF exports for the FDA auditor
- LOTO: a technician scans the safety lock on a press, gets the procedure, completes 4 lockout points, takes 4 photos, returns the permit on closeout

### Kill shot
**SAP EAM + IFS Cloud.** These two compete heavily for regulated industries. Now CherryAI is in that conversation.

---

## Sprint 6 — Warranty + Contractor + Documents (Weeks 13-14)

**Goal:** Eliminate the last three "yes but does it have…" objections.

### Warranty Management

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | New entity `Warranty` (assetId, type [original/extended], coverageStart, coverageEnd, vendorId, terms JSONB, claimsCount). Migration | New file | S |
| 2 | New entity `WarrantyClaim` (warrantyId, woId, submittedDate, status, recoveredAmount, notes). Migration | New file | S |
| 3 | New service `WarrantyService` with `GetExpiringAsync(days)`, `FileClaimAsync` | New file | M |
| 4 | Pages: `Warranty/Index.cshtml`, `Warranty/Asset.cshtml/{assetId}` | New pages | M |
| 5 | KPI: "Warranty expiring in 90 days" tile + email digest | KPI + scheduled task | S |
| 6 | Auto-suggest warranty claim filing during WO closeout if asset is under warranty | Add to `CloseoutService` flow | M |

### Contractor Management

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | New entity `Contractor` (name, contact, insuranceExpiry, certifications JSONB, badgeNumber, status). Migration | New file | S |
| 2 | New entity `ContractorAssignment` (contractorId, woId, assignedAt, completedAt) | New file | S |
| 3 | Pages: `Admin/Contractors.cshtml`, `Admin/ContractorEdit.cshtml` | New pages | M |
| 4 | Block WO assignment to contractor with expired insurance or expired cert | Validation in WO assignment service | S |
| 5 | Warning dashboard: insurance expiring in 30 days, certs expiring in 60 days | KPI service | S |

### Document Management

| # | Task | Files | Effort |
|---|---|---|---|
| 1 | Extend Attachment with `Version`, `IsLatest`, `SupersededByAttachmentId` | Migration + model update | S |
| 2 | Add file viewer for PDFs in-page (PDF.js) | Static lib + UI partial | S |
| 3 | Add OCR pipeline for uploaded vendor invoices/manuals (call out to OpenAI Vision or a separate OCR service) | New service `OcrService` | L |
| 4 | When user uploads a new version, link to the previous and mark superseded | Service logic | S |

### Done when
- An asset's warranty is automatically checked on every WO; a tech is told "this WO might be covered under warranty — want to file a claim?"
- A contractor with expired insurance can't be assigned a new WO
- A pdf manual uploaded to an asset is searchable by content (OCR'd) and has version history

### Kill shot
**The objections list runs out.** At this point CherryAI has every "must have" feature of the major EAMs *plus* the AI/voice/PWA wedge. Sales conversations stop being "do you have X?" and start being "show me how X works."

---

## After 90 days — what to build next

**Tier 2 expansion (the next 90 days):**
- Predictive maintenance ML (vibration analysis, oil analysis, RUL estimation) — Sprint 7-8
- Linear assets (pipelines, roads, conveyors with KP/segment/station modeling) — Sprint 9
- Energy monitoring + sustainability/ESG reporting — Sprint 10
- Visual no-code workflow builder for approvals — Sprint 11
- Plugin economy: customers write their own Skills against CherryAI data via Claude — Sprint 12

**Pricing & GTM (parallel track):**
- Launch **Launchpad** tier as freemium ($0 / 25 assets / 1 user)
- Launch **Autopilot** tier at $15/user/month (vs. MaintainX $49)
- Launch **Command Center** tier at $99/user/month (vs. Maximo $300+/user/month)
- Single-page self-serve onboarding wizard (the SeedPackV2 already does the heavy lifting)
- 30-second demo video showing voice-to-WO + asset twin chat

---

## What I'd ask Claude Code to do first

If you only have time for one task this week: **finish Sprint 0 task #1 (PO Details FK migration) and task #3 (re-enable auth).** Those two unblock everything.

If you have time for the wedge: **start Sprint 1 (PWA mobile) + Sprint 2 (voice-to-WO).** They're additive — both can ship in parallel, and together they're the demo that wins.

The ordering above is recommended, not mandatory. The big rule is: **don't ship Sprint 1+ to a paying customer until Sprint 0 is done.** Auth, password hashing, secrets — those are non-negotiable.

---

## Why this works

The competitive analysis (file 08) lays out the matrix. The TL;DR is:

- **The tech stack is already a generation ahead** (.NET 9 / PostgreSQL / Razor / Claude-ready vs. WebSphere/ABAP/Java 8).
- **The financial engine is already deeper than most competitors** (multi-method, multi-convention, MACRS + CCA + bonus depr + Section 179 in one app).
- **The architecture choices are right** (multi-tenant from day one, lookup-driven everything, transactional outbox, FK-bound dropdowns).
- **The UX is already premium** (DataGrid, KPI cards, dark mode, command palette).
- **What's missing is mostly UI on top of data that already exists** (calibration, IoT live dashboard, warranty workflow, mobile WO).

CherryAI doesn't need to outspend IBM. It needs to ship the demo that makes the buyer say "wait, why doesn't Maximo do that?" — and then keep shipping for 6 months while incumbents try to retrofit.

The visionary instinct that built the bones is right. Now it's about flipping the switches in the right order.
