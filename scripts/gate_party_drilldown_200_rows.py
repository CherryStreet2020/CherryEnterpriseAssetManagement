#!/usr/bin/env python3
"""Gate: Vendor Drilldown >= 200 rows with non-empty VendorName."""
import json, sys, subprocess, os

BASE = "http://127.0.0.1:5000/api/v1"
HEADERS = [
    "-H", "X-Tenant-Id: default",
    "-H", "X-User-Id: system@localhost",
    "-H", "X-Org-Node-Id: a0000000-0000-0000-0000-000000000001",
]

def main():
    url = f"{BASE}/drilldown/party-summary"
    r = subprocess.run(["curl", "-s", url] + HEADERS, capture_output=True, text=True, timeout=30)
    data = json.loads(r.stdout)

    total_rows = data["totalRows"]
    rows = list(data["rows"])
    total_amount = data["totalAmount"]

    non_empty_names = sum(1 for row in rows if row.get("vendorName") and len(row["vendorName"].strip()) > 0)
    name_pct = (non_empty_names / total_rows * 100) if total_rows > 0 else 0

    checks = {
        "total_rows_gte_200": total_rows >= 200,
        "vendor_name_non_empty_100pct": name_pct == 100.0,
        "total_amount_positive": total_amount > 0,
        "all_rows_have_required_fields": all(
            "vendorName" in r and "vendorCode" in r and "totalAmount" in r and "transactionCount" in r
            for r in rows
        ),
    }

    print("=== Gate: Vendor Drilldown >= 200 Rows ===")
    print(f"Total rows: {total_rows}")
    print(f"Total amount: {total_amount:,.2f}")
    print(f"Non-empty vendor names: {non_empty_names}/{total_rows} ({name_pct:.1f}%)")
    print()

    all_pass = True
    for check, result in checks.items():
        marker = "PASS" if result else "FAIL"
        if not result:
            all_pass = False
        print(f"  [{marker}] {check}")

    verdict = "PASS" if all_pass else "FAIL"
    print(f"\nVERDICT: {verdict}")

    os.makedirs("proof/gates", exist_ok=True)
    with open("proof/gates/gate_party_drilldown.json", "w") as f:
        json.dump({"verdict": verdict, "total_rows": total_rows, "total_amount": float(total_amount),
                    "checks": {k: v for k, v in checks.items()}, "sample_rows": rows[:5]}, f, indent=2)

    sys.exit(0 if all_pass else 1)

if __name__ == "__main__":
    main()
