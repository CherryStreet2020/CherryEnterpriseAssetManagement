# Gate 00 — Auth Smoke

## Command
```bash
LD_LIBRARY_PATH=... npx playwright test tests/00_auth_smoke.spec.js --reporter=html
```

## Environment
- Server running at http://127.0.0.1:5000
- Default credentials: admin / admin123
- Playwright configured with headless Chromium

## Pass Criteria
All 5 checks must pass:
1. Unauth user hits protected route (/Admin/Users) -> redirected to /Account/Login
2. Login with admin/admin123 succeeds
3. Post-login redirect returns to the protected route
4. Logout works (redirects to login page)
5. After logout, protected route redirects to sign-in again

## Evidence Artifacts (in proof bundle)
- `playwright-report/index.html` — HTML test report
- `test-results/` — Playwright traces per test
- `screenshots/00_unauth_redirect.png`
- `screenshots/00_login_success.png`
- `screenshots/00_post_login_redirect.png`
- `screenshots/00_logout.png`
- `screenshots/00_post_logout_redirect.png`
