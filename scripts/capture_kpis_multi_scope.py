#!/usr/bin/env python3
"""Capture KPIs at multiple org scopes (holding, opco, site) via proxy_client."""
import os
import sys
import json
import subprocess

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proxy_client

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_DIR = os.path.join(REPO_ROOT, "proof", "org", "after")

DB_QUERY = "SELECT id,node_type,name FROM platform.org_node WHERE tenant_code='default' ORDER BY node_type,name"

def query_org_nodes():
    try:
        db_url = os.environ.get("DATABASE_URL", "")
        if not db_url:
            print("WARNING: DATABASE_URL not set, trying default psql connection")

        cmd = ["psql", "-t", "-A", "-F", "|", "-c", DB_QUERY]
        if db_url:
            cmd.insert(1, db_url)

        result = subprocess.run(cmd, capture_output=True, text=True, timeout=15)
        if result.returncode != 0:
            print(f"psql error: {result.stderr}")
            return []

        rows = []
        for line in result.stdout.strip().splitlines():
            line = line.strip()
            if not line or line.startswith("("):
                continue
            parts = line.split("|")
            if len(parts) >= 3:
                rows.append({
                    "id": parts[0].strip(),
                    "node_type": parts[1].strip(),
                    "name": parts[2].strip(),
                })
        return rows
    except FileNotFoundError:
        print("ERROR: psql not found. Cannot query org nodes.")
        return []
    except Exception as e:
        print(f"ERROR querying org nodes: {e}")
        return []

def fetch_kpis(org_node_id, label):
    print(f"Fetching KPIs for {label} (org_node_id={org_node_id})...")
    try:
        resp = proxy_client.get("/analytics/kpis", org_node_id=org_node_id)
        data = {"status_code": resp.status_code, "org_node_id": org_node_id, "label": label}
        try:
            data["body"] = resp.json()
        except Exception:
            data["body"] = resp.text[:2000]
        return data
    except Exception as e:
        return {"error": str(e), "org_node_id": org_node_id, "label": label}

def save_json(data, filename):
    path = os.path.join(OUTPUT_DIR, filename)
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
    print(f"  Saved: {path}")

def main():
    nodes = query_org_nodes()
    if not nodes:
        print("WARNING: No org nodes returned from DB. Creating placeholder files.")
        for fname in ["kpis_holding.json", "kpis_opco_a.json", "kpis_opco_b.json", "kpis_site_a.json", "kpis_site_b.json"]:
            save_json({"error": "no org nodes found in DB", "query": DB_QUERY}, fname)
        sys.exit(1)

    print(f"Found {len(nodes)} org nodes:")
    for n in nodes:
        print(f"  {n['node_type']:15s} {n['name']:30s} {n['id']}")

    holdings = [n for n in nodes if n["node_type"].lower() in ("holding", "holdingcompany", "holding_company")]
    companies = [n for n in nodes if n["node_type"].lower() in ("company", "opco", "operating_company")]
    sites = [n for n in nodes if n["node_type"].lower() in ("site", "location", "facility")]

    if not holdings:
        holdings = nodes[:1]
        print(f"WARNING: No holding node found, using first node as holding: {holdings[0]['name']}")
    if not companies:
        remaining = [n for n in nodes if n not in holdings]
        companies = remaining[:2]
        print(f"WARNING: No company nodes found, using next nodes: {[c['name'] for c in companies]}")
    if not sites:
        remaining = [n for n in nodes if n not in holdings and n not in companies]
        sites = remaining[:2]
        print(f"WARNING: No site nodes found, using next nodes: {[s['name'] for s in sites]}")

    holding = holdings[0]
    data = fetch_kpis(holding["id"], f"holding:{holding['name']}")
    save_json(data, "kpis_holding.json")

    for i, label_suffix in enumerate(["a", "b"]):
        if i < len(companies):
            c = companies[i]
            data = fetch_kpis(c["id"], f"opco:{c['name']}")
            save_json(data, f"kpis_opco_{label_suffix}.json")
        else:
            save_json({"skipped": True, "reason": f"fewer than {i+1} company nodes"}, f"kpis_opco_{label_suffix}.json")

    parent_company = companies[0] if companies else None
    if parent_company:
        company_sites = [s for s in sites]
    else:
        company_sites = sites

    for i, label_suffix in enumerate(["a", "b"]):
        if i < len(company_sites):
            s = company_sites[i]
            data = fetch_kpis(s["id"], f"site:{s['name']}")
            save_json(data, f"kpis_site_{label_suffix}.json")
        else:
            save_json({"skipped": True, "reason": f"fewer than {i+1} site nodes"}, f"kpis_site_{label_suffix}.json")

    print("\nDone. All KPI captures saved.")

if __name__ == "__main__":
    main()
