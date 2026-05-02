#!/usr/bin/env python3
import json, sys, os

OUT_JSON = "proof/runtime/logs/gate_nav_return_path_stateful.json"
OUT_TXT = "proof/runtime/logs/gate_nav_return_path_stateful.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

with open("wwwroot/js/return-path.js", "r") as f:
    rp_js = f.read()

with open("Services/Navigation/ReturnUrlHelper.cs", "r") as f:
    ruh_cs = f.read()

REQUIRED_MODULES = ["work", "finance", "materials", "assets"]

checks = []

has_session_storage = "sessionStorage" in rp_js
checks.append(("sessionStorage used in return-path.js", has_session_storage))

has_module_map = "MODULE_MAP" in rp_js
checks.append(("MODULE_MAP defined", has_module_map))

has_list_pages = "LIST_PAGES" in rp_js
checks.append(("LIST_PAGES array defined", has_list_pages))

modules_in_js = []
for m in REQUIRED_MODULES:
    if f"'{m}'" in rp_js:
        modules_in_js.append(m)
has_all_modules = len(modules_in_js) == len(REQUIRED_MODULES)
checks.append((f"All required modules present ({', '.join(REQUIRED_MODULES)})", has_all_modules))

has_resolve = "resolveBackUrl" in rp_js
checks.append(("resolveBackUrl function exists", has_resolve))

has_return_url_param = "returnUrl" in rp_js
checks.append(("returnUrl query param support", has_return_url_param))

has_org_scope = "getOrgScope" in rp_js
checks.append(("Org scope in storage key", has_org_scope))

has_canonical_fallbacks = "CanonicalFallback" in ruh_cs or "canonicalFallback" in ruh_cs.lower() or "Fallback" in ruh_cs
checks.append(("Server-side ReturnUrlHelper has fallbacks", has_canonical_fallbacks))

all_pass = all(c[1] for c in checks)
verdict = "PASS" if all_pass else "FAIL"

data = {
    "checks": [{"name": c[0], "pass": c[1]} for c in checks],
    "required_modules": REQUIRED_MODULES,
    "found_modules": modules_in_js,
    "verdict": verdict
}

with open(OUT_JSON, "w") as f:
    json.dump(data, f, indent=2)

lines = [
    "Return Path Stateful Check",
    "==========================",
]
for c in checks:
    mark = "PASS" if c[1] else "FAIL"
    lines.append(f"  [{mark}] {c[0]}")
lines.append(f"")
lines.append(f"OVERALL: {verdict}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if verdict == "PASS" else 1)
