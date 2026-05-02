#!/usr/bin/env python3
import json, re, sys, os

LAYOUT_FILE = "Pages/Shared/_ModernLayout.cshtml"
JS_FILE = "wwwroot/js/sidebar-nav.js"
OUT_JSON = "proof/runtime/logs/gate_nav_active_state_correct.json"
OUT_TXT = "proof/runtime/logs/gate_nav_active_state_correct.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

with open(LAYOUT_FILE, "r") as f:
    layout = f.read()

with open(JS_FILE, "r") as f:
    js = f.read()

routes_with_nav = re.findall(r'data-nav-route="([^"]+)"', layout)

has_data_nav_routes = len(routes_with_nav) > 0

server_active_patterns = re.findall(r'@\(Is(?:Page|ExactPage|Any)\([^)]+\)\s*\?\s*"active"\s*:\s*""\)', layout)
has_server_active = len(server_active_patterns) > 0

has_client_highlight = "highlightActiveItem" in js

has_path_matching = "currentPath" in js and "startsWith" in js

checks = [
    ("data-nav-route attributes present", has_data_nav_routes, f"{len(routes_with_nav)} routes"),
    ("Server-side active state (@IsPage)", has_server_active, f"{len(server_active_patterns)} patterns"),
    ("Client-side highlightActiveItem()", has_client_highlight, "found in sidebar-nav.js"),
    ("Path matching logic", has_path_matching, "currentPath + startsWith"),
]

all_pass = all(c[1] for c in checks)
verdict = "PASS" if all_pass else "FAIL"

data = {
    "checks": [{"name": c[0], "pass": c[1], "detail": c[2]} for c in checks],
    "route_count": len(routes_with_nav),
    "verdict": verdict
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "Active State Correctness Check",
    "==============================",
]
for c in checks:
    mark = "PASS" if c[1] else "FAIL"
    lines.append(f"  [{mark}] {c[0]}: {c[2]}")
lines.append(f"")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
