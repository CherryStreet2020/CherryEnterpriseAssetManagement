#!/usr/bin/env python3
import json, re, sys, os, urllib.request

LAYOUT_FILE = "Pages/Shared/_ModernLayout.cshtml"
BASE = os.environ.get("APP_BASE_URL", "http://127.0.0.1:5000")
OUT_JSON = "proof/runtime/logs/gate_nav_layout_consistency.json"
OUT_TXT = "proof/runtime/logs/gate_nav_layout_consistency.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

with open(LAYOUT_FILE, "r") as f:
    content = f.read()

routes = list(dict.fromkeys(re.findall(r'data-nav-route="([^"]+)"', content)))

EXTRA_PAGES = ["/", "/Help", "/Books", "/Reports/ReportHub"]
all_routes = list(dict.fromkeys(routes + EXTRA_PAGES))

results = []
consistent = 0
inconsistent = 0

for route in all_routes:
    url = BASE + route
    try:
        req = urllib.request.Request(url)
        resp = urllib.request.urlopen(req, timeout=10)
        body = resp.read().decode("utf-8", errors="replace")
        has_sidebar = "cherryai-sidebar" in body or 'id="mainSidebar"' in body
        has_topbar = 'id="mainHeader"' in body or "main-header" in body
        has_org = "orgSelectorWrapper" in body
        is_consistent = has_sidebar and has_topbar and has_org
        if is_consistent:
            consistent += 1
        else:
            inconsistent += 1
        results.append({
            "route": route,
            "has_sidebar": has_sidebar,
            "has_topbar": has_topbar,
            "has_org_selector": has_org,
            "consistent": is_consistent
        })
    except urllib.error.HTTPError as e:
        if e.code == 302:
            consistent += 1
            results.append({
                "route": route,
                "redirected": True,
                "consistent": True
            })
        else:
            inconsistent += 1
            results.append({
                "route": route,
                "error": str(e),
                "consistent": False
            })
    except Exception as e:
        inconsistent += 1
        results.append({
            "route": route,
            "error": str(e),
            "consistent": False
        })

total = consistent + inconsistent
pct = round(100 * consistent / total, 1) if total > 0 else 0
verdict = "PASS" if pct == 100.0 else "FAIL"

data = {
    "total_pages": total,
    "consistent": consistent,
    "inconsistent": inconsistent,
    "consistency_pct": pct,
    "results": results,
    "verdict": verdict
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "Layout Consistency Check",
    "========================",
    f"Total pages: {total}",
    f"Consistent:  {consistent} ({pct}%)",
    f"Inconsistent: {inconsistent}",
    "",
]
for r in results:
    mark = "OK" if r.get("consistent") else "FAIL"
    lines.append(f"  [{mark}] {r['route']}")
lines.append(f"")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
