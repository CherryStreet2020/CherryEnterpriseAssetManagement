#!/usr/bin/env python3
"""Gate: Exactly 21 Detail Types Coverage - all must return 200 OK with real data."""
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

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_DIR = os.path.join(REPO_ROOT, "proof", "runtime", "logs")
PROXY_LOG = os.path.join(OUTPUT_DIR, "proxy_requests.log")

def log_request(method, url, headers_dict, status):
    hdr_str = " | ".join(f"{k}={v}" for k, v in headers_dict.items())
    line = f"| {method} {url} | {hdr_str} | status={status}\n"
    with open(PROXY_LOG, "a") as f:
        f.write(line)

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    results = {}
    passed = 0
    failed = 0

    hdr_dict = {"X-Tenant-Id": "default", "X-User-Id": "system@localhost",
                "X-Org-Node-Id": "a0000000-0000-0000-0000-000000000001"}

    for dtype, test_id in sorted(DETAIL_TYPES.items()):
        url = f"{BASE}/details/{dtype}/{test_id}"
        try:
            r = subprocess.run(
                ["curl", "-s", "-w", "\n%{http_code}", url] + HEADERS,
                capture_output=True, text=True, timeout=10
            )
            lines = r.stdout.strip().rsplit("\n", 1)
            status = int(lines[-1]) if len(lines) > 1 else 0
            body = lines[0] if len(lines) > 1 else ""

            log_request("GET", url, hdr_dict, status)

            if status == 200:
                data = json.loads(body)
                header_fields = len(data.get("header", {})) if isinstance(data.get("header"), dict) else 0
                results[dtype] = {"status": status, "has_header": "header" in data, "header_fields": header_fields}
                passed += 1
            else:
                results[dtype] = {"status": status}
                failed += 1
        except Exception as e:
            results[dtype] = {"error": str(e)}
            failed += 1

    total_types = len(DETAIL_TYPES)

    print(f"=== Gate: 21 Detail Types Coverage ===")
    print(f"Total types tested: {total_types}")
    print(f"  200 OK: {passed}")
    print(f"  Failed: {failed}")
    print()

    for dtype, res in sorted(results.items()):
        status = res.get("status", "ERR")
        marker = "PASS" if status == 200 else "FAIL"
        extra = f"  header_fields={res.get('header_fields', '?')}" if status == 200 else ""
        print(f"  [{marker}] {dtype}: {status}{extra}")

    print()
    ok = total_types == 21 and failed == 0 and passed == 21
    verdict = "PASS" if ok else "FAIL"
    print(f"Exactly 21 types: {total_types == 21} ({total_types})")
    print(f"All 200 OK: {failed == 0}")
    print(f"\nVERDICT: {verdict}")

    with open(os.path.join(OUTPUT_DIR, "gate_21_detail_types_coverage.json"), "w") as f:
        json.dump({"verdict": verdict, "total_types": total_types, "passed_200": passed,
                    "failed": failed, "results": results}, f, indent=2)

    with open(os.path.join(OUTPUT_DIR, "gate_21_detail_types_coverage.txt"), "w") as f:
        f.write(f"Gate: 21 Detail Types Coverage\n")
        f.write(f"Total: {total_types}, 200 OK: {passed}, Failed: {failed}\n")
        for dtype, res in sorted(results.items()):
            status = res.get("status", "ERR")
            f.write(f"  {'PASS' if status == 200 else 'FAIL'}  {dtype}: {status}\n")
        f.write(f"\nVERDICT: {verdict}\n")

    sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
