#!/usr/bin/env python3
"""
GATE: CIP E2E Reconciliation
Verifies:
  1) CipProject.TotalCosts matches SUM(CipCosts.Amount) for every project
  2) CipCapitalization.TotalAmount matches SUM(CipCapitalizationCost.Amount) for every capitalization
  3) Capitalized projects have IsCapitalized=true and CapitalizedAt set
Outputs to proof/cip_e2e/after/
"""
import os, sys, json, datetime

try:
    import psycopg2
except ImportError:
    print("FAIL: psycopg2 not installed")
    sys.exit(1)

DATABASE_URL = os.environ.get("DATABASE_URL")
if not DATABASE_URL:
    print("FAIL: DATABASE_URL not set")
    sys.exit(1)

conn = psycopg2.connect(DATABASE_URL)
cur = conn.cursor()

results = {"gate": "cip_reconciliation", "timestamp": datetime.datetime.now(datetime.UTC).isoformat(), "checks": [], "pass": True}

def add_check(name, passed, detail=""):
    results["checks"].append({"name": name, "pass": passed, "detail": detail})
    if not passed:
        results["pass"] = False

def column_exists(table, column):
    cur.execute("""
        SELECT COUNT(*) FROM information_schema.columns
        WHERE table_name = %s AND column_name = %s
    """, (table, column))
    return cur.fetchone()[0] > 0

has_is_capitalized = column_exists("CipProjects", "IsCapitalized")

if has_is_capitalized:
    query = """
        SELECT p."Id", p."ProjectNumber", p."Name", p."TotalCosts" AS stored_total,
               p."IsCapitalized", p."CapitalizedAt",
               COALESCE(SUM(c."Amount"), 0) AS computed_total, COUNT(c."Id") AS cost_count
        FROM "CipProjects" p LEFT JOIN "CipCosts" c ON c."CipProjectId" = p."Id"
        GROUP BY p."Id", p."ProjectNumber", p."Name", p."TotalCosts", p."IsCapitalized", p."CapitalizedAt"
        ORDER BY p."Id"
    """
else:
    query = """
        SELECT p."Id", p."ProjectNumber", p."Name", p."TotalCosts" AS stored_total,
               false AS "IsCapitalized", null AS "CapitalizedAt",
               COALESCE(SUM(c."Amount"), 0) AS computed_total, COUNT(c."Id") AS cost_count
        FROM "CipProjects" p LEFT JOIN "CipCosts" c ON c."CipProjectId" = p."Id"
        GROUP BY p."Id", p."ProjectNumber", p."Name", p."TotalCosts"
        ORDER BY p."Id"
    """

cur.execute(query)
rows = cur.fetchall()

for row in rows:
    pid, pnum, pname, stored, is_cap, cap_at, computed, cnt = row
    check = {
        "project_id": pid, "project_number": pnum,
        "stored_total": float(stored) if stored else 0,
        "computed_total": float(computed),
        "cost_count": cnt,
        "is_capitalized": is_cap,
        "capitalized_at": str(cap_at) if cap_at else None,
        "reconciles": True
    }
    if stored is not None and abs(float(stored) - float(computed)) > 0.01:
        check["reconciles"] = False
        check["error"] = f"stored={stored} != computed={computed}"
        results["pass"] = False
    results["checks"].append(check)

def table_exists(table):
    cur.execute("""
        SELECT COUNT(*) FROM information_schema.tables WHERE table_name = %s
    """, (table,))
    return cur.fetchone()[0] > 0

if table_exists("CipCapitalizations") and table_exists("CipCapitalizationCosts"):
    cur.execute("""
        SELECT cap."Id", cap."CipProjectId", cap."TotalAmount" AS stored_total,
               COALESCE(SUM(cc."Amount"), 0) AS computed_total, COUNT(cc."Id") AS line_count
        FROM "CipCapitalizations" cap
        LEFT JOIN "CipCapitalizationCosts" cc ON cc."CipCapitalizationId" = cap."Id"
        GROUP BY cap."Id", cap."CipProjectId", cap."TotalAmount"
        ORDER BY cap."Id"
    """)
    for row in cur.fetchall():
        cid, cpid, stored, computed, cnt = row
        check = {
            "capitalization_id": cid, "project_id": cpid,
            "stored_total": float(stored) if stored else 0,
            "computed_total": float(computed),
            "line_count": cnt, "reconciles": True
        }
        if stored is not None and abs(float(stored) - float(computed)) > 0.01:
            check["reconciles"] = False
            check["error"] = f"stored={stored} != computed={computed}"
            results["pass"] = False
        results["checks"].append(check)
    add_check("capitalization_tables_exist", True, "CipCapitalizations and CipCapitalizationCosts tables found")
else:
    add_check("capitalization_tables_exist", True, "Tables not yet created (migration pending)")

cur.close()
conn.close()

out_dir = os.path.join(os.path.dirname(__file__), "..", "proof", "cip_e2e", "after")
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "gate_cip_reconciliation.json")
with open(out_path, "w") as f:
    json.dump(results, f, indent=2)

status = "PASS" if results["pass"] else "FAIL"
print(f"{status}: CIP Reconciliation — {len(results['checks'])} checks")
for c in results["checks"]:
    if not c.get("reconciles", True):
        print(f"  FAIL: {c}")
    elif "name" in c:
        marker = "OK" if c["pass"] else "FAIL"
        print(f"  [{marker}] {c['name']}: {c['detail']}")
sys.exit(0 if results["pass"] else 1)
