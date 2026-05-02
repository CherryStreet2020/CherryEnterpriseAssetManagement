#!/usr/bin/env python3
"""Gate: Drilldown Semantic Completeness - validates invoice and PO drilldown data quality."""
import json, sys, urllib.request

BASE = "http://127.0.0.1:5000"
HEADERS = {
    "X-Tenant-Id": "default",
    "X-User-Id": "system@localhost",
    "X-Org-Node-Id": "a0000000-0000-0000-0000-000000000001",
}

def fetch(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=15) as resp:
        return json.loads(resp.read().decode())

results = []
all_pass = True

print("=" * 70)
print("GATE: Drilldown Semantic Completeness")
print("=" * 70)

for drilldown_type, name_field, id_field in [
    ("customer_invoice", "customer_name", "invoice_number"),
    ("purchase_order", "vendor_name", "po_number"),
]:
    print(f"\n--- {drilldown_type} drilldown ---")
    data = fetch(f"{BASE}/api/v1/analytics/drilldown?type={drilldown_type}&limit=200")
    rows = data.get("rows", [])
    total = data.get("total", 0)

    blank_names = sum(1 for r in rows if not r.get(name_field) or r[name_field] in ("", "—", "-", "N/A"))
    valid_ids = sum(1 for r in rows if r.get(id_field))
    valid_refs = sum(1 for r in rows if r.get("detail_refs") and len(r["detail_refs"]) > 0)

    row_count = len(rows)
    pass_count = row_count >= 200
    pass_names = blank_names == 0
    pass_refs = valid_refs >= min(3, row_count)

    gate_pass = pass_count and pass_names and pass_refs

    status = "PASS" if gate_pass else "FAIL"
    if not gate_pass:
        all_pass = False

    print(f"  Total in DB: {total}")
    print(f"  Rows returned: {row_count} (need >=200): {'PASS' if pass_count else 'FAIL'}")
    print(f"  Blank {name_field}: {blank_names}/{row_count} (need 0): {'PASS' if pass_names else 'FAIL'}")
    print(f"  Valid {id_field}: {valid_ids}/{row_count}")
    print(f"  Valid detail_refs (first 3): {valid_refs} (need >=3): {'PASS' if pass_refs else 'FAIL'}")
    print(f"  Overall: {status}")

    if row_count >= 3:
        print(f"\n  Sample rows (first 3):")
        for i, r in enumerate(rows[:3]):
            print(f"    [{i}] {id_field}={r.get(id_field)}, {name_field}={r.get(name_field)}, detail_refs={r.get('detail_refs')}")

    results.append({
        "type": drilldown_type,
        "total": total,
        "rows_returned": row_count,
        "blank_names": blank_names,
        "valid_ids": valid_ids,
        "valid_refs": valid_refs,
        "verdict": status,
    })

print(f"\n{'=' * 70}")
overall = "PASS" if all_pass else "FAIL"
print(f"OVERALL: {overall}")
print(f"{'=' * 70}")

with open("proof/quality/after/gate_drilldown_semantic_completeness.txt", "w") as f:
    for r in results:
        f.write(f"{r['verdict']:4s}  {r['type']:25s}  rows={r['rows_returned']}  blank_names={r['blank_names']}  valid_refs={r['valid_refs']}\n")
    f.write(f"\nOVERALL: {overall}\n")

sys.exit(0 if all_pass else 1)
