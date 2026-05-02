# Gate 01 — API Headers & Base URL

## Command
```bash
LD_LIBRARY_PATH=... npx playwright test tests/01_api_headers.spec.js --reporter=html
```

## Environment
- Server running at http://127.0.0.1:5000
- Playwright network request interception active

## Pass Criteria
- Every /api/v1 request starts with http://127.0.0.1:5000/api/v1/
- Every /api/v1 request includes:
  - X-Tenant-Id: default
  - X-User-Id: system@localhost
  - X-Org-Node-Id: (valid UUID)
- At least 10 API requests captured
- Log file generated with captured requests

## Evidence Artifacts (in proof bundle)
- `PROXY_LOGS/api_requests_headers_verified.log` — 10+ lines of verified API requests
- `playwright-report/index.html` — HTML test report
- `test-results/` — Playwright traces
