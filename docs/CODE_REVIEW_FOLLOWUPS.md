# Code review follow-ups (post 2026-05-07)

Living tracker for deferred work surfaced during the 2026-05-07 end-of-day
code review and the subsequent fix-up PRs ([#22](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/22)
through [#27](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/27)).
The 7 critical and smell-tier findings from that review are all closed.
What's left here is the next layer down: defensive hardening and missing
test coverage that didn't fit the original scope but should land before
shipping a public 1.0.

When you pick something off this list, link the PR back here and move
the entry under "Closed" so the open list stays a real backlog.

> **See also:** [`audit-2026-05-08-followup/STRUCTURAL_AUDIT.md`](audit-2026-05-08-followup/STRUCTURAL_AUDIT.md) — the 2026-05-08 pre-roadmap structural integrity audit. Sprint 0.5 is the active workstream. Items 1–11 below are tracked separately from the audit's S1/S2 list (some overlap intentionally — e.g., Sprint 0.5 closes the InvoiceMatchingService gap as part of S2-10).

## Open

### 1. Tenant-scope `InvoiceMatchingService` defensively

**Where:** `Services/InvoiceMatchingService.cs`. The service takes only
`AppDbContext` in its constructor and queries `_db.VendorInvoices` by id
without any tenant filter:

```csharp
var invoice = await _db.VendorInvoices
    .Include(i => i.Lines)
        .ThenInclude(l => l.PurchaseOrderLine)
    ...
    .FirstOrDefaultAsync(i => i.Id == invoiceId);
```

**Why it's safe today:** every production caller is a Razor page handler
that already scope-verified the invoice via its own page query
(`Pages/AccountsPayable/Details.cshtml.cs`, etc.) and passes only an
in-scope id down. So the leak is one bad caller away.

**What to do:** inject `ITenantContext` into the service and add the
`VisibleCompanyIds.Contains` predicate to the load. Mirror the shape
used in [#22](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/22)
and [#26](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/26).
Add a regression test analogous to `AccountsPayableTenantScopeTests` —
"loading another tenant's invoice id returns NotMatched, doesn't read
the row."

**Sizing:** ~30 LOC + ~50 LOC test. Single PR.

---

### 2. Same conditional-tenant-scope shape elsewhere — sweep the codebase

**Where:** the bug shape that caused [#22](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/22)
(AP) and [#26](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/26)
(Receiving) is:

```csharp
var query = ...;
if (_tenantContext.CompanyId.HasValue)
    query = query.Where(p => visibleCompanyIds.Contains(...));
```

If a user has `CompanyId == null` but `VisibleCompanyIds` restricts to
some other tenant's company, the company filter is skipped entirely.
We've found and fixed two; the rest of the codebase likely has more.

**What to do:** grep for `_tenantContext.CompanyId.HasValue` and
`tenantContext.CompanyId.HasValue` across `Pages/` and `Services/`.
Each match is a candidate. The CORRECT pattern is unconditional
`VisibleCompanyIds.Contains(...)` (and `SiteId` checks may legitimately
stay conditional — site is a sub-scope of company).

**Sizing:** depends on hits. Likely 2–5 small PRs. Each needs a
regression test in the same shape as `ReceivingTenantScopeTests`.

---

### 3. Missing unit-test coverage

These business-critical paths have no test files yet. Each one should
be a focused PR with the test file + any small refactors needed to make
the code testable in isolation.

| Path | What to cover | Why it matters |
|---|---|---|
| 3-way invoice matching | `Services/InvoiceMatchingService.EvaluateMatchAsync` — quantity + price + tolerance edge cases, partial receipts, over-receipts, unlinked lines | The core AP correctness check; today only tested via the page-handler smoke path |
| PO state transitions | `Pages/Purchasing/Details.cshtml.cs` — `Approve` / `Send` / `Cancel` / `Close` handlers + their FK syncs | Each transition writes both the legacy enum and the FK column; getting either out of sync corrupts dropdowns |
| CIP capitalization | `Services/Cip/*` — settlement to FA, partial settlements, CIP-to-CIP transfers | Material flows; an audit failure here is a re-statement |
| Maintenance cost rollup | `Services/Maintenance/CloseoutService` + cost field updates on `MaintenanceEvent` | Drives the maintenance cost KPIs and the per-asset lifetime cost |

**Sizing:** ~150–250 LOC each. Treat as 4 separate PRs.

---

### 4. Sprint 0 #8 — strongly-typed outbox payloads — ✅ DONE

Closed across [#29](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/29) (design doc), [#30](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/30) (Phase 1 — registry + typed enqueue + payloadVersion column), [#31](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/31) (Phase 2 — migrate all 5 internal call sites), [#32](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/32) (Phase 3 — `/Admin/Webhooks/Catalog`), [#33](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/33) (Phase 5 — drop legacy overloads). See [`docs/design/OUTBOX_TYPED_PAYLOADS.md`](design/OUTBOX_TYPED_PAYLOADS.md) for the full design.

Phase 4 (V1→V2 parallel-emit tooling) stays deferred per the design — only build it when the first event actually needs to evolve. Current state:

- `IOutboxWriter.EnqueueAsync<T>(int companyId, int? siteId, T evt) where T : IDomainEvent` is the only enqueue method
- 5 V1 records live under `Services/Webhooks/Events/`
- `DomainEventRegistry` discovers them at startup; partner-facing catalog at `/Admin/Webhooks/Catalog`
- Wire envelope carries `payloadVersion` alongside `schemaVersion`; existing rows without `PayloadVersion` dispatch as V1
- 25+ tests including snapshot tests that lock the V1 wire shape

---

### 5. Three critical workflows: end-to-end smoke verification

After all of the above lands (or sooner if the user wants), manually
walk these three flows in the browser on Replit and confirm no
regressions:

1. **PO → Receipt → 3-way match → AP**: create a PO, receive against it,
   create a vendor invoice, run the 3-way match, post.
2. **WO create → assign → close**: create a maintenance work order,
   assign a technician, close it with cost lines (ensure the period
   lock from [#23](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/23)
   blocks against a closed period and allows against an open one).
3. **CIP → capitalize → asset → depreciation**: create a CIP project,
   accumulate costs, capitalize to a fixed asset, run a depreciation
   period, post the journal.

The Playwright suite (`tests/*.spec.js`) covers a slice of #1 and #2;
it's flaky on Replit Free tier per [#9](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/9)
but worth a run in addition to the manual walk.

**Sizing:** ~1 hour of focused testing. Tag a release once stable.

---

### 6. Outbox enqueue runs in a separate save from business state — not atomic

**Where:** `Services/Webhooks/OutboxWriter.EnqueueAsync<T>` at line 160 calls
its own `_db.SaveChangesAsync()`. Every producer (Closeout, AP posting, asset
lifecycle, PO/Receiving, CIP capitalization, depreciation, PM, WO issuance)
calls `await _db.SaveChangesAsync()` first to commit business state, then
calls `_outbox.EnqueueAsync(...)` which saves the outbox row in a second
transaction.

**Why it matters:** If a process crash or DB error happens between the two
saves, business state is committed but the outbox row isn't — partner
integrations miss the event. CLAUDE.md's "Outbox pattern for integrations"
principle states "Business event + outbox row write happen in the same
SaveChangesAsync transaction." Today's implementation violates that contract.

**What to do:** Refactor `IOutboxWriter` to add a `QueueForEnqueueAsync<T>`
that adds the OutboxEvent to the context **without** saving, letting the
caller's existing SaveChanges commit both rows atomically. Migrate every
producer (~10 call sites) to the new shape and remove the saving variant.

**Sizing:** ~150 LOC across producer + tests. One PR is fine — the change
is mechanical and CI catches mistakes.

---

### 7. Disable Replit Agent auto-commits at the source

**Where:** Replit Agent's checkpoint auto-commit feature. [#8](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/8)
broke the screenshot trigger that was firing it most often, and
auto-commits have been quiet since. But the feature is still enabled
upstream and will fire again whenever Replit Agent decides to
checkpoint.

**What to do:** investigate Replit account settings or `.replit`
configuration to disable the auto-checkpoint behavior, OR have Replit
Agent's commits go to a quarantine branch we can ignore. Likely needs
account-level config rather than repo config.

**Sizing:** investigation-led; could be 5 min or 1 hour.

---

## Closed

| # | Title | PR |
|---|---|---|
| AP cross-tenant invoice leak | `Pages/AccountsPayable/Details.cshtml.cs` conditional `if (CompanyId.HasValue)` skipped tenant scoping | [#22](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/22) |
| Period lock missing on Receiving | `OnPostReceiveAsync` posted GoodsReceipts without consulting `IPeriodGuard` | [#23](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/23) |
| Period lock missing on Maintenance close | `OnPostCompleteAsync` flipped status + wrote costs without a period check | [#23](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/23) |
| `MaintenanceService` no-arg ctor | Left `_tenantContext` null; every scoped query NRE'd | [#24](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/24) |
| `WorkOrderParts` query trusted parent scope | Implicitly safe via early `NotFound`, but a refactor could open a parts leak | [#24](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/24) |
| Receiving exception swallowing | `Receive.cshtml.cs` had bare `catch (Exception)` with no logging | [#25](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/25) |
| Receiving cross-tenant PO leak | Same conditional shape as AP, in both `OnGetAsync` and `OnPostReceiveAsync` | [#26](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/26) |
| Improve.cshtml.cs depreciation snapshot | `UsefulLifeMonths`/`AcquisitionCost` mutated in place without restamping `AssetBookSettings` | [#27](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/27) |
