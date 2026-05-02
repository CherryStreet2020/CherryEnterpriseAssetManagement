#!/usr/bin/env python3
"""
GATE: CIP Costs Reconciliation (Phase 4)
Verifies:
  1) stored_total matches computed SUM(CipCosts.Amount)
  2) stored_total > 0 requires cost_count > 0
  3) cost_count > 0 requires computed_total > 0
Outputs to proof/cip/after/
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

cur.execute("""
    SELECT
        p."Id",
        p."ProjectNumber",
        p."Name",
        p."TotalCosts"       AS stored_total,
        COALESCE(SUM(c."Amount"), 0) AS computed_total,
        COUNT(c."Id")        AS cost_count
    FROM "CipProjects" p
    LEFT JOIN "CipCosts" c ON c."CipProjectId" = p."Id"
    GROUP BY p."Id", p."ProjectNumber", p."Name", p."TotalCosts"
    ORDER BY p."Id"
""")

rows = cur.fetchall()
cur.close()
conn.close()

results = []
failures = []

print("=" * 90)
print("GATE: CIP Costs Reconciliation")
print("=" * 90)
print(f"{'ID':<4} {'ProjectNumber':<16} {'Name':<28} {'Stored':>12} {'Computed':>12} {'CostCount':>10} {'Status':<8}")
print("-" * 90)

for row in rows:
    pid, pnum, name, stored, computed, count = row
    stored = float(stored) if stored else 0.0
    computed = float(computed) if computed else 0.0
    discrepancy = abs(stored - computed)

    status = "PASS"
    fail_reasons = []

    if discrepancy > 0.01:
        status = "FAIL"
        fail_reasons.append(f"discrepancy={discrepancy:.2f} (stored={stored:.2f} vs computed={computed:.2f})")

    if stored > 0 and count == 0:
        status = "FAIL"
        fail_reasons.append(f"stored={stored:.2f} but cost_count=0 (phantom spend)")

    if computed > 0 and count == 0:
        status = "FAIL"
        fail_reasons.append(f"computed={computed:.2f} but cost_count=0 (impossible)")

    result = {
        "id": pid,
        "project_number": pnum,
        "name": name,
        "stored_total": stored,
        "computed_total": computed,
        "cost_count": count,
        "discrepancy": round(discrepancy, 2),
        "status": status,
        "fail_reasons": fail_reasons
    }
    results.append(result)

    if status == "FAIL":
        failures.append(result)

    print(f"{pid:<4} {pnum:<16} {name:<28} {stored:>12.2f} {computed:>12.2f} {count:>10} {status:<8}")
    if fail_reasons:
        for reason in fail_reasons:
            print(f"     >> {reason}")

print("-" * 90)

overall = "PASS" if len(failures) == 0 else "FAIL"
print(f"\nProjects checked: {len(results)}")
print(f"Failures: {len(failures)}")
print(f"{'=' * 90}")
print(f"OVERALL: {overall}")
print(f"{'=' * 90}")

audit = {
    "gate": "cip_costs_reconcile",
    "timestamp": datetime.datetime.now(datetime.UTC).isoformat() + "Z",
    "project_count": len(results),
    "failure_count": len(failures),
    "overall": overall,
    "projects": results
}

output_dir = os.environ.get("GATE_OUTPUT_DIR", "proof/cip/after")
os.makedirs(output_dir, exist_ok=True)

with open(os.path.join(output_dir, "cip_reconcile_audit.json"), "w") as f:
    json.dump(audit, f, indent=2)

with open(os.path.join(output_dir, "gate_cip_costs_reconcile.txt"), "w") as f:
    f.write(f"GATE: CIP Costs Reconciliation\n")
    f.write(f"Overall: {overall}\n")
    f.write(f"Projects: {len(results)}, Failures: {len(failures)}\n\n")
    for r in results:
        f.write(f"{r['project_number']} | stored={r['stored_total']:.2f} computed={r['computed_total']:.2f} count={r['cost_count']} | {r['status']}\n")
        for reason in r['fail_reasons']:
            f.write(f"  >> {reason}\n")

sys.exit(0 if overall == "PASS" else 1)
