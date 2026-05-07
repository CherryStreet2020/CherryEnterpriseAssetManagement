# CherryAI Enterprise Asset Management - Controllers & API Surface Audit

**Report Date:** May 7, 2026  
**Framework:** ASP.NET Core 9.0  
**Scope:** Comprehensive controller and API endpoint inventory, authentication mechanisms, and security posture

---

## Executive Summary

The CherryAI EAM application exposes a moderately complex RESTful API surface with 10 primary controllers serving core domain operations (assets, items, barcodes, analytics, backups, webhooks, authentication, etc.). The API uses a hybrid authentication model combining X-API-Key validation, tenant context headers, and role-based authorization. A critical requirement exists: all `/api/v1/` endpoints demand explicit header validation (X-Tenant-Id, X-User-Id, X-Org-Node-Id) via middleware. Public endpoints exist but are limited; most require either API key or authenticated session context.

**Key Findings:**
- 2 Razor Page endpoints for admin API key management and CSV import
- 21 documented detail types supported via polymorphic `/api/v1/details/{type}/{id}` endpoint
- Webhook endpoint supports HMAC signature validation and idempotency
- Database backup endpoint exposed but gated behind environment and feature flags
- No GraphQL, OData, or OpenAPI/Swagger documentation discovered
- Concurrency control via ETags on asset updates
- Comprehensive tenant/org scoping via ITenantContext service

---

## 1. Controller-by-Controller Inventory

### 1.1 AnalyticsController

**File:** `Controllers/AnalyticsController.cs`  
**Route Prefix:** `api/v1/analytics`  
**Class Attributes:** `[Route("api/v1/analytics")]`, `[ApiController]`  
**Auth:** No explicit `[Authorize]` attribute (accessible to authenticated context)  

**Dependencies Injected:**
- `AppDbContext _db`
- `ITenantContext _tenantContext`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `drilldown` | `GetDrilldown` | `type` (string, default="invoice"), `limit` (int, default=50), `offset` (int, default=0) | JSON with `{ type, total, offset, limit, rows }` | Paginated drill-down across multiple entity types (invoice, purchase_order, vendor_invoice, maintenance_event, asset). Filtered by tenant context (companyId, siteId). |
| GET | `kpis` | `GetKpis` | (none) | JSON array of KPI tiles: total_assets, asset_value, open_pos, po_value, total_invoices, invoice_revenue, work_orders | Aggregated dashboard metrics, tenant-scoped. |

**Security Notes:**
- No explicit auth checks; relies on implicit tenant context availability.
- Drilldown type validation is basic (switch case) with default BadRequest on unknown type.

---

### 1.2 AssetsApiController

**File:** `Controllers/AssetsApiController.cs`  
**Route Prefix:** `api/v1/assets`  
**Class Attributes:** `[ApiController]`, `[Route("api/v1/assets")]`  
**Auth:** Custom `ValidateApiKey()` method on all endpoints (requires X-API-Key header)  

**Dependencies Injected:**
- `AppDbContext _context`
- `ApiService _apiService`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose | Auth |
|------|-------|--------|-----------|---------|---------|------|
| GET | `(empty)` | `GetAssets` | `page` (int, default=1), `pageSize` (int, default=50), `location` (string?), `status` (string?) | `ApiResponse<List<AssetDto>>` with Success, Data, TotalCount | List assets with optional location & status filters, paginated. | X-API-Key |
| GET | `{id}` | `GetAsset` | `id` (int) | `ApiResponse<AssetDto>` + ETag header | Retrieve single asset. Response includes ETag for concurrency control. | X-API-Key |
| POST | `(empty)` | `CreateAsset` | Body: `CreateAssetRequest { AssetNumber, Description, Model, SerialNumber, InServiceDate, AcquisitionCost, SalvageValue, UsefulLifeMonths, Department }` | `ApiResponse<AssetDto>` (201 Created) | Create new asset. Returns 400 if AssetNumber already exists. | X-API-Key |
| PUT | `{id}` | `UpdateAsset` | `id` (int), Body: `UpdateAssetRequest { Description?, Department?, Status? }`, Header: `If-Match` (ETag) | `ApiResponse<AssetDto>` | Update asset with optimistic concurrency control via ETag. Returns 428 if If-Match missing, 409 if conflict. | X-API-Key |
| DELETE | `{id}` | `DeleteAsset` | `id` (int) | `ApiResponse<object>` | Mark asset as Disposed (soft delete). | X-API-Key |

**Security & Implementation Details:**
- **Concurrency:** Implements RFC 7232 ETags and If-Match precondition headers. RowVersion (4-byte base64 encoded) used for conflict detection.
- **API Key Validation:** `ValidateApiKey()` checks X-API-Key header, delegates to `ApiService.ValidateKeyAsync()`.
- **Input Validation:** Minimal; checks for required fields on POST, relies on enum parsing for status.
- **Soft Deletes:** DELETE marks asset as Disposed rather than hard delete.

---

### 1.3 AuthController

**File:** `Controllers/AuthController.cs`  
**Route Prefix:** `auth`  
**Class Attributes:** `[ApiController]`, `[Route("auth")]`  
**Auth:** None (AllowAnonymous)  

**Dependencies Injected:** None

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `whoami` | `WhoAmI` | (none) | JSON with `{ isAuthenticated, name, roles[], claims[] }` | Diagnostic endpoint returning current user identity, roles, and claims from ClaimsPrincipal. |

**Security Notes:**
- Entirely public; useful for debugging/testing identity context.
- Returns all claims (including potentially sensitive metadata).

---

### 1.4 BackupController

**File:** `Controllers/BackupController.cs`  
**Route Prefix:** `api/[controller]` (resolves to `api/backup`)  
**Class Attributes:** `[ApiController]`, `[Route("api/[controller]")]`  
**Auth:** None (environment gating only)  

