#!/usr/bin/env python3
import json
import os
import subprocess
import sys

def run_sql(query):
    result = subprocess.run(
        ["psql", os.environ["DATABASE_URL"], "-t", "-A", "-F", "|", "-c", query],
        capture_output=True, text=True
    )
    return result.stdout.strip()

def main():
    with open("config/lookup_baselines.json") as f:
        config = json.load(f)

    matrix = {}
    for baseline in config["baselines"]:
        key = baseline["lookupKey"]
        required_codes = [v["code"] for v in baseline["values"]]

        rows = run_sql(f'''
            SELECT lt."Id", lt."TenantId", lt."CompanyId"
            FROM "LookupTypes" lt
            WHERE lt."Key" = '{key}'
            ORDER BY lt."TenantId", lt."CompanyId" NULLS FIRST, lt."Id"
        ''')

        key_results = []
        if rows:
            for row in rows.split("\n"):
                parts = row.split("|")
                if len(parts) < 3:
                    continue
                lt_id = parts[0].strip()
                tenant_id = parts[1].strip() or None
                company_id = parts[2].strip() or None

                code_check = {}
                for code in required_codes:
                    count = run_sql(f'''
                        SELECT count(*) FROM "LookupValues"
                        WHERE "LookupTypeId" = {lt_id}
                          AND lower("Code") = lower('{code}')
                    ''')
                    code_check[code] = int(count) > 0

                key_results.append({
                    "lookupTypeId": int(lt_id),
                    "tenantId": int(tenant_id) if tenant_id else None,
                    "companyId": int(company_id) if company_id else None,
                    "baselinePresence": code_check,
                    "allPresent": all(code_check.values())
                })

        matrix[key] = {
            "requiredCodes": required_codes,
            "scopes": key_results,
            "fullyEnforced": all(s["allPresent"] for s in key_results) if key_results else False
        }

    output_path = sys.argv[1] if len(sys.argv) > 1 else "baseline_matrix.json"
    with open(output_path, "w") as f:
        json.dump(matrix, f, indent=2)
    print(f"Matrix written to {output_path}")

    all_enforced = all(m["fullyEnforced"] for m in matrix.values())
    print(f"Overall: {'ALL ENFORCED' if all_enforced else 'GAPS FOUND'}")

if __name__ == "__main__":
    main()
