# Gate 04 — Canonical Routes & Redirects

## Command
```bash
LD_LIBRARY_PATH=... npx playwright test tests/04_redirects.spec.js --reporter=list
```

## Environment
- Server running at http://127.0.0.1:5000

## Pass Criteria
- Canonical routes return HTTP 200
- Legacy /Admin routes return HTTP 301 (permanent redirect) to canonical routes
- Redirect targets verified via APIRequestContext with maxRedirects:0
- All 5 redirect mappings verified:
  - /Admin/Locations -> /Assets/Locations
  - /Admin/Vendors -> /Materials/Vendors
  - /Admin/PMTemplates -> /Maintenance/PMTemplates
  - /Admin/PMScheduleEdit -> /Maintenance/PMScheduleEdit
  - /Admin/GlAccounts -> /Books/GlAccounts/1
- Route map documented in ROUTES/navigation_route_map.md

## Evidence Artifacts (in proof bundle)
- `ROUTES/navigation_route_map.md` — Full route map with redirect table
- `screenshots/04_canonical_200.png`
- `test-results/` — Playwright traces for each redirect assertion