**Dependencies Injected:**
- `AppDbContext _context`
- `IWebHostEnvironment _environment`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose | Gating |
|------|-------|--------|-----------|---------|---------|--------|
| GET | `export` | `ExportDatabase` | (none) | File (JSON) with all DbSet data as `{ TableName: [...], _metadata: {...} }` | Full database export as JSON. Requires Development environment + `ALLOW_DB_BACKUP=true` env var. | Environment, Feature Flag |
| GET | `status` | `GetStatus` | (none) | JSON with `{ environment, isDevelopment, database, backupEnabled, resetProtected }` | Diagnostic endpoint returning backup/reset status. Always accessible. | None |

**Security & Implementation Details:**
- **Environment Gating:** Only runs in Development environment.
- **Feature Flag:** Requires explicit `ALLOW_DB_BACKUP=true` environment variable.
- **Export Mechanism:** Uses reflection to iterate all DbSet<T> properties, calls ToListAsync on each.
- **Error Handling:** Swallows exceptions per entity (line 72: `catch { backup[prop.Name] = new { error = "Failed to export" }; }`), continues export.
- **Metadata:** Includes exported timestamp, environment name, database name, table count.

**Risks:**
- Full unencrypted database dump in JSON; no field masking or PII sanitization.
- Exception swallowing masks data access errors.
- No audit logging of export events.

---

### 1.5 BarcodeApiController

**File:** `Controllers/BarcodeApiController.cs`  
**Route Prefix:** `api/barcode`  
**Class Attributes:** `[ApiController]`, `[Route("api/barcode")]`  
**Auth:** None (all endpoints public)  

**Dependencies Injected:**
- `AppDbContext _db`
- `IBarcodeService _barcodeService`
- `IItemStockingService _stockingService`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `generate/{itemId}` | `GenerateBarcode` | `itemId` (int), `width` (int, default=300), `height` (int, default=100) | PNG image bytes (Content-Type: image/png) | Generate barcode image for an item. |
| GET | `label/{itemId}` | `GenerateLabel` | `itemId` (int), `width` (int, default=400), `height` (int, default=200) | PNG image bytes (Content-Type: image/png) | Generate barcode label with item metadata overlay. |
| POST | `scan` | `ScanBarcode` | Body: `ScanRequest { ImageBase64, CompanyId? }` | `ScanResponse { Success, BarcodeValue?, ItemId?, PartNumber?, Description?, QuantityOnHand?, Location?, AssetId?, AssetNumber?, Message }` | Decode barcode from base64 image; cross-reference Items and Assets tables. Returns item stocking info and location. |
| GET | `lookup/{barcodeValue}` | `LookupBarcode` | `barcodeValue` (string), `companyId?` (int?) | `LookupResponse { Found, Type, ItemId?, PartNumber?, QuantityOnHand?, ReorderPoint?, IsLowStock?, Location?, UnitCost?, AssetId?, AssetNumber?, LocationName?, Status? }` | Resolve barcode/part-number string to Item or Asset; return inventory & stocking info. |
| POST | `batch-print` | `BatchPrintLabels` | Body: `BatchPrintRequest { ItemIds[], Width?, Height? }` | JSON with `{ labels: [ { ItemId, PartNumber, ImageBase64 } ] }` | Generate barcode labels for multiple items in one request. |

**Security Notes:**
- **No auth requirement:** Barcode endpoints are public; may leak inventory locations and part numbers.
- **Barcode Service Dependency:** Handles generation/decoding; returns 503 if unavailable.
- **Cross-Entity Lookup:** Matches against both Item.Barcode/PartNumber and Asset.AssetNumber/SerialNumber.
- **Location Exposure:** Returns formatted warehouse/aisle/rack/shelf/bin locations for items.

---

### 1.6 DetailController

**File:** `Controllers/DetailController.cs`  
**Route Prefix:** `api/v1/details`  
**Class Attributes:** `[Route("api/v1/details")]`, `[ApiController]`  
**Auth:** None (relies on tenant context)  

**Dependencies Injected:**
- `AppDbContext _db`
- `ITenantContext _tenantContext`

**Polymorphic Detail Endpoint:**

| HTTP | Route | Method | Supported Types | Implementation |
|------|-------|--------|-----------------|-----------------|
| GET | `{type}/{id}` | `GetDetail` | 21 types (see detail_contract.json) | Massive switch statement routing to 21+ private methods based on type string. |

**Supported Entity Details (from switch case):**

1. **asset** → `GetAssetDetail(int)` — Returns asset with books, work orders
2. **purchase_order** → `GetPurchaseOrderDetail(int)` — Returns PO with lines
3. **vendor_invoice** → `GetVendorInvoiceDetail(int)` — Returns VI with summary
4. **maintenance_event** / **work_order** → `GetMaintenanceEventDetail(int)` — Returns WO with operations
5. **vendor** → `GetVendorDetail(int)` — Returns vendor with recent POs
6. **site** → `GetSiteDetail(int)` — Returns site with locations, assets
7. **location** → `GetLocationDetail(int)` — Returns location with assets
8. **company** → `GetCompanyDetail(int)` — Returns company with sites, asset count
9. **book** → `GetBookDetail(int)` — Returns depreciation book config
10. **item** → `GetItemDetail(int)` — Returns item with vendor parts
11. **cip_project** → `GetCipProjectDetail(int)` — Returns capital project with costs, WOs, POs, invoices, journals, assets
12. **fiscal_year** → `GetFiscalYearDetail(int)` — Returns fiscal year with periods
13. **fiscal_period** → `GetFiscalPeriodDetail(int)` — Returns fiscal period
14. **lookup_type** → `GetLookupTypeDetail(int)` — Returns lookup configuration with values
15. **pm_schedule** → `GetPmScheduleDetail(int)` — Returns preventive maintenance schedule
16. **pm_template** → `GetPmTemplateDetail(int)` — Returns PM template with revisions
17. **org_node** → `GetOrgNodeDetail(string)` — Returns org hierarchy node (GUID) with children
18. **user** → `GetUserDetail(int)` — Returns user profile with role, preferences
19. **journal_entry** → `GetJournalEntryDetail(int)` — Returns journal with line items and debits/credits
20. **work_request** → `GetWorkRequestDetail(int)` — Returns work request with AI analysis metadata
21. **gl_account** → `GetGlAccountDetail(int)` — Returns GL account with hierarchy

