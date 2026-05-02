#!/usr/bin/env python3
import json, re, sys, os, urllib.request

BASE = os.environ.get("APP_BASE_URL", "http://127.0.0.1:5000")
OUT_JSON = "proof/runtime/logs/gate_modern_shell_features.json"
OUT_TXT = "proof/runtime/logs/gate_modern_shell_features.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

url = BASE + "/"
req = urllib.request.Request(url)
resp = urllib.request.urlopen(req, timeout=10)
body = resp.read().decode("utf-8", errors="replace")

with open("wwwroot/js/command-palette.js", "r") as f:
    palette_js = f.read()

with open("wwwroot/js/sidebar-nav.js", "r") as f:
    sidebar_js = f.read()

with open("wwwroot/js/recent-nav.js", "r") as f:
    recent_js = f.read()

checks = []

has_collapse_btn = "sidebarCollapseBtn" in body
checks.append(("Sidebar collapse toggle button", has_collapse_btn))

has_collapse_logic = "collapsed" in sidebar_js and "sidebarCollapsed" in sidebar_js
checks.append(("Sidebar collapse JS logic", has_collapse_logic))

has_search_input = "globalSearchInput" in body
checks.append(("Global search input present", has_search_input))

has_slash_shortcut = 'key === "/"' in sidebar_js or "key === '/'" in sidebar_js
checks.append(('/ shortcut focuses search', has_slash_shortcut))

has_palette_modal = "commandPaletteOverlay" in body
checks.append(("Command palette modal in DOM", has_palette_modal))

has_ctrl_k = "Ctrl+K" in body or "ctrlKey" in palette_js
checks.append(("Ctrl+K opens command palette", has_ctrl_k))

has_routes_list = "ROUTES" in palette_js and "path" in palette_js
checks.append(("Command palette has routes list", has_routes_list))

has_keyboard_nav = "ArrowDown" in palette_js and "ArrowUp" in palette_js
checks.append(("Keyboard navigation in palette", has_keyboard_nav))

has_recent_storage = "cherryai_recent" in recent_js and "localStorage" in recent_js
checks.append(("Recent nav localStorage storage", has_recent_storage))

has_recent_section = "recentNavSection" in body
checks.append(("Recent nav section in DOM", has_recent_section))

has_feature_flag = "FEATURE_RECENT_NAV" in recent_js
checks.append(("Recent nav feature flag check", has_feature_flag))

has_responsive = "mobile-menu-btn" in body or "mobileMenuBtn" in body
checks.append(("Mobile menu button present", has_responsive))

has_overlay = "sidebarOverlay" in body
checks.append(("Mobile sidebar overlay", has_overlay))

palette_css_loaded = "command-palette.css" in body
checks.append(("Command palette CSS loaded", palette_css_loaded))

all_pass = all(c[1] for c in checks)
verdict = "PASS" if all_pass else "FAIL"

data = {
    "checks": [{"name": c[0], "pass": c[1]} for c in checks],
    "pass_count": sum(1 for c in checks if c[1]),
    "fail_count": sum(1 for c in checks if not c[1]),
    "verdict": verdict
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "Modern Shell Features Check",
    "===========================",
]
for c in checks:
    mark = "PASS" if c[1] else "FAIL"
    lines.append(f"  [{mark}] {c[0]}")
lines.append(f"")
lines.append(f"Pass: {data['pass_count']} / {len(checks)}")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
