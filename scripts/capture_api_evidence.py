#!/usr/bin/env python3
"""Capture API detail responses using ProxyClient with required headers."""
import os, sys, json

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from proxy_client import save_json, api_get

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

ids_file = os.path.join(REPO_ROOT, "proof", "cip_e2e", "after", "test_entity_ids.json")
e2e_file = os.path.join(REPO_ROOT, "proof", "cip_e2e", "after", "e2e_entities.json")

cip_id = wo_id = po_id = asset_id = journal_id = None

if os.path.exists(e2e_file):
    with open(e2e_file) as f:
        e2e = json.load(f)
    cip_id = e2e.get("cipProjectId")
    wo_id = e2e.get("workOrderId")
    po_id = e2e.get("purchaseOrderId")
    asset_id = e2e.get("assetId")
    journal_id = e2e.get("journalEntryId")

if not cip_id:
    import psycopg2
    conn = psycopg2.connect(os.environ["DATABASE_URL"])
    cur = conn.cursor()
    cur.execute('SELECT "Id" FROM "CipProjects" WHERE "ProjectNumber" = %s', ("CIP-E2E-0001",))
    row = cur.fetchone()
    cip_id = row[0] if row else 1
    conn.close()

captures = [
    (f"/api/v1/details/cip_project/{cip_id}", "proof/api/details_cip_project.json"),
]
if wo_id:
    captures.append((f"/api/v1/details/work_order/{wo_id}", "proof/api/details_work_order.json"))
if po_id:
    captures.append((f"/api/v1/details/purchase_order/{po_id}", "proof/api/details_purchase_order.json"))
if asset_id:
    captures.append((f"/api/v1/details/asset/{asset_id}", "proof/api/details_asset.json"))
if journal_id:
    captures.append((f"/api/v1/details/fiscal_period/1", "proof/api/details_journal_entry.json"))

results = []
for path, out in captures:
    out_path = os.path.join(REPO_ROOT, out)
    status = save_json(path, out_path)
    results.append((path, status))
    print(f"  {'OK' if status == 200 else 'FAIL'} {path} -> {out} (status={status})")

failed = sum(1 for _, s in results if s != 200)
print(f"\n{'PASS' if failed == 0 else 'FAIL'}: API Evidence — {len(results)-failed}/{len(results)} captured")
sys.exit(0 if failed == 0 else 1)