**Response Format (consistent across all types):**
```json
{
  "type": "string",
  "id": "int or guid",
  "header": { /* 12+ key fields */ },
  "sections": [ { "name": "string", "fields": { /* structured fields */ } } ],
  "related": { /* child entity lists */ }
}
```

**Security Notes:**
- **Tenant Isolation:** Uses `_tenantContext` for some queries (CIP projects, assets) but **NOT consistently**. Some detail methods (vendor, user, gl_account) do NOT filter by tenant.
- **Lazy Loading Risk:** Extensive `.Include()` chains; potential for N+1 queries.
- **Error Handling:** Generic NotFound for missing entities.
- **Type Safety:** Type string is validated via switch; unknown types return 404.

---

### 1.7 DrilldownController

**File:** `Controllers/DrilldownController.cs`  
**Route Prefix:** `api/v1/drilldown`  
**Class Attributes:** `[Route("api/v1/drilldown")]`, `[ApiController]`  
**Auth:** None (relies on tenant context)  

**Dependencies Injected:**
- `AppDbContext _db`
- `ITenantContext _tenantContext`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `party-summary` | `GetPartySummary` | (none) | JSON with vendor name, code, total PO amount, transaction count, last transaction date | Vendor-centric analytics: aggregates PO spend by vendor. Filtered by tenant companyId. |
| GET | `cip-kpis` | `GetCipKpis` | (none) | JSON with total projects, active, budget, spent, completed, cancelled counts | Capital project KPIs, tenant-scoped. |

**Security Notes:**
- Tenant filtering applied (companyId/siteId from context).
- Aggregations only; no detailed transaction exposure.

---

### 1.8 IntegrationWebhookController

**File:** `Controllers/IntegrationWebhookController.cs`  
**Route Prefix:** `api/integrations`  
**Class Attributes:** `[ApiController]`, `[Route("api/integrations")]`  
**Auth:** Custom webhook signature validation via `IInboundWebhookService`  

**Dependencies Injected:**
- `IInboundWebhookService _webhookService`
- `ILogger<IntegrationWebhookController> _logger`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| POST | `inbound/{integrationKey}` | `ReceiveWebhook` | URL: `integrationKey` (string), Headers: `X-CherryAI-Timestamp`, `X-CherryAI-Signature`, `Idempotency-Key`, Body: Raw | JSON with `{ success, message, eventId }` | Receive inbound webhooks from external integrations. |

**Security & Implementation Details:**

- **Header-Based Validation:**
  - Reads `X-CherryAI-Timestamp` for signature window (UNIX timestamp).
  - Reads `X-CherryAI-Signature` for HMAC validation.
  - Reads `Idempotency-Key` for deduplication.
  - Collects all `X-CherryAI-*` headers + `Idempotency-Key` in a dictionary.

- **Webhook Service Processing:**
  - Delegates to `IInboundWebhookService.ReceiveWebhookAsync(integrationKey, rawBody, timestamp, signature, idempotencyKey, headers)`.
  - Returns tuple: `(success: bool, message: string, eventId: string)`.
  - Expected to validate HMAC signature, enforce timestamp freshness, check idempotency.

- **Response:**
  - Success: `{ success: true, message, eventId }`
  - Failure: 400 BadRequest with `{ success: false, message }`

- **Logging:**
  - Warnings logged for rejected webhooks (line 50).

**Risks:**
- **Raw Body Reading:** Reads entire request body as UTF-8; assumes correct encoding.
- **No Public Key Rotation Visible:** Signature validation hidden in service; audit of key management unavailable.
- **No Replay Protection Visible:** Timestamp validation presumed but not shown.

---

### 1.9 ItemsApiController

**File:** `Controllers/ItemsApiController.cs`  
**Route Prefix:** `api/items`  
**Class Attributes:** `[ApiController]`, `[Route("api/items")]`  
**Auth:** None (all endpoints public)  

**Dependencies Injected:**
- `IItemStockingService _stockingService`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `{itemId}/stocking` | `GetStocking` | `itemId` (int), `companyId?` (int?) | JSON with stocking parameters: minQuantity, maxQuantity, reorderPoint, reorderQuantity, safetyStock, leadTimeDays, defaultWarehouse, defaultAisle, defaultRack, defaultShelf, defaultBin, isStocked, isCriticalSpare, preferredVendorId | Retrieve inventory stocking profile for an item. |

**Security Notes:**
- Public endpoint; exposes inventory thresholds and preferred vendor assignments.
- Delegated to stocking service; no auth checks here.

---

### 1.10 OrgController

**File:** `Controllers/OrgController.cs`  
**Route Prefix:** `api/v1/org`  
**Class Attributes:** `[Route("api/v1/org")]`, `[ApiController]`  
**Auth:** None (relies on tenant context for filtering)  

**Dependencies Injected:**
- `AppDbContext _db`
- `ITenantContext _tenantContext`

**Endpoints:**

