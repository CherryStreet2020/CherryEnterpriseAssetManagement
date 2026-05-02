#!/usr/bin/env python3
"""
GATE: No Hardcoded CIP Cost Types
Verifies:
  1) No Razor files contain hardcoded CipCostType <option> or <select> lists
  2) CIP pages use ILookupService for cost type dropdowns
  3) CipCostType baseline exists in config/lookup_baselines.json
  4) CipCostType LookupValues exist in the database
Outputs to proof/cip_e2e/after/
"""
import os, sys, json, datetime, re, glob

try:
    import psycopg2
except ImportError:
    print("FAIL: psycopg2 not installed")
    sys.exit(1)

DATABASE_URL = os.environ.get("DATABASE_URL")
if not DATABASE_URL:
    print("FAIL: DATABASE_URL not set")
    sys.exit(1)

WORKSPACE = os.path.join(os.path.dirname(__file__), "..")
results = {"gate": "no_hardcoded_cip_cost_types", "timestamp": datetime.datetime.now(datetime.UTC).isoformat(), "checks": [], "pass": True}

def add_check(name, passed, detail=""):
    results["checks"].append({"name": name, "pass": passed, "detail": detail})
    if not passed:
        results["pass"] = False

hardcoded_patterns = [
    re.compile(r'<option[^>]*>(?:Construction|Engineering|Equipment|Labor|Materials|Freight|Installation|Testing|Permits|Professional|Interest)</option>', re.IGNORECASE),
]

cip_pages = glob.glob(os.path.join(WORKSPACE, "Pages", "CIP", "*.cshtml"))
cip_pages += glob.glob(os.path.join(WORKSPACE, "Pages", "CIP", "*.cshtml.cs"))
violations = []

for fpath in cip_pages:
    with open(fpath, "r") as f:
        content = f.read()
    for i, pat in enumerate(hardcoded_patterns):
        matches = pat.findall(content)
        if matches:
            rel = os.path.relpath(fpath, WORKSPACE)
            for m in matches:
                violations.append({"file": rel, "pattern_index": i, "match": m[:80]})

add_check("no_hardcoded_cost_type_options", len(violations) == 0,
          f"{len(violations)} violations found" + (f": {violations[:3]}" if violations else ""))

baselines_path = os.path.join(WORKSPACE, "config", "lookup_baselines.json")
if os.path.exists(baselines_path):
    with open(baselines_path) as f:
        baselines = json.load(f)
    baselines_str = json.dumps(baselines)
    has_cip = "CipCostType" in baselines_str
    add_check("baseline_has_CipCostType", has_cip, f"Found in lookup_baselines.json: {has_cip}")
else:
    add_check("baseline_has_CipCostType", False, "lookup_baselines.json not found")

conn = psycopg2.connect(DATABASE_URL)
cur = conn.cursor()
try:
    cur.execute("""
        SELECT COUNT(*) FROM "LookupValues" lv
        JOIN "LookupTypes" lt ON lt."Id" = lv."LookupTypeId"
        WHERE lt."Key" = 'CipCostType'
    """)
    db_count = cur.fetchone()[0]
    add_check("db_has_CipCostType_values", db_count > 0, f"{db_count} CipCostType lookup values in DB")

    cur.execute("""
        SELECT lv."Code", lv."Name" FROM "LookupValues" lv
        JOIN "LookupTypes" lt ON lt."Id" = lv."LookupTypeId"
        WHERE lt."Key" = 'CipCostType'
        ORDER BY lv."SortOrder", lv."Code"
    """)
    values = [{"code": r[0], "name": r[1]} for r in cur.fetchall()]
    results["cip_cost_type_values"] = values
except Exception as e:
    add_check("db_has_CipCostType_values", False, f"Query error: {e}")
cur.close()
conn.close()

lookup_usage = False
for fpath in cip_pages:
    if fpath.endswith(".cshtml.cs"):
        with open(fpath, "r") as f:
            content = f.read()
            if "ILookupService" in content or "LookupService" in content:
                lookup_usage = True
                break
add_check("cip_pages_use_ILookupService", lookup_usage, f"ILookupService found in CIP page models: {lookup_usage}")

out_dir = os.path.join(WORKSPACE, "proof", "cip_e2e", "after")
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "gate_no_hardcoded_cip_cost_types.json")
with open(out_path, "w") as f:
    json.dump(results, f, indent=2)

status = "PASS" if results["pass"] else "FAIL"
print(f"{status}: No Hardcoded CIP Cost Types — {len(results['checks'])} checks")
for c in results["checks"]:
    marker = "OK" if c["pass"] else "FAIL"
    print(f"  [{marker}] {c['name']}: {c['detail']}")
sys.exit(0 if results["pass"] else 1)
