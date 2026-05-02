#!/usr/bin/env python3
"""Gate: Org Selector Scoping Delta - verify KPI values differ between companies."""
import json, sys, subprocess, os

BASE = "http://127.0.0.1:5000/api/v1"

ORG_A = "b0000000-0000-0000-0000-000000000001"
ORG_B = "b0000000-0000-0000-0000-000000000002"

def call_kpis(org_node_id):
    headers = [
        "-H", "X-Tenant-Id: default",
        "-H", "X-User-Id: system@localhost",
        "-H", f"X-Org-Node-Id: {org_node_id}",
    ]
    r = subprocess.run(
        ["curl", "-s", f"{BASE}/drilldown/cip-kpis"] + headers,
        capture_output=True, text=True, timeout=10
    )
    return json.loads(r.stdout)

def call_party(org_node_id):
    headers = [
        "-H", "X-Tenant-Id: default",
        "-H", "X-User-Id: system@localhost",
        "-H", f"X-Org-Node-Id: {org_node_id}",
    ]
    r = subprocess.run(
        ["curl", "-s", f"{BASE}/drilldown/party-summary"] + headers,
        capture_output=True, text=True, timeout=10
    )
    return json.loads(r.stdout)

def main():
    kpi_a = call_kpis(ORG_A)
    kpi_b = call_kpis(ORG_B)
    party_a = call_party(ORG_A)
    party_b = call_party(ORG_B)

    print("=== Gate: Org Selector Scoping Delta ===")
    print(f"Org A ({ORG_A}): {kpi_a['orgScope']}")
    print(f"  CIP: projects={kpi_a['totalProjects']}, budget={kpi_a['totalBudget']}, spent={kpi_a['totalSpent']}")
    print(f"  Party: rows={party_a['totalRows']}, amount={party_a['totalAmount']}")
    print(f"Org B ({ORG_B}): {kpi_b['orgScope']}")
    print(f"  CIP: projects={kpi_b['totalProjects']}, budget={kpi_b['totalBudget']}, spent={kpi_b['totalSpent']}")
    print(f"  Party: rows={party_b['totalRows']}, amount={party_b['totalAmount']}")
    print()

    kpi_fields = ["totalProjects", "totalBudget", "totalSpent", "activeProjects", "completedProjects", "cancelledProjects"]
    kpi_deltas = {}
    for field in kpi_fields:
        a_val = kpi_a.get(field, 0)
        b_val = kpi_b.get(field, 0)
        kpi_deltas[field] = {"a": a_val, "b": b_val, "differs": a_val != b_val}

    party_delta = {
        "rows": {"a": party_a["totalRows"], "b": party_b["totalRows"], "differs": party_a["totalRows"] != party_b["totalRows"]},
        "amount": {"a": party_a["totalAmount"], "b": party_b["totalAmount"], "differs": party_a["totalAmount"] != party_b["totalAmount"]},
    }

    kpi_diff_count = sum(1 for d in kpi_deltas.values() if d["differs"])
    party_diff_count = sum(1 for d in party_delta.values() if d["differs"])

    checks = {
        "kpi_delta_gte_2_fields": kpi_diff_count >= 2,
        "party_delta_exists": party_diff_count >= 1,
        "org_scopes_differ": kpi_a["orgScope"] != kpi_b["orgScope"],
    }

    all_pass = all(checks.values())

    for field, delta in kpi_deltas.items():
        marker = "DELTA" if delta["differs"] else "SAME"
        print(f"  [{marker}] {field}: A={delta['a']} vs B={delta['b']}")

    print()
    for check, result in checks.items():
        marker = "PASS" if result else "FAIL"
        print(f"  [{marker}] {check}")

    verdict = "PASS" if all_pass else "FAIL"
    print(f"\n  KPI fields with delta: {kpi_diff_count}/6")
    print(f"  Party fields with delta: {party_diff_count}/2")
    print(f"\nVERDICT: {verdict}")

    os.makedirs("proof/gates", exist_ok=True)
    with open("proof/gates/gate_org_scoping_delta.json", "w") as f:
        json.dump({"verdict": verdict, "kpi_deltas": kpi_deltas, "party_delta": party_delta,
                    "checks": checks, "kpi_diff_count": kpi_diff_count}, f, indent=2)

    sys.exit(0 if all_pass else 1)

if __name__ == "__main__":
    main()