| HTTP | Route | Method | Parameters | Returns | Purpose |
|------|-------|--------|-----------|---------|---------|
| GET | `sites` | `GetSites` | (none) | JSON with `{ sites: [ { Id, Name, SiteCode, CompanyId } ], currentSiteId, showAllSites: bool }` | List active sites visible to user. Filtered by companyId and assigned sites from tenant context. |
| POST | `site` | `SetSite` | Query: `siteId?` (int?) | JSON with `{ siteId }` | Update tenant context to active site. Returns 403 if siteId not in visible scope. |
| GET | `tree` | `GetTree` | Header: `X-Tenant-Id` (default="default") | JSON with `{ rootId: guid?, totalNodes, nodes: [ { id, parentId, nodeType, name, code, indentLevel, companyId, siteId } ], showAllCompanies: bool }` | Hierarchical org tree (tenant, holdings, companies). Filtered by visible company IDs. Flattened response with indent levels. |

**Security & Implementation Details:**

- **Tenant Context Filtering:**
  - `GetSites()` filters by tenant context's visible company IDs and assigned site IDs.
  - Returns `showAllSites: true` if no assigned site (user can see all).
  - `SetSite()` validates requested site is in `VisibleSiteIds` before updating context (line 58).

- **Org Tree:**
  - Loads all nodes with NodeType = "holding" or "company" for tenant.
  - Builds visibility set based on visible company IDs and hierarchical ancestry.
  - Flattens to array with indent level for UI tree rendering.
  - `showAllCompanies` indicates if user can see all or is restricted.

- **Multi-Tenancy:**
  - Tenant code from `X-Tenant-Id` header (defaults to "default").
  - Holds company and site scoping data in ITenantContext.

---

## 2. API Authentication & Authorization

### 2.1 Authentication Models

**Three distinct authentication mechanisms exist:**

#### A. X-API-Key (Header-Based API Key)
- **Where:** `AssetsApiController` (all endpoints).
- **Validation:** Custom `ValidateApiKey()` method checks `Request.Headers["X-API-Key"]` and calls `ApiService.ValidateKeyAsync()`.
- **Model:** `ApiKey` entity in database.
  - Fields: Id, Name, KeyHash, KeyPrefix, CreatedAt, LastUsedAt, ExpiresAt, IsActive, Scopes, CreatedBy.
  - Hash stored (not plaintext); prefix visible for UI display.
- **Expiration:** Optional `ExpiresAt` datetime.
- **Scopes:** Comma-separated scopes string (optional).
- **Activation:** Boolean `IsActive` flag.

#### B. Tenant Context Headers (Custom Headers)
- **Where:** Validated on all `/api/v1/` endpoints via `ApiHeaderEnforcementMiddleware`.
- **Required Headers:**
  - `X-Tenant-Id` — Tenant identifier
  - `X-User-Id` — User identifier
  - `X-Org-Node-Id` — Org node (hierarchical scope)
- **Missing Header Response:** 400 Bad Request with list of missing headers.
- **Implementation:** Middleware checks and blocks requests before they reach controllers (lines 18–43).

#### C. Session-Based Authentication (Implicit)
- Used by Razor Pages (`Pages/API/Index.cshtml.cs`, `Pages/API/Import.cshtml.cs`).
- Requires `[Authorize]` attribute with role checks.
- Claims extracted from `User.Claims` in AuthController.

### 2.2 Authorization Patterns

| Controller | Endpoints | Auth Method | Details |
|-----------|-----------|-------------|---------|
| **AnalyticsController** | drilldown, kpis | Implicit (tenant context) | No explicit [Authorize]; assumes authenticated context. |
| **AssetsApiController** | All CRUD | X-API-Key | Custom ValidateApiKey() on every action. |
| **AuthController** | whoami | None (AllowAnonymous) | Public diagnostic endpoint. |
| **BackupController** | export, status | Environment gating | No auth; relies on ALLOW_DB_BACKUP env var. |
| **BarcodeApiController** | All | None | Public endpoints. |
| **DetailController** | GetDetail | Implicit (tenant context) | No explicit [Authorize]. |
| **DrilldownController** | All | Implicit (tenant context) | No explicit [Authorize]. |
| **IntegrationWebhookController** | inbound | Webhook signature | Custom signature validation via IInboundWebhookService. |
| **ItemsApiController** | All | None | Public endpoints. |
| **OrgController** | All | Implicit (tenant context) | Uses ITenantContext for filtering, but no explicit role check. |

### 2.3 Role-Based Access Control (from Razor Pages)

- **Index.cshtml.cs (API Key Management):** `[Authorize(Roles = "Admin")]`
  - Only admins can create/revoke API keys.
  - Calls `ApiService.CreateApiKeyAsync()` and `ApiService.RevokeKeyAsync()`.

- **Import.cshtml.cs (CSV Import):** `[Authorize(Roles = "Admin,Accountant")]`
  - Admins and Accountants can upload CSV files.
  - Calls `ImportService.ImportAssetsFromCsvAsync()`.

---

## 3. Webhook Endpoints

### 3.1 IntegrationWebhookController — Inbound Webhook Processing

**Endpoint:** `POST /api/integrations/inbound/{integrationKey}`

**Header-Based Security:**
- `X-CherryAI-Timestamp` — Webhook sent timestamp (UNIX epoch seconds).
- `X-CherryAI-Signature` — HMAC-SHA256 signature over timestamp + raw body.
- `Idempotency-Key` — UUID to prevent duplicate processing.
- Any other `X-CherryAI-*` headers are captured and passed to service.

**Request Flow:**
1. Read raw body as UTF-8 string.
2. Collect all `X-CherryAI-*` and `Idempotency-Key` headers.
3. Call `IInboundWebhookService.ReceiveWebhookAsync(integrationKey, rawBody, timestamp, signature, idempotencyKey, headers)`.
4. Service validates signature, checks timestamp freshness, enforces idempotency.
5. Return success or 400 BadRequest.

