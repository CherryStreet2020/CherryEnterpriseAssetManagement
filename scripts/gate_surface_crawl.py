#!/usr/bin/env python3
"""Strict surface area crawl proof - port 5000 only, forbidden port excluded, no data schema."""
import json, sys, urllib.request, time

BASE = "http://127.0.0.1:5000"
HEADERS = {
    "X-Tenant-Id": "default",
    "X-User-Id": "system@localhost",
    "X-Org-Node-Id": "a0000000-0000-0000-0000-000000000001",
}

API_ENDPOINTS = [
    "/api/v1/org/tree",
    "/api/v1/analytics/drilldown?type=customer_invoice&limit=5",
    "/api/v1/analytics/drilldown?type=purchase_order&limit=5",
    "/api/v1/analytics/drilldown?type=vendor_invoice&limit=5",
    "/api/v1/analytics/drilldown?type=maintenance_event&limit=5",
    "/api/v1/analytics/drilldown?type=asset&limit=5",
    "/api/v1/details/asset/1",
    "/api/v1/details/purchase_order/1",
    "/api/v1/details/customer_invoice/1",
    "/api/v1/details/vendor_invoice/3",
    "/api/v1/details/maintenance_event/1",
    "/api/v1/details/vendor/1",
    "/api/v1/details/customer/1",
    "/api/v1/details/site/1",
    "/api/v1/details/location/1",
    "/api/v1/details/company/1",
    "/api/v1/details/book/1",
    "/api/v1/details/item/99",
    "/api/v1/details/work_order/1",
    "/api/v1/details/cip_project/1",
    "/api/v1/details/fiscal_year/1",
    "/api/v1/details/fiscal_period/1",
    "/api/v1/details/lookup_type/1",
    "/api/v1/details/pm_schedule/101",
    "/api/v1/details/pm_template/599",
    "/api/v1/details/org_node/a0000000-0000-0000-0000-000000000001",
    "/api/v1/details/user/1",
]

PAGE_ENDPOINTS = [
    "/",
    "/Assets",
    "/Admin/Company",
    "/Purchasing",
    "/AccountsPayable",
    "/Maintenance",
    "/Materials/Items",
    "/CIP",
    "/Admin/Users",
    "/Admin/Lookups",
    "/Reports/ReportHub",
    "/Help",
]

results = []
pass_count = 0
fail_count = 0

print("=" * 70)
print("GATE: Surface Area Crawl (port 5000 only)")
print(f"Proxy Base URL: {BASE}")
print("=" * 70)

for endpoint in API_ENDPOINTS + PAGE_ENDPOINTS:
    url = f"{BASE}{endpoint}"
    req = urllib.request.Request(url, headers=HEADERS)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            status = resp.status
            size = len(resp.read())
    except urllib.error.HTTPError as e:
        status = e.code
        size = 0
    except Exception as e:
        status = 0
        size = 0

    passed = status == 200
    if passed:
        pass_count += 1
    else:
        fail_count += 1

    results.append({"url": endpoint, "status": status, "size": size, "verdict": "PASS" if passed else "FAIL"})
    print(f"  {'PASS' if passed else 'FAIL'}  {status}  {endpoint}")

print(f"\n{'=' * 70}")
print(f"TOTAL: {pass_count} PASS / {fail_count} FAIL out of {len(results)}")
print(f"Proxy Base URL: {BASE}")
print(f"{'=' * 70}")

output = {
    "crawl_time": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    "proxy_base_url": BASE,
    "port": 5000,
    "total_endpoints": len(results),
    "pass": pass_count,
    "fail": fail_count,
    "results": results,
}

with open("proof/coverage/surface_area_no_exceptions.json", "w") as f:
    json.dump(output, f, indent=2)

sys.exit(0 if fail_count == 0 else 1)
