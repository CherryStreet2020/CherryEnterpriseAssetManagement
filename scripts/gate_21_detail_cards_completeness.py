#!/usr/bin/env python3
"""Gate: 21 Detail Cards Completeness - each must have 8+ non-empty fields, 1+ relationship, money/qty where applicable."""
import json, sys, subprocess, os

BASE = "http://127.0.0.1:5000/api/v1"
HEADERS = [
    "-H", "X-Tenant-Id: default",
    "-H", "X-User-Id: system@localhost",
    "-H", "X-Org-Node-Id: a0000000-0000-0000-0000-000000000001",
]

DETAIL_TYPES = {
    "asset": "311",
    "purchase_order": "11",
    "vendor_invoice": "3",
    "maintenance_event": "15",
    "vendor": "1",
    "site": "3",
    "location": "635",
    "company": "1",
    "book": "1",
    "item": "100",
    "cip_project": "3",
    "fiscal_year": "1",
    "fiscal_period": "1",
    "lookup_type": "1",
    "pm_schedule": "101",
    "pm_template": "599",
    "org_node": "a0000000-0000-0000-0000-000000000001",
    "user": "2",
    "journal_entry": "1",
    "work_request": "3",
    "gl_account": "1",
}

MONEY_TYPES = {"asset", "purchase_order", "vendor_invoice", "maintenance_event", "cip_project",
               "journal_entry", "pm_schedule", "item", "work_request", "book", "gl_account"}

MONEY_KEYWORDS = {"amount", "cost", "price", "total", "budget", "spent", "debit", "credit",
                   "balance", "subtotal", "tax", "shipping", "estimated", "quantity", "qty",
                   "value", "limit", "rate"}

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_DIR = os.path.join(REPO_ROOT, "proof", "runtime", "logs")
PROXY_LOG = os.path.join(OUTPUT_DIR, "proxy_requests.log")

def log_request(method, url, headers_dict, status):
    hdr_str = " | ".join(f"{k}={v}" for k, v in headers_dict.items())
    line = f"| {method} {url} | {hdr_str} | status={status}\n"
    with open(PROXY_LOG, "a") as f:
        f.write(line)

def count_non_empty(d):
    if not isinstance(d, dict):
        return 0
    count = 0
    for v in d.values():
        if v is not None and v != "" and v != 0 and v is not False:
            count += 1
        elif isinstance(v, (int, float)):
            count += 1
    return count

def has_money_field(d):
    if not isinstance(d, dict):
        return False
    for key in d.keys():
        if any(kw in key.lower() for kw in MONEY_KEYWORDS):
            return True
    return False

def has_relationship(data):
    related = data.get("related", {})
    if isinstance(related, dict) and len(related) > 0:
        return True
    header = data.get("header", {})
    if isinstance(header, dict):
        for key in header.keys():
            if any(kw in key.lower() for kw in {"id", "_id", "assetid", "companyid", "vendorid", "siteid"}):
                return True
    return False

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    hdr_dict = {"X-Tenant-Id": "default", "X-User-Id": "system@localhost",
                "X-Org-Node-Id": "a0000000-0000-0000-0000-000000000001"}

    results = []
    pass_count = 0
    fail_count = 0

    for dtype, test_id in sorted(DETAIL_TYPES.items()):
        url = f"{BASE}/details/{dtype}/{test_id}"
        try:
            r = subprocess.run(
                ["curl", "-s", "-w", "\n%{http_code}", url] + HEADERS,
                capture_output=True, text=True, timeout=10
            )
            lines = r.stdout.strip().rsplit("\n", 1)
            status = int(lines[-1]) if len(lines) > 1 else 0
            body_str = lines[0] if len(lines) > 1 else "{}"
            log_request("GET", url, hdr_dict, status)

            if status != 200:
                results.append({"type": dtype, "verdict": "FAIL", "reason": f"HTTP {status}", "header_fields": 0, "has_relationship": False, "has_money": False})
                fail_count += 1
                continue

            data = json.loads(body_str)
            header = data.get("header", {})
            non_empty = count_non_empty(header)
            has_rel = has_relationship(data)
            has_money = has_money_field(header)
            needs_money = dtype in MONEY_TYPES

            reasons = []
            if non_empty < 8:
                reasons.append(f"header_fields={non_empty} < 8")
            if not has_rel:
                reasons.append("no relationship link")
            if needs_money and not has_money:
                reasons.append("missing money/qty field")

            passed = len(reasons) == 0
            if passed:
                pass_count += 1
            else:
                fail_count += 1

            results.append({
                "type": dtype,
                "id": test_id,
                "verdict": "PASS" if passed else "FAIL",
                "header_fields": non_empty,
                "has_relationship": has_rel,
                "has_money": has_money,
                "needs_money": needs_money,
                "reason": "; ".join(reasons) if reasons else "",
            })
        except Exception as e:
            results.append({"type": dtype, "verdict": "FAIL", "reason": str(e), "header_fields": 0, "has_relationship": False, "has_money": False})
            fail_count += 1

    print("=== Gate: 21 Detail Cards Completeness ===")
    for r in results:
        reason_str = f"  ({r['reason']})" if r.get("reason") else ""
        print(f"  [{r['verdict']}] {r['type']:25s}  fields={r['header_fields']}  rel={r['has_relationship']}  money={r.get('has_money', '?')}{reason_str}")

    print(f"\nTotal: {pass_count} PASS / {fail_count} FAIL out of {len(results)}")
    all_pass = fail_count == 0 and len(results) == 21
    verdict = "PASS" if all_pass else "FAIL"
    print(f"VERDICT: {verdict}")

    with open(os.path.join(OUTPUT_DIR, "gate_21_detail_cards_completeness.json"), "w") as f:
        json.dump({"verdict": verdict, "pass": pass_count, "fail": fail_count, "total": len(results), "results": results}, f, indent=2)

    with open(os.path.join(OUTPUT_DIR, "gate_21_detail_cards_completeness.txt"), "w") as f:
        f.write(f"Gate: 21 Detail Cards Completeness\n")
        for r in results:
            f.write(f"  {r['verdict']:4s}  {r['type']:25s}  fields={r['header_fields']}  rel={r['has_relationship']}  money={r.get('has_money', '?')}  {r.get('reason', '')}\n")
        f.write(f"\nTotal: {pass_count} PASS / {fail_count} FAIL out of {len(results)}\n")
        f.write(f"VERDICT: {verdict}\n")

    sys.exit(0 if all_pass else 1)

if __name__ == "__main__":
    main()