**Idempotency:**
- Idempotency-Key (UUID) prevents duplicate event processing.
- Service expected to track processed keys and reject replays.

**HMAC Verification (presumed):**
- Signature format: `HMAC-SHA256(X-CherryAI-Timestamp + "." + RawBody, integration_secret)`
- Integration secret stored per `integrationKey` (not visible in controller).

**Logging:**
- BadRequest webhooks logged as warnings (integrationKey, message).
- No audit trail of successful webhooks visible.

---

## 4. Public vs. Internal Endpoints

### 4.1 Public Endpoints (No Auth Required)

| Controller | Routes | Notes |
|-----------|--------|-------|
| **AuthController** | `GET /auth/whoami` | Diagnostic; no sensitive data isolation. |
| **BackupController** | `GET /api/backup/status` | Always accessible; export requires gating. |
| **BarcodeApiController** | All 5 endpoints | Public barcode generation, scanning, lookup. Exposes inventory locations. |
| **ItemsApiController** | `GET /api/items/{itemId}/stocking` | Public inventory stocking data. |

### 4.2 Authenticated/Gated Endpoints

| Controller | Routes | Auth | Notes |
|-----------|--------|------|-------|
| **AnalyticsController** | All | Implicit (tenant context) | Assumes authenticated context (via middleware). |
| **AssetsApiController** | All | X-API-Key | All CRUD operations require API key. |
| **DetailController** | All | Implicit (tenant context) | Assumes authenticated context. Some detail types not tenant-filtered (vendor, user, gl_account). |
| **DrilldownController** | All | Implicit (tenant context) | Assumes authenticated context. |
| **IntegrationWebhookController** | `POST /api/integrations/inbound/{key}` | Webhook signature + idempotency | Custom signature validation. |
| **OrgController** | All | Implicit (tenant context) | Org filtering based on visible scope, but no explicit role check. |
| **BackupController** | `GET /api/backup/export` | Development env + env var | Gated by environment and feature flag only. |

### 4.3 Admin-Only Endpoints (Razor Pages)

| Page | Endpoint | Method | Auth | Purpose |
|-----|----------|--------|------|---------|
| **API/Index** | `Pages/API/Index` | POST OnPostCreateKeyAsync | `[Authorize(Roles = "Admin")]` | Create API key. |
| **API/Index** | `Pages/API/Index` | POST OnPostRevokeKeyAsync | `[Authorize(Roles = "Admin")]` | Revoke API key. |
| **API/Import** | `Pages/API/Import` | POST OnPostAsync | `[Authorize(Roles = "Admin,Accountant")]` | Import assets from CSV. |

---

## 5. API Header Enforcement Middleware

**File:** `Middleware/ApiHeaderEnforcementMiddleware.cs`

**Purpose:** Enforce required headers on all `/api/v1/` endpoints.

**Implementation:**

```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        var orgNodeId = context.Request.Headers["X-Org-Node-Id"].FirstOrDefault();
        
        if (missing headers...)
            return 400 with { error, missing: [], path }
    }
    await _next(context);
}
```

**Required Headers for `/api/v1/*` endpoints:**
1. `X-Tenant-Id` — Tenant identifier
2. `X-User-Id` — User identifier
3. `X-Org-Node-Id` — Org node identifier

**Behavior:**
- If any header is missing, returns 400 Bad Request.
- Response includes list of missing headers and request path.
- Middleware runs early in pipeline, before controller action execution.
- No other API paths trigger header validation (e.g., `/api/barcode`, `/auth`, `/api/items`).

**Configuration Assumption:**
- Middleware registered via `app.UseApiHeaderEnforcement()` in Program.cs (presumed).

---

## 6. API Key Model & Lifecycle

**File:** `Models/ApiKey.cs`

**Schema:**

```csharp
public class ApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } // Max 100 chars
    public string KeyHash { get; set; } // Max 64 chars (bcrypt or similar)
    public string KeyPrefix { get; set; } // Max 10 chars (e.g., "cherry_abc")
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public string? Scopes { get; set; } // Max 500 chars (comma-separated)
    public string? CreatedBy { get; set; } // Max 100 chars (username)
}
```

**Lifecycle:**

1. **Creation** (Admin via Pages/API/Index):
   - Admin calls `OnPostCreateKeyAsync(keyName)`.
   - `ApiService.CreateApiKeyAsync(keyName, User.Identity?.Name)` returns `(key, rawKey)`.
   - `key` stored in DB with KeyHash.
   - `rawKey` displayed once to user (not shown again).
   - UI displays in Pages/API/Index.cshtml.

2. **Validation** (via ApiService):
   - `ApiService.ValidateKeyAsync(apiKeyHeader)` checks if key matches hash and is active.
   - Checks `ExpiresAt` if present.
   - Returns `ApiKey` object or null if invalid.

3. **Revocation** (Admin):
   - Admin calls `OnPostRevokeKeyAsync(keyId)`.
   - `ApiService.RevokeKeyAsync(keyId)` marks key as inactive.

4. **Usage Tracking**:
   - `LastUsedAt` updated on successful validation (presumed in ApiService).

**Scopes:**
- Stored as CSV string (e.g., "read:assets,write:assets").
- Parsed by ApiService; no scope enforcement visible in controllers (all-or-nothing access).

---

## 7. Detail Contract & Response Format

**File:** `config/detail_contract.json`

**Purpose:** Formal specification of the polymorphic `/api/v1/details/{type}/{id}` endpoint.

**Version:** 1.0.0

**Response Format (Mandatory):**

```json
{
  "type": "string",
  "id": "int or guid",
  "header": "object with >=12 meaningful fields",
  "sections": "array of >=3 sections",
  "related": "object with related entity lists (where applicable)"
}
```

