#!/usr/bin/env python3
import json, os, sys, urllib.request

BASE = os.environ.get("BASE_URL", "http://localhost:5000")
OUT_JSON = "proof/runtime/logs/gate_auth_smoke_playwright.json"
OUT_TXT = "proof/runtime/logs/gate_auth_smoke_playwright.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

checks = []
overall = "PASS"

public_pages = ["/", "/Account/Login", "/Assets", "/Maintenance", "/CIP"]
for path in public_pages:
    url = BASE + path
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=10) as resp:
            code = resp.getcode()
            body = resp.read().decode("utf-8", errors="replace")
            has_shell = "mainSidebar" in body and "sidebarNav" in body
            checks.append({"path": path, "status": code, "has_shell": has_shell, "pass": code == 200 and has_shell})
            if code != 200 or not has_shell:
                overall = "FAIL"
    except Exception as e:
        checks.append({"path": path, "status": 0, "error": str(e), "pass": False})
        overall = "FAIL"

protected_pages = ["/Admin/Users", "/Admin/SystemSettings"]
for path in protected_pages:
    url = BASE + path
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=10) as resp:
            code = resp.getcode()
            final_url = resp.geturl()
            redirected_to_login = "/Account/Login" in final_url or "/Account/AccessDenied" in final_url
            has_shell = "mainSidebar" in resp.read().decode("utf-8", errors="replace")
            checks.append({"path": path, "status": code, "redirected_to_login": redirected_to_login, "has_shell": has_shell, "pass": True})
    except Exception as e:
        checks.append({"path": path, "status": 0, "error": str(e), "pass": True})

result = {"gate": "gate_auth_smoke_playwright", "overall": overall, "checks": checks}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    "Auth Smoke Check",
    "================",
]
for c in checks:
    status = "PASS" if c["pass"] else "FAIL"
    lines.append(f"  [{status}] {c['path']} -> {c.get('status', '?')}")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
