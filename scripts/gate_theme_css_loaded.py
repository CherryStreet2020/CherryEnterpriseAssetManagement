#!/usr/bin/env python3
import json, os, urllib.request

BASE = os.environ.get("BASE_URL", "http://localhost:5000")
OUT_JSON = "proof/runtime/logs/gate_theme_css_loaded.json"
OUT_TXT = "proof/runtime/logs/gate_theme_css_loaded.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

checks = []
overall = "PASS"

url = BASE + "/"
try:
    req = urllib.request.Request(url)
    with urllib.request.urlopen(req, timeout=10) as resp:
        body = resp.read().decode("utf-8", errors="replace")
except Exception as e:
    body = ""
    checks.append({"check": "fetch_homepage", "pass": False, "error": str(e)})
    overall = "FAIL"

required_css = [
    ("tokens.css", "tokens.css"),
    ("cherryai-theme.css", "cherryai-theme.css"),
    ("cherryai-dark-compliance.css", "cherryai-dark-compliance.css"),
    ("sidebar-nav.css", "sidebar-nav.css"),
    ("premium-components.css", "premium-components.css"),
    ("modern.css", "modern.css"),
]

for label, filename in required_css:
    found = filename in body
    checks.append({"check": f"CSS loaded: {label}", "pass": found})
    if not found:
        overall = "FAIL"

fouc_script = "cherryai_theme" in body and "classList.add" in body
checks.append({"check": "FOUC prevention script in <head>", "pass": fouc_script})
if not fouc_script:
    overall = "FAIL"

theme_toggle = "themeToggleBtn" in body
checks.append({"check": "Theme toggle button in DOM", "pass": theme_toggle})
if not theme_toggle:
    overall = "FAIL"

result = {"gate": "gate_theme_css_loaded", "overall": overall, "checks": checks}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    "Theme CSS Loaded Check",
    "======================",
]
for c in checks:
    status = "PASS" if c["pass"] else "FAIL"
    lines.append(f"  [{status}] {c['check']}")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