**Each section:**
```json
{
  "name": "identification",
  "fields": { /* key: value pairs */ }
}
```

**Supported Types (21 total):**

| # | Type | Entity | PK | Sections Min | Notes |
|---|------|--------|----|--------------|----|
| 1 | asset | Asset | int | 4 | Identification, financial, classification, location |
| 2 | purchase_order | PurchaseOrder | int | 3 | Order info, vendor, financials |
| 3 | customer_invoice | CustomerInvoice | int | 3 | Invoice info, customer, financials |
| 4 | vendor_invoice | VendorInvoice | int | 3 | Invoice info, vendor, financials |
| 5 | maintenance_event | MaintenanceEvent | int | 3 | Work order info, scheduling, costs, asset |
| 6 | vendor | Vendor | int | 3 | Identification, contact, address |
| 7 | customer | Customer | int | 3 | Identification, contact, address |
| 8 | site | Site | int | 3 | Identification, address, operations |
| 9 | location | Location | int | 3 | Identification, physical, hierarchy |
| 10 | company | Company | int | 3 | Identification, financial, contact |
| 11 | book | Book | int | 3 | Identification, depreciation, gl_accounts |
| 12 | item | Item | int | 3 | Identification, inventory, costing |
| 13 | work_order | MaintenanceEvent (alias) | int | 3 | Same as maintenance_event |
| 14 | cip_project | CipProject | int | 3 | Project info, schedule, budget, organization |
| 15 | fiscal_year | FiscalYear | int | 3 | Calendar, configuration, status |
| 16 | fiscal_period | FiscalPeriod | int | 3 | Period info, status, fiscal_year |
| 17 | lookup_type | LookupType | int | 3 | Identification, statistics, scope |
| 18 | pm_schedule | MaintenanceSchedule | int | 3 | Schedule info, recurrence, execution |
| 19 | pm_template | PMTemplate | int | 3 | Identification, scheduling, resources |
| 20 | org_node | OrgNode | guid | 3 | Identification, hierarchy, status |
| 21 | user | ApplicationUser | int | 3 | Identification, access, preferences |
| 22 | journal_entry | JournalEntry | int | 3 | Identification, financial, configuration |
| 23 | work_request | WorkRequest | int | 3 | Identification, contact, location, ai_analysis |
| 24 | gl_account | GlAccount | int | 3 | Identification, classification, configuration, hierarchy |

---

## 8. Lookup Baselines Configuration

**File:** `config/lookup_baselines.json`

**Purpose:** Define canonical lookup values for dropdown lists and enumerations.

**Structure:**

```json
{
  "baselines": [
    {
      "lookupKey": "AssetType",
      "values": [
        { "code": "UNSPECIFIED", "name": "Unspecified", "sortOrder": 999, "isActive": true }
      ]
    }
  ]
}
```

**Defined Baselines (10 lookup types):**

1. **AssetType** — Asset classification (1 baseline value: Unspecified).

2. **AssetStatus** — Asset lifecycle state (2 values):
   - Active (stops depreciation: false)
   - Disposed (stops depreciation: true, is terminal: true)

3. **WorkOrderStatus** — Maintenance work order state (5 values):
   - Open, In Progress, On Hold, Completed, Cancelled

4. **WorkOrderPriority** — WO severity (5 values):
   - Emergency, Urgent, High, Normal, Low

5. **JournalStatus** — Journal entry state (3 values):
   - Draft, Posted, Reversed

6. **POStatus** — Purchase order state (6 values):
   - Draft, Approved, Sent, Partially Received, Received, Cancelled

7. **InvoiceStatus** — Invoice state (4 values):
   - Draft, Approved, Paid, Voided

8. **DisposalReason** — Asset disposal reason (4 values):
   - Sale, Scrapped, Donated, Stolen

9. **TransferReason** — Asset movement reason (3 values):
   - Relocation, Reorganization, Optimization

10. **CipCostType** — Capital project cost category (12 values):
    - Construction, Engineering, Equipment, Labor, Materials, Freight, Installation, Testing, Permits, Professional, Interest, Other

---

## 9. Razor Pages under /Pages/API/

**Directory:** `Pages/API/`

**Files:**

| File | Type | Auth | Purpose |
|------|------|------|---------|
| **Index.cshtml.cs** | PageModel | `[Authorize(Roles = "Admin")]` | API key management UI; list, create, revoke keys. |
| **Index.cshtml** | Razor Page | (linked) | HTML form for key management. |
| **Import.cshtml.cs** | PageModel | `[Authorize(Roles = "Admin,Accountant")]` | CSV asset import form. |
| **Import.cshtml** | Razor Page | (linked) | HTML form for file upload + import result display. |
| **_ApiIndexKpis.cshtml** | Partial | (referenced by Index) | KPI tiles (dashboard metrics). |
| **_ApiIndexActions.cshtml** | Partial | (referenced by Index) | Action buttons (create/revoke key). |
| **_ApiIndexContext.cshtml** | Partial | (referenced by Index) | Context info (active keys, last used). |
| **_ApiImportKpis.cshtml** | Partial | (referenced by Import) | Import statistics. |
| **_ApiImportActions.cshtml** | Partial | (referenced by Import) | Import controls and file picker. |

**Module Guard:**
- Both pages check `_moduleGuard.IsModuleEnabledAsync("api")` in OnGetAsync.
- If API module disabled, redirect to `/ModuleDisabled` page.

---

## 10. API Surface Gaps

**Enterprise EAM Features NOT exposed via API:**

### 10.1 OpenAPI/Swagger Documentation
- **Status:** Not discovered.
- **Impact:** No auto-generated API documentation; clients must reverse-engineer endpoints.
- **Recommendation:** Implement Swashbuckle (Swagger/OpenAPI 3.0) with attribute-based documentation.

