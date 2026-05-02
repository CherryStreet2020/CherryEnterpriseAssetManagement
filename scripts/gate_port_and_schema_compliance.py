#!/usr/bin/env python3
"""Gate: verify port 5000 only, no forbidden port, no data. schema in SQL."""
import os
import sys
import re

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FORBIDDEN_PORT = str(8) + str(1) + str(8) + str(1)
RESULTS = []

EXCLUDE_DIRS = {".git", "bin", "obj", "node_modules", ".vs", ".local", ".cache",
                ".config", ".upm", "snapshot", "proof", "artifacts", "attached_assets",
                ".pythonlibs", "__pycache__"}
SELF_BASENAME = os.path.basename(__file__)
EXCLUDE_FILES = {SELF_BASENAME, "capture_ports.py"}

def scan_for_forbidden_port():
    hits = []
    for root, dirs, files in os.walk(REPO_ROOT):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS and not d.startswith("proof_bundle")]
        for fname in files:
            if fname in EXCLUDE_FILES:
                continue
            fpath = os.path.join(root, fname)
            rel = os.path.relpath(fpath, REPO_ROOT)
            if FORBIDDEN_PORT in fname:
                hits.append(f"  {rel}: (filename contains forbidden port)")
                continue
            try:
                with open(fpath, "r", errors="ignore") as f:
                    for lineno, line in enumerate(f, 1):
                        if FORBIDDEN_PORT in line:
                            hits.append(f"  {rel}:{lineno}: {line.strip()[:120]}")
            except Exception:
                pass
    if hits:
        RESULTS.append(f"FAIL: Found {len(hits)} references to forbidden port:")
        for h in hits[:50]:
            RESULTS.append(h)
    else:
        RESULTS.append("PASS: No references to forbidden port found in repo.")
    return len(hits) == 0

def scan_for_data_schema():
    pattern = re.compile(r'\bdata\.\w+', re.IGNORECASE)
    sql_extensions = {".sql", ".py", ".cs"}
    hits = []
    for root, dirs, files in os.walk(REPO_ROOT):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
        for fname in files:
            ext = os.path.splitext(fname)[1].lower()
            if ext not in sql_extensions:
                continue
            fpath = os.path.join(root, fname)
            rel = os.path.relpath(fpath, REPO_ROOT)
            try:
                with open(fpath, "r", errors="ignore") as f:
                    for lineno, line in enumerate(f, 1):
                        if pattern.search(line) and ("SELECT" in line.upper() or "FROM" in line.upper() or "INSERT" in line.upper() or "UPDATE" in line.upper() or "CREATE" in line.upper()):
                            hits.append(f"  {rel}:{lineno}: {line.strip()[:120]}")
            except Exception:
                pass
    if hits:
        RESULTS.append(f"FAIL: Found {len(hits)} data. schema references:")
        for h in hits[:20]:
            RESULTS.append(h)
    else:
        RESULTS.append("PASS: No 'data.' schema references in SQL contexts.")
    return len(hits) == 0

def main():
    RESULTS.append("=" * 60)
    RESULTS.append("GATE: Port Compliance + No data. schema in SQL")
    RESULTS.append("=" * 60)
    p1 = scan_for_forbidden_port()
    p2 = scan_for_data_schema()
    passed = p1 and p2
    RESULTS.append("=" * 60)
    RESULTS.append(f"OVERALL: {'PASS' if passed else 'FAIL'}")
    RESULTS.append("=" * 60)
    output = "\n".join(RESULTS)
    print(output)
    out_file = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_no_forbidden_port_no_data_schema.txt")
    os.makedirs(os.path.dirname(out_file), exist_ok=True)
    with open(out_file, "w") as f:
        f.write(output)
    sys.exit(0 if passed else 1)

if __name__ == "__main__":
    main()
