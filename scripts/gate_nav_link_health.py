#!/usr/bin/env python3
import json, re, sys, os, urllib.request

LAYOUT_FILE = "Pages/Shared/_ModernLayout.cshtml"
BASE = os.environ.get("APP_BASE_URL", "http://127.0.0.1:5000")
OUT_JSON = "proof/runtime/logs/gate_nav_link_health.json"
OUT_TXT = "proof/runtime/logs/gate_nav_link_health.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

with open(LAYOUT_FILE, "r") as f:
    content = f.read()

routes = list(dict.fromkeys(re.findall(r'data-nav-route="([^"]+)"', content)))

results = []
failures = 0

for route in routes:
    url = BASE + route
    try:
        req = urllib.request.Request(url)
        resp = urllib.request.urlopen(req, timeout=10)
        code = resp.getcode()
        body = resp.read().decode("utf-8", errors="replace")
        has_sidebar = "sidebar" in body.lower() or "cherryai-sidebar" in body
        has_topbar = "main-header" in body
        has_org = "orgSelector" in body
        status = "OK"
    except urllib.error.HTTPError as e:
        code = e.code
        has_sidebar = False
        has_topbar = False
        has_org = False
        if code == 302:
            status = "REDIRECT"
        else:
            status = "FAIL"
            failures += 1
    except Exception as e:
        code = 0
        has_sidebar = False
        has_topbar = False
        has_org = False
        status = f"ERROR: {e}"
        failures += 1

    results.append({
        "route": route,
        "status_code": code,
        "status": status,
        "has_sidebar": has_sidebar,
        "has_topbar": has_topbar,
        "has_org_selector": has_org,
    })

verdict = "PASS" if failures == 0 else "FAIL"

data = {
    "total_routes": len(routes),
    "ok_count": sum(1 for r in results if r["status"] == "OK"),
    "redirect_count": sum(1 for r in results if r["status"] == "REDIRECT"),
    "fail_count": failures,
    "results": results,
    "verdict": verdict
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "Sidebar Link Health Check",
    "=========================",
    f"Total routes: {data['total_routes']}",
    f"OK (200):     {data['ok_count']}",
    f"Redirect:     {data['redirect_count']}",
    f"Failed:       {data['fail_count']}",
    "",
]
for r in results:
    mark = "OK" if r["status"] in ("OK", "REDIRECT") else "FAIL"
    lines.append(f"  [{mark}] {r['route']} -> {r['status_code']} {r['status']}")
lines.append(f"")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