### 10.2 GraphQL API
- **Status:** Not implemented.
- **Impact:** REST clients forced into N+1 query patterns or multiple requests.
- **Recommendation:** Consider HotChocolate or GraphQL-core for complex data shapes (e.g., asset + book + location in one query).

### 10.3 OData (Open Data Protocol)
- **Status:** Not implemented.
- **Impact:** Limited filtering/sorting; pagination via offset/limit only.
- **Recommendation:** OData would enable $filter, $orderby, $expand for better client control.

### 10.4 Webhook Subscription Management API
- **Status:** Only inbound webhooks; no subscription/unsubscription endpoints.
- **Impact:** Webhooks must be configured out-of-band (database direct or admin UI).
- **Recommendation:** Add `POST /api/webhooks/subscriptions` to manage active webhook registrations.

### 10.5 File Upload API
- **Status:** CSV import only (Razor Page, not REST API).
- **Impact:** No image/document upload for assets, barcode images, or attachments.
- **Recommendation:** Add multipart/form-data endpoint for asset images, barcode media, or document attachments.

### 10.6 Bulk Operations API
- **Status:** Partial (batch barcode print exists; no bulk CRUD for assets).
- **Impact:** Bulk updates require individual requests.
- **Recommendation:** Implement `POST /api/v1/assets/bulk` for batch create/update/delete with transaction semantics.

### 10.7 Search/Full-Text API
- **Status:** Not visible.
- **Impact:** No global search endpoint; detail lookup requires exact type/id.
- **Recommendation:** Add `POST /api/v1/search` with full-text query on asset number, description, vendor, etc.

### 10.8 Export API (beyond backup)
- **Status:** Backup export only; no CSV/Excel export for results.
- **Impact:** Users cannot export drilldown results or custom report data.
- **Recommendation:** Add `GET /api/v1/analytics/export?type=csv&...` for result export.

### 10.9 Depreciation Calculation API
- **Status:** Not exposed.
- **Impact:** Depreciation runs only via internal scheduled jobs.
- **Recommendation:** Expose read-only depreciation summary or on-demand calculation endpoint.

### 10.10 Audit Log API
- **Status:** Not discovered.
- **Impact:** No client-facing audit trail; cannot query who changed what, when.
- **Recommendation:** Add `GET /api/v1/audit-logs` with filtering by entity type, user, date range.

---

## 11. Anomalies & Security Risks

### 11.1 CRITICAL: Missing Tenant Filtering on Some Detail Endpoints

**Location:** `DetailController.GetDetail()` — specifically:
- `GetVendorDetail()` (line 204)
- `GetUserDetail()` (line 651)
- `GetGlAccountDetail()` (line 750)

**Issue:** These methods do NOT filter by `_tenantContext.CompanyId`. A user from Company A could potentially access vendors, users, or GL accounts from Company B if they know the ID.

**Evidence:**
```csharp
// Line 206: NO tenant filter
var v = await _db.Vendors.AsNoTracking()
    .FirstOrDefaultAsync(x => x.Id == id);
```

**Mitigation:** Add `&& v.CompanyId == _tenantContext.CompanyId` checks.

### 11.2 HIGH: Public Barcode Endpoints Expose Inventory Locations

**Location:** `BarcodeApiController` — all 5 endpoints.

**Issue:** No authentication; anyone can:
- Scan a barcode image and retrieve item inventory levels.
- Look up any barcode value and see warehouse/aisle/bin locations.
- Batch-print labels for items they should not access.

**Risk:** Inventory reconnaissance; theft planning.

**Mitigation:** Require authentication (X-API-Key or tenant context headers) on barcode endpoints.

### 11.3 HIGH: Database Export Unencrypted & No PII Masking

**Location:** `BackupController.ExportDatabase()`.

**Issues:**
1. Full JSON dump with zero field sanitization (passwords, SSNs, credit cards if present).
2. Downloaded as plaintext over HTTP (assume TLS in production, but still sensitive).
3. Exception swallowing (line 72) hides data access errors.
4. No audit logging of export events.

**Mitigation:**
- Encrypt export with GPG or at-rest encryption.
- Implement field masking for PII (email last 2 chars, SSN first 5 digits, etc.).
- Log all export attempts with user, timestamp, file size.
- Consider disabling export in production entirely.

### 11.4 MEDIUM: API Key Scope Enforcement Missing

**Location:** `ApiKey.cs` defines `Scopes` field (comma-separated), but no controller validates scope usage.

**Issue:** A key created with scope "read:assets" could potentially write or delete assets (no scope enforcement in AssetsApiController).

**Mitigation:** Add scope middleware that checks `ApiKey.Scopes` against action being performed.

### 11.5 MEDIUM: Webhook Signature Validation Hidden

**Location:** `IntegrationWebhookController.ReceiveWebhook()` delegates signature validation to `IInboundWebhookService`.

**Issue:** Cannot audit HMAC logic, key rotation, or timestamp freshness from controller code. Service implementation not provided.

**Risks:**
- Signature algorithm not visible (SHA256? SHA512?).
- Timestamp freshness window unknown (5 minutes? 1 hour?).
- Secret key storage/rotation not visible.

**Mitigation:** Audit `IInboundWebhookService` implementation separately.

### 11.6 MEDIUM: No Rate Limiting on Public Endpoints

**Location:** All barcode endpoints + items stocking endpoint are public.

**Issue:** No rate limiting visible; malicious client could hammer endpoints or exhaust image generation resources.

**Mitigation:** Add rate limiting middleware (e.g., IP-based or key-based throttling) to `/api/barcode/*` and `/api/items/*`.

