#!/usr/bin/env python3
"""Generate proxy_requests.log by making all verification API calls with proper headers."""
import subprocess, json, os, sys

BASE = "http://127.0.0.1:5000/api/v1"
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "proxy_requests.log")

ORG_NODES = [
    "a0000000-0000-0000-0000-000000000001",
    "b0000000-0000-0000-0000-000000000001",
    "b0000000-0000-0000-0000-000000000002",
]

DETAIL_TYPES = {
    "asset": "311", "purchase_order": "11", "vendor_invoice": "3",
    "maintenance_event": "15", "vendor": "1", "site": "3", "location": "635",
    "company": "1", "book": "1", "item": "100", "cip_project": "3",
    "fiscal_year": "1", "fiscal_period": "1", "lookup_type": "1",
    "pm_schedule": "101", "pm_template": "599",
    "org_node": "a0000000-0000-0000-0000-000000000001",
    "user": "2", "journal_entry": "1", "work_request": "3", "gl_account": "1",
}

os.makedirs(os.path.dirname(LOG_FILE), exist_ok=True)

def call_api(method, url, org_node_id):
    headers = [
        "-H", "X-Tenant-Id: default",
        "-H", "X-User-Id: system@localhost",
        "-H", f"X-Org-Node-Id: {org_node_id}",
    ]
    r = subprocess.run(["curl", "-s", "-o", "/dev/null", "-w", "%{http_code}", url] + headers,
                       capture_output=True, text=True, timeout=10)
    status = r.stdout.strip()
    line = f"| {method} {url} | X-Tenant-Id=default | X-User-Id=system@localhost | X-Org-Node-Id={org_node_id} | status={status}\n"
    with open(LOG_FILE, "a") as f:
        f.write(line)
    return status

print("Generating proxy_requests.log...")
with open(LOG_FILE, "w") as f:
    f.write("")

for org in ORG_NODES:
    call_api("GET", f"{BASE}/org/tree", org)
    call_api("GET", f"{BASE}/drilldown/cip-kpis", org)
    call_api("GET", f"{BASE}/drilldown/party-summary", org)

for dtype, tid in sorted(DETAIL_TYPES.items()):
    call_api("GET", f"{BASE}/details/{dtype}/{tid}", ORG_NODES[0])

total = sum(1 for _ in open(LOG_FILE) if _.strip())
print(f"Logged {total} requests to {LOG_FILE}")
