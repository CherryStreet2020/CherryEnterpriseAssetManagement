#!/usr/bin/env python3
"""Fetch org tree via proxy_client and save proof artifacts."""
import os
import sys
import json
import re

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proxy_client

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HOLDING_UUID_FILE = os.path.join(REPO_ROOT, "proof", "org", "after", "holding_uuid.txt")
OUTPUT_JSON = os.path.join(REPO_ROOT, "proof", "org", "after", "org_tree_proxy.json")
OUTPUT_COUNT = os.path.join(REPO_ROOT, "proof", "org", "after", "org_tree_nodecount.txt")

UUID_PATTERN = re.compile(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')

def read_holding_uuid():
    if not os.path.exists(HOLDING_UUID_FILE):
        print(f"ERROR: {HOLDING_UUID_FILE} not found.")
        sys.exit(1)
    with open(HOLDING_UUID_FILE, "r") as f:
        content = f.read().strip()
    match = UUID_PATTERN.search(content)
    if match:
        return match.group(0)
    print(f"ERROR: No UUID found in {HOLDING_UUID_FILE}")
    print(f"Content: {content[:200]}")
    sys.exit(1)

def count_nodes(node):
    if not node or not isinstance(node, dict):
        return 0
    count = 1
    children = node.get("children", [])
    if isinstance(children, list):
        for child in children:
            count += count_nodes(child)
    return count

def count_nodes_in_response(data):
    if isinstance(data, dict):
        if "children" in data or "id" in data:
            return count_nodes(data)
        if "root" in data:
            return count_nodes(data["root"])
        if "nodes" in data and isinstance(data["nodes"], list):
            return len(data["nodes"])
        total = data.get("totalNodes", None)
        if total is not None:
            return int(total)
    if isinstance(data, list):
        total = 0
        for item in data:
            total += count_nodes(item)
        return total
    return 0

def main():
    holding_uuid = read_holding_uuid()
    print(f"Holding UUID: {holding_uuid}")

    try:
        resp = proxy_client.get("/org/tree", org_node_id=holding_uuid)
        resp.raise_for_status()
        data = resp.json()
    except Exception as e:
        print(f"ERROR fetching org tree: {e}")
        sys.exit(1)

    os.makedirs(os.path.dirname(OUTPUT_JSON), exist_ok=True)
    with open(OUTPUT_JSON, "w") as f:
        json.dump(data, f, indent=2)
    print(f"Saved org tree JSON to {OUTPUT_JSON}")

    node_count = count_nodes_in_response(data)
    with open(OUTPUT_COUNT, "w") as f:
        f.write(f"total_nodes={node_count}\n")
    print(f"Total nodes: {node_count}")
    print(f"Saved node count to {OUTPUT_COUNT}")

if __name__ == "__main__":
    main()