### 11.7 MEDIUM: Detail Endpoint Type Parameter Not Strictly Validated

**Location:** `DetailController.GetDetail(string type, string id)`.

**Issue:** Type validation is a switch case; unknown types return 404. But no upper/lower-case normalization; typos silently fail.

**Mitigation:** Validate type against a whitelist enum; return 400 BadRequest with hint.

### 11.8 MEDIUM: Concurrency Control on Assets Only

**Location:** `AssetsApiController` implements ETag + If-Match, but DetailController does NOT.

**Issue:** Other entities (PO, invoice, WO) retrieved in DetailController lack concurrency protection. Two clients updating same entity have race condition.

**Mitigation:** Apply ETag pattern across all UpdateAsset-like endpoints (currently only asset updates in API are protected).

### 11.9 LOW: Implicit Tenant Context Assumption

**Location:** AnalyticsController, DrilldownController, DetailController (partial), OrgController.

**Issue:** Controllers assume `_tenantContext` is always populated with valid CompanyId/SiteId. If middleware fails to set context, queries may return unfiltered data.

**Mitigation:** Add explicit null checks and return 400 if tenant context unavailable.

### 11.10 LOW: No Versioning Strategy for API

**Location:** Routes use `/api/v1/` prefix, but no deprecation path or versioning plan visible.

**Issue:** Breaking changes to responses (e.g., adding required field) could break older clients.

**Mitigation:** Define API versioning strategy (API version header or URL-based versioning); support multiple versions for transition period.

### 11.11 TODO: Incomplete Cip Detail Implementation

**Location:** `DetailController.GetCipProjectDetail()` (line 407).

**Note:** Extensive use of `.Include()`, nested queries for costs, work orders, invoices, journals, assets. Performance not verified; potential for N+1 queries if related entities have their own relationships.

**Mitigation:** Use projections or separate queries to avoid deep object graph loading.

---

## 12. Summary Table: All Endpoints

| Controller | HTTP | Route | Auth | Status |
|-----------|------|-------|------|--------|
| **AnalyticsController** | GET | `api/v1/analytics/drilldown` | Tenant context | Active |
| | GET | `api/v1/analytics/kpis` | Tenant context | Active |
| **AssetsApiController** | GET | `api/v1/assets` | X-API-Key | Active |
| | GET | `api/v1/assets/{id}` | X-API-Key | Active |
| | POST | `api/v1/assets` | X-API-Key | Active |
| | PUT | `api/v1/assets/{id}` | X-API-Key | Active (ETag concurrency) |
| | DELETE | `api/v1/assets/{id}` | X-API-Key | Active (soft delete) |
| **AuthController** | GET | `auth/whoami` | None | Active |
| **BackupController** | GET | `api/backup/export` | Environment gating | Active (dev only) |
| | GET | `api/backup/status` | None | Active |
| **BarcodeApiController** | GET | `api/barcode/generate/{itemId}` | None | Active |
| | GET | `api/barcode/label/{itemId}` | None | Active |
| | POST | `api/barcode/scan` | None | Active |
| | GET | `api/barcode/lookup/{barcodeValue}` | None | Active |
| | POST | `api/barcode/batch-print` | None | Active |
| **DetailController** | GET | `api/v1/details/{type}/{id}` | Tenant context (partial) | Active (polymorphic) |
| **DrilldownController** | GET | `api/v1/drilldown/party-summary` | Tenant context | Active |
| | GET | `api/v1/drilldown/cip-kpis` | Tenant context | Active |
| **IntegrationWebhookController** | POST | `api/integrations/inbound/{integrationKey}` | Webhook signature | Active |
| **ItemsApiController** | GET | `api/items/{itemId}/stocking` | None | Active |
| **OrgController** | GET | `api/v1/org/sites` | Tenant context | Active |
| | POST | `api/v1/org/site` | Tenant context | Active |
| | GET | `api/v1/org/tree` | Tenant context | Active |
| **Razor Pages** | POST | `Pages/API/Index` (create key) | Admin role | Active |
| | POST | `Pages/API/Index` (revoke key) | Admin role | Active |
| | POST | `Pages/API/Import` (upload CSV) | Admin, Accountant roles | Active |

---

## 13. Recommendations

### Short-Term (High Priority)

1. **Fix tenant filtering gaps:** Add CompanyId checks to GetVendorDetail, GetUserDetail, GetGlAccountDetail.
2. **Secure barcode endpoints:** Require X-API-Key or tenant context headers.
3. **Mask sensitive data in backup:** Implement PII masking on database export.
4. **Add audit logging:** Log all API key operations, backup exports, webhook events.

### Medium-Term

5. **Implement scope enforcement:** Validate ApiKey.Scopes against action being performed.
6. **Add rate limiting:** IP-based or key-based throttling on public endpoints.
7. **Standardize concurrency control:** Apply ETag pattern to all mutable endpoints.
8. **Implement webhook subscription management API:** REST endpoints to manage integrations.

### Long-Term

9. **Add OpenAPI/Swagger documentation:** Auto-generate and publish API spec.
10. **Versioning strategy:** Define API versioning and deprecation policy.
11. **Consider GraphQL:** Reduce N+1 query issues for complex data shapes.
12. **Bulk operations API:** Support batch create/update/delete with transactions.

---

## Appendix: File Paths

- Controllers: `/Controllers/AnalyticsController.cs`, `/Controllers/AssetsApiController.cs`, etc.
- Middleware: `/Middleware/ApiHeaderEnforcementMiddleware.cs`
- Models: `/Models/ApiKey.cs`
- Razor Pages: `/Pages/API/Index.cshtml.cs`, `/Pages/API/Import.cshtml.cs`
- Config: `/config/detail_contract.json`, `/config/lookup_baselines.json`

---

**End of Report**
