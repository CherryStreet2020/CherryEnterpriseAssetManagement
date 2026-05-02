# Gate 02 — Nav No IA Drift

## Command
```bash
LD_LIBRARY_PATH=... npx playwright test tests/02_nav_no_ia_drift.spec.js --reporter=html
```

## Environment
- Server running at http://127.0.0.1:5000

## Pass Criteria
- Zero sidebar nav items in MAIN groups (assets, finance, materials, projects, work) point to /Admin/* routes
- Zero command palette entries for operational groups (Assets, Work, Materials, Finance, Projects) point to /Admin/* routes
- Admin group items (Users, Sites, Lookups, etc.) are allowed to use /Admin/* routes

## Evidence Artifacts (in proof bundle)
- `playwright-report/index.html` — HTML test report
- `test-results/` — Playwright traces
- `screenshots/02_sidebar_no_admin_drift.png`
- `screenshots/02_command_palette_check.png`
