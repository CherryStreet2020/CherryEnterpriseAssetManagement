#!/usr/bin/env python3
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAN_DIRS = ["Pages/CIP", "Services"]
SCAN_GLOB = {".cs", ".cshtml", ".js"}

DEMO_LITERALS = [
    "Warehouse Racking",
    "CIP-2025-004",
    "Tom Harris",
    "Warehouse 1",
    "$180,000",
    "$175,000",
]

CIP_NUMBER_RE = re.compile(r'CIP-\d{4}-\d{3,}')

EXCLUDE_DIRS = {".git", "bin", "obj", "node_modules", "proof", "snapshot", ".cache", "__pycache__"}

results = []
passed = True

def should_skip(path):
    parts = path.split(os.sep)
    return any(d in EXCLUDE_DIRS for d in parts)

def scan():
    global passed
    hits = []
    for scan_dir in SCAN_DIRS:
        full_dir = os.path.join(REPO_ROOT, scan_dir)
        if not os.path.isdir(full_dir):
            continue
        for root, dirs, files in os.walk(full_dir):
            dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
            for f in files:
                _, ext = os.path.splitext(f)
                if ext.lower() not in SCAN_GLOB:
                    continue
                fp = os.path.join(root, f)
                rel = os.path.relpath(fp, REPO_ROOT)
                if should_skip(rel):
                    continue
                try:
                    with open(fp, "r", errors="ignore") as fh:
                        for i, line in enumerate(fh, 1):
                            for literal in DEMO_LITERALS:
                                if literal in line:
                                    hits.append(f"DEMO_LITERAL [{literal}] {rel}:{i}: {line.strip()[:120]}")
                            if ext.lower() == ".cshtml" and CIP_NUMBER_RE.search(line):
                                stripped = line.strip()
                                if not stripped.startswith("//") and not stripped.startswith("@*") and "seed" not in rel.lower():
                                    if "@Model." not in line and "asp-" not in line and "Razor" not in line:
                                        if '"CIP-' in line or "'CIP-" in line:
                                            hits.append(f"CIP_NUMBER_LITERAL {rel}:{i}: {stripped[:120]}")
                except Exception:
                    pass

    if hits:
        passed = False
        results.append(f"FAIL: Found {len(hits)} hardcoded demo literal(s):")
        for h in hits[:30]:
            results.append(f"  {h}")
    else:
        results.append("PASS: No hardcoded demo literals found in CIP pages or services.")

if __name__ == "__main__":
    results.append("=" * 60)
    results.append("GATE: No Hardcoded Demo Literals in CIP")
    results.append("=" * 60)
    scan()
    results.append("=" * 60)
    results.append(f"OVERALL: {'PASS' if passed else 'FAIL'}")
    results.append("=" * 60)

    output = "\n".join(results)
    print(output)

    os.makedirs(os.path.join(REPO_ROOT, "proof", "runtime", "logs"), exist_ok=True)
    with open(os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_no_hardcoded_demo_literals_cip.txt"), "w") as f:
        f.write(output)

    sys.exit(0 if passed else 1)
