# Gate 03 — Breadcrumbs & Back to Results

## Command
```bash
LD_LIBRARY_PATH=... npx playwright test tests/03_breadcrumbs_back_to_results.spec.js --reporter=list
```

## Environment
- Server running at http://127.0.0.1:5000

## Pass Criteria
- Maintenance detail page has breadcrumbs (2+ crumbs) and "Back to results" link
- CIP detail page has breadcrumbs and "Back to results" link
- CIP CostDetails drilldown has breadcrumbs and "Back to results" link
- CIP CostTypeDetails drilldown has breadcrumbs and "Back to results" link
- Journals detail page has breadcrumbs and "Back to results" link
- Purchasing detail page has breadcrumbs and "Back to results" link
- AccountsPayable detail page has breadcrumbs and "Back to results" link
- BulkOperations detail page has breadcrumbs and "Back to results" link
- Books detail page has breadcrumbs and "Back to results" link
- Canonical routes serve content (HTTP 200)
- Legacy /Admin routes redirect to canonical via browser follow
- Back link preserves returnUrl from query string (list-state restore)

## Evidence Artifacts (in proof bundle)
- `screenshots/03_maintenance_detail.png`
- `screenshots/03_cip_detail.png`
- `screenshots/03_cip_cost_details.png`
- `screenshots/03_cip_cost_type_details.png`
- `screenshots/03_journals_detail.png`
- `screenshots/03_purchasing_detail.png`
- `screenshots/03_ap_detail.png`
- `screenshots/03_bulkops_detail.png`
- `screenshots/03_books_detail.png`
- `screenshots/03_canonical_routes.png`
- `screenshots/03_legacy_redirects.png`
- `screenshots/03_returnurl_restore.png`
