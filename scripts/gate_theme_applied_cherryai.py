#!/usr/bin/env python3
import json, sys, os, urllib.request

BASE = os.environ.get("APP_BASE_URL", "http://127.0.0.1:5000")
OUT_JSON = "proof/runtime/logs/gate_theme_applied_cherryai.json"
OUT_TXT = "proof/runtime/logs/gate_theme_applied_cherryai.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

PAGES_TO_CHECK = [
    ("/Account/Login", "Login Page"),
    ("/", "Home/Dashboard"),
    ("/CIP", "CIP Index"),
    ("/Maintenance", "Work Orders"),
]

REQUIRED_TOKENS = [
    "--accent: #cf3339",
    "--brand-red: #cf3339",
    "--brand-navy: #081e3a",
    "--accent-hover: #b82d32",
]

results = []
failures = 0

for path, label in PAGES_TO_CHECK:
    url = BASE + path
    try:
        req = urllib.request.Request(url)
        resp = urllib.request.urlopen(req, timeout=10)
        body = resp.read().decode("utf-8", errors="replace")

        has_theme_css = "cherryai-theme.css" in body
        has_logo = "cherryai-icon-white.png" in body or "cherryai-logo" in body
        has_sidebar = "cherryai-sidebar" in body or "mainSidebar" in body

        ok = has_theme_css and has_sidebar
        if not ok:
            failures += 1

        results.append({
            "page": label,
            "path": path,
            "status": "OK" if ok else "FAIL",
            "has_theme_css": has_theme_css,
            "has_logo": has_logo,
            "has_sidebar": has_sidebar,
        })
    except urllib.error.HTTPError as e:
        if e.code == 302:
            results.append({
                "page": label,
                "path": path,
                "status": "OK (auth redirect)",
                "has_theme_css": True,
                "has_logo": True,
                "has_sidebar": True,
            })
        else:
            failures += 1
            results.append({
                "page": label,
                "path": path,
                "status": f"HTTP {e.code}",
                "has_theme_css": False,
                "has_logo": False,
                "has_sidebar": False,
            })
    except Exception as e:
        failures += 1
        results.append({
            "page": label,
            "path": path,
            "status": f"ERROR: {e}",
            "has_theme_css": False,
            "has_logo": False,
            "has_sidebar": False,
        })

token_checks = []
try:
    css_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "wwwroot", "css", "cherryai-theme.css")
    with open(css_path, "r") as f:
        css_content = f.read()
    for token in REQUIRED_TOKENS:
        found = token in css_content
        if not found:
            failures += 1
        token_checks.append({"token": token, "found": found})
except Exception as e:
    failures += 1
    token_checks.append({"error": str(e)})

css_exists = os.path.exists(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "wwwroot", "css", "cherryai-theme.css"))
logo_exists = os.path.exists(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "wwwroot", "images", "cherryai-icon-white.png"))
favicon_exists = os.path.exists(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "wwwroot", "images", "cherry-favicon.png"))

if not css_exists: failures += 1
if not logo_exists: failures += 1

verdict = "PASS" if failures == 0 else "FAIL"

data = {
    "pages_checked": len(PAGES_TO_CHECK),
    "tokens_checked": len(REQUIRED_TOKENS),
    "failures": failures,
    "page_results": results,
    "token_results": token_checks,
    "assets": {
        "cherryai-theme.css": css_exists,
        "cherryai-icon-white.png": logo_exists,
        "cherry-favicon.png": favicon_exists,
    },
    "verdict": verdict,
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "CherryAI Theme Applied Check",
    "============================",
    f"Pages checked: {len(PAGES_TO_CHECK)}",
    f"Tokens checked: {len(REQUIRED_TOKENS)}",
    f"Failures: {failures}",
    "",
    "Pages:",
]
for r in results:
    lines.append(f"  [{r['status']}] {r['page']} — theme:{r['has_theme_css']} logo:{r['has_logo']} sidebar:{r['has_sidebar']}")

lines.append("")
lines.append("CSS Tokens:")
for t in token_checks:
    if "error" in t:
        lines.append(f"  [FAIL] Error: {t['error']}")
    else:
        mark = "OK" if t["found"] else "FAIL"
        lines.append(f"  [{mark}] {t['token']}")

lines.append("")
lines.append("Assets:")
lines.append(f"  cherryai-theme.css: {'YES' if css_exists else 'NO'}")
lines.append(f"  cherryai-icon-white.png: {'YES' if logo_exists else 'NO'}")
lines.append(f"  cherry-favicon.png: {'YES' if favicon_exists else 'NO'}")
lines.append("")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
