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

### 4. Sprint 0 #8 — strongly-typed outbox payloads

**Where:** `Services/Webhooks/OutboxWriter.cs` (the writer) and the
integration partner contract on the dispatcher side. Today payloads
are stringly-typed JSON blobs.

**Why it matters:** versioned `IDomainEvent` types give the dispatcher
schema-aware retries, pattern-matched handlers, and a compile-time
contract for partner integrations. Without them, payload shape changes
silently break consumers.

**What to do:** introduce a versioned `IDomainEvent` base interface
plus concrete event records (`InvoiceApprovedV1`, `WorkOrderClosedV1`,
…). Migrate `OutboxWriter` to accept those instead of `object`.
Dispatcher does dispatch by type tag.

**Sizing:** medium — ~400 LOC plus migration of every existing call
site. Worth a design doc first; production-readiness is the use case,
not greenfield work, so backward-compat with already-queued rows
matters.

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

### 6. Disable Replit Agent auto-commits at the source

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
