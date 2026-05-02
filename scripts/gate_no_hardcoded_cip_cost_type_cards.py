#!/usr/bin/env python3
"""Gate: Verify CIP cost type cards come from DB lookups, not hardcoded enums."""
import os, sys, re, psycopg2

checks = []
def check(name, passed, detail=""):
    status = "OK" if passed else "FAIL"
    checks.append({"name": name, "status": status, "detail": detail})
    print(f"  [{status}] {name}: {detail}")

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

index_cshtml = os.path.join(REPO, "Pages", "CIP", "Index.cshtml")
index_cs = os.path.join(REPO, "Pages", "CIP", "Index.cshtml.cs")

with open(index_cshtml) as f:
    view_content = f.read()
with open(index_cs) as f:
    model_content = f.read()

has_enum_getvalues = "Enum.GetValues<CipCostType>" in view_content
check("no_enum_getvalues_in_view", not has_enum_getvalues,
      "No Enum.GetValues<CipCostType> in Index.cshtml" if not has_enum_getvalues else "FOUND hardcoded enum iteration!")

has_getcoststypename = "GetCostTypeName" in view_content
check("no_hardcoded_name_switch", not has_getcoststypename,
      "No GetCostTypeName switch" if not has_getcoststypename else "FOUND hardcoded name switch!")

has_lookup_iteration = "Model.CipCostTypeLookups" in view_content
check("uses_db_lookups", has_lookup_iteration,
      "View iterates Model.CipCostTypeLookups" if has_lookup_iteration else "Missing DB lookup iteration")

has_lookup_service = "ILookupService" in model_content
check("model_uses_ilookupservice", has_lookup_service,
      "Page model uses ILookupService" if has_lookup_service else "Missing ILookupService")

no_costsByType_enum = "CostsByType" not in model_content
check("no_enum_costs_dict", no_costsByType_enum,
      "No Dictionary<CipCostType> in model" if no_costsByType_enum else "FOUND enum-keyed dictionary")

has_db_dict = "CostsByLookupId" in model_content
check("uses_lookup_id_dict", has_db_dict,
      "Uses Dictionary<int, decimal> CostsByLookupId" if has_db_dict else "Missing lookup-keyed dictionary")

DB_URL = os.environ.get('DATABASE_URL')
if DB_URL:
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()
    cur.execute("""SELECT COUNT(*) FROM "LookupValues" lv
        JOIN "LookupTypes" lt ON lv."LookupTypeId" = lt."Id"
        WHERE lt."Key" = 'CipCostType'""")
    lv_count = cur.fetchone()[0]
    check("db_has_cip_cost_types", lv_count >= 10, f"{lv_count} CipCostType lookup values in DB")
    conn.close()

passed = sum(1 for c in checks if c["status"] == "OK")
failed = sum(1 for c in checks if c["status"] == "FAIL")
print(f"\n{'PASS' if failed == 0 else 'FAIL'}: No Hardcoded CIP Cost Type Cards — {passed}/{passed+failed}")
sys.exit(0 if failed == 0 else 1)
