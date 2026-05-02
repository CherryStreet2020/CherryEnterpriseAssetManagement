#!/usr/bin/env python3
import json, os, re, urllib.request

BASE = os.environ.get("BASE_URL", "http://localhost:5000")
OUT_JSON = "proof/runtime/logs/gate_dark_mode_tabs_active_state.json"
OUT_TXT = "proof/runtime/logs/gate_dark_mode_tabs_active_state.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

checks = []
overall = "PASS"

tab_pages = [
    ("/Assets/Asset/100", "asset-tabs"),
    ("/Assets/Asset/100?tab=location", "asset-tabs"),
]

for path, tab_id in tab_pages:
    url = BASE + path
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=10) as resp:
            code = resp.getcode()
            body = resp.read().decode("utf-8", errors="replace")
            has_tab_nav = 'class="tab-nav' in body or "tab-nav__item" in body
            has_active_tab = "tab-nav__item--active" in body
            has_active_panel = 'class="tab-panel active' in body or 'class="tab-panel  active' in body
            passed = has_tab_nav and has_active_tab
            checks.append({
                "path": path,
                "tab_id": tab_id,
                "status": code,
                "has_tab_nav": has_tab_nav,
                "has_active_tab": has_active_tab,
                "has_active_panel": has_active_panel,
                "pass": passed,
            })
            if not passed:
                overall = "FAIL"
    except Exception as e:
        checks.append({"path": path, "error": str(e), "pass": False})
        overall = "FAIL"

dark_css_has_tab_overrides = False
try:
    with open("wwwroot/css/cherryai-dark-compliance.css", "r") as f:
        content = f.read()
    dark_css_has_tab_overrides = "html.dark .tab-nav" in content or "html.dark .premium-tabs" in content
except:
    pass
checks.append({"check": "dark_compliance_has_tab_overrides", "pass": dark_css_has_tab_overrides})
if not dark_css_has_tab_overrides:
    overall = "FAIL"

result = {"gate": "gate_dark_mode_tabs_active_state", "overall": overall, "checks": checks}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    "Dark Mode: Tab Active State Check",
    "==================================",
]
for c in checks:
    status = "PASS" if c["pass"] else "FAIL"
    label = c.get("path", c.get("check", "?"))
    lines.append(f"  [{status}] {label}")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
