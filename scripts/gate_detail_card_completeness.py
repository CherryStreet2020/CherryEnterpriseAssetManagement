#!/usr/bin/env python3
"""Gate: Detail Card Completeness - validates all 21 detail types against contract."""
import json, sys, urllib.request

BASE = "http://127.0.0.1:5000"
HEADERS = {
    "X-Tenant-Id": "default",
    "X-User-Id": "system@localhost",
    "X-Org-Node-Id": "a0000000-0000-0000-0000-000000000001",
}

TEST_IDS = {
    "asset": "1",
    "purchase_order": "1",
    "customer_invoice": "1",
    "vendor_invoice": "3",
    "maintenance_event": "1",
    "vendor": "1",
    "customer": "1",
    "site": "1",
    "location": "1",
    "company": "1",
    "book": "1",
    "item": "99",
    "work_order": "1",
    "cip_project": "1",
    "fiscal_year": "1",
    "fiscal_period": "1",
    "lookup_type": "1",
    "pm_schedule": "101",
    "pm_template": "599",
    "org_node": "a0000000-0000-0000-0000-000000000001",
    "user": "1",
}

with open("config/detail_contract.json") as f:
    contract = json.load(f)
contract_map = {t["type"]: t for t in contract["types"]}

results = []
pass_count = 0
fail_count = 0

for dtype, test_id in sorted(TEST_IDS.items()):
    url = f"{BASE}/api/v1/details/{dtype}/{test_id}"
    req = urllib.request.Request(url, headers=HEADERS)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            status = resp.status
            body = json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        status = e.code
        body = {}
    except Exception as e:
        status = 0
        body = {"error": str(e)}

    header_fields = len(body.get("header", {})) if isinstance(body.get("header"), dict) else 0
    sections = body.get("sections", [])
    section_count = len(sections) if isinstance(sections, list) else 0
    c = contract_map.get(dtype, {})
    min_sections = c.get("sections_min", 3)

    passed = status == 200 and header_fields >= 12 and section_count >= min_sections
    verdict = "PASS" if passed else "FAIL"
    if passed:
        pass_count += 1
    else:
        fail_count += 1

    reason = ""
    if status != 200:
        reason = f"HTTP {status}"
    elif header_fields < 12:
        reason = f"header_fields={header_fields} < 12"
    elif section_count < min_sections:
        reason = f"sections={section_count} < {min_sections}"

    results.append({
        "type": dtype,
        "id": test_id,
        "status": status,
        "header_fields": header_fields,
        "sections": section_count,
        "sections_min": min_sections,
        "verdict": verdict,
        "reason": reason,
    })
    print(f"  {verdict:4s}  {dtype:25s}  HTTP={status}  header={header_fields}  sections={section_count}/{min_sections}  {reason}")

print(f"\n{'='*60}")
print(f"TOTAL: {pass_count} PASS / {fail_count} FAIL out of {len(results)}")
print(f"{'='*60}")

with open("artifacts/21_api_endpoints.json", "w") as f:
    json.dump({"test_run": "detail_card_completeness", "pass": pass_count, "fail": fail_count, "total": len(results), "results": results}, f, indent=2)

with open("proof/quality/after/gate_detail_card_completeness.txt", "w") as f:
    for r in results:
        f.write(f"{r['verdict']:4s}  {r['type']:25s}  HTTP={r['status']}  header={r['header_fields']}  sections={r['sections']}/{r['sections_min']}  {r['reason']}\n")
    f.write(f"\nTOTAL: {pass_count} PASS / {fail_count} FAIL out of {len(results)}\n")

sys.exit(0 if fail_count == 0 else 1)
