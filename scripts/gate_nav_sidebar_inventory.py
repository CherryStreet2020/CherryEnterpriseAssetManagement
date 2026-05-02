#!/usr/bin/env python3
import json, re, sys, os

LAYOUT_FILE = "Pages/Shared/_ModernLayout.cshtml"
OUT_JSON = "proof/runtime/logs/gate_nav_sidebar_inventory.json"
OUT_TXT = "proof/runtime/logs/gate_nav_sidebar_inventory.txt"

os.makedirs("proof/runtime/logs", exist_ok=True)

with open(LAYOUT_FILE, "r") as f:
    content = f.read()

links = re.findall(r'data-nav-route="([^"]+)"', content)

unique_routes = list(dict.fromkeys(links))
duplicates = [r for r in unique_routes if links.count(r) > 1]

storeroom_matches = re.findall(r'(?i)storeroom', content)

labels = re.findall(r'<span[^>]*>([^<]+)</span>', content)
storeroom_labels = [l for l in labels if 'storeroom' in l.lower()]

has_warehouses = any('Warehouses' in l for l in labels)

result = {
    "total_sidebar_links": len(links),
    "unique_routes": len(unique_routes),
    "duplicate_count": len(duplicates),
    "duplicates": duplicates,
    "storeroom_occurrences": len(storeroom_matches),
    "storeroom_labels": storeroom_labels,
    "has_warehouses_label": has_warehouses,
    "routes": unique_routes,
    "verdict": "PASS" if len(duplicates) == 0 and len(storeroom_matches) == 0 and has_warehouses else "FAIL"
}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    f"Sidebar Link Inventory",
    f"======================",
    f"Total links:       {result['total_sidebar_links']}",
    f"Unique routes:     {result['unique_routes']}",
    f"Duplicates:        {result['duplicate_count']}",
    f"Storeroom in UI:   {result['storeroom_occurrences']}",
    f"Warehouses label:  {result['has_warehouses_label']}",
    f"",
    f"Routes:",
]
for r in unique_routes:
    lines.append(f"  {r}")
lines.append(f"")
if duplicates:
    lines.append(f"DUPLICATES FOUND: {duplicates}")
if storeroom_labels:
    lines.append(f"STOREROOM LABELS FOUND: {storeroom_labels}")
lines.append(f"")
lines.append(f"OVERALL: {result['verdict']}")

with open(OUT_TXT, "w") as f:
    f.write("\n".join(lines))

print("\n".join(lines))
sys.exit(0 if result["verdict"] == "PASS" else 1)
