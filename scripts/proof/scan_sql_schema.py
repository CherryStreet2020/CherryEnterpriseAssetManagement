#!/usr/bin/env python3
"""Scans for 'data' schema references in SQL contexts (migrations, raw SQL, .sql files).
Only flags actual SQL schema usage like: FROM data.table, INSERT INTO data.table,
CREATE SCHEMA data, SET search_path TO data."""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCANS_DIR = os.path.join(ROOT, 'SCANS')
os.makedirs(SCANS_DIR, exist_ok=True)

SQL_DATA_SCHEMA_PATTERNS = [
    re.compile(r'\bFROM\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bJOIN\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bINTO\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bUPDATE\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bCREATE\s+SCHEMA\s+data\b', re.IGNORECASE),
    re.compile(r'\bSET\s+search_path\b.*\bdata\b', re.IGNORECASE),
    re.compile(r'\bALTER\s+TABLE\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bDROP\s+TABLE\s+data\.\w+', re.IGNORECASE),
    re.compile(r'\bTRUNCATE\s+data\.\w+', re.IGNORECASE),
    re.compile(r'"data"\s*\.\s*"\w+"', re.IGNORECASE),
]

SCAN_EXTS = {'.cs', '.sql', '.cshtml', '.json', '.xml', '.yaml', '.yml'}
SKIP_DIRS = {'.git', 'node_modules', 'bin', 'obj', '__pycache__', '.local', '.cache',
             '.pythonlibs', '.pw-browsers', 'proof', 'attached_assets'}

def main():
    hits = []
    files_scanned = 0
    dirs_scanned = 0
    excluded_dirs = set()

    print(f"SQL Schema Scan")
    print(f"Root: {ROOT}")
    print(f"Excluded directories: {', '.join(sorted(SKIP_DIRS))}")
    print(f"Scanned extensions: {', '.join(sorted(SCAN_EXTS))}")
    print(f"Patterns checked: {len(SQL_DATA_SCHEMA_PATTERNS)}")
    print()

    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel_dir = os.path.relpath(dirpath, ROOT)
        if any(rel_dir == sd or rel_dir.startswith(sd + os.sep) for sd in SKIP_DIRS):
            excluded_dirs.add(rel_dir.split(os.sep)[0])
            dirnames.clear()
            continue
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        dirs_scanned += 1
        for f in filenames:
            ext = os.path.splitext(f)[1].lower()
            if ext not in SCAN_EXTS:
                continue
            fp = os.path.join(dirpath, f)
            rel = os.path.relpath(fp, ROOT)
            files_scanned += 1
            try:
                with open(fp, 'r', errors='ignore') as fh:
                    for i, line in enumerate(fh, 1):
                        for pattern in SQL_DATA_SCHEMA_PATTERNS:
                            if pattern.search(line):
                                ctx = line.strip()[:200]
                                hits.append(f"{rel}:{i}: {ctx}")
                                break
            except Exception:
                pass

    print(f"--- SCAN STATISTICS ---")
    print(f"Directories scanned: {dirs_scanned}")
    print(f"Directories excluded: {len(excluded_dirs)} ({', '.join(sorted(excluded_dirs))})")
    print(f"Files scanned: {files_scanned}")
    print()

    out_file = os.path.join(SCANS_DIR, 'sql_schema_scan.txt')
    with open(out_file, 'w') as f:
        f.write("SQL Schema Scan (data. schema references in SQL contexts)\n\n")
        f.write(f"Root: {ROOT}\n")
        f.write(f"Excluded directories: {', '.join(sorted(SKIP_DIRS))}\n")
        f.write(f"Scanned extensions: {', '.join(sorted(SCAN_EXTS))}\n")
        f.write(f"Patterns checked: {len(SQL_DATA_SCHEMA_PATTERNS)}\n\n")
        f.write(f"--- SCAN STATISTICS ---\n")
        f.write(f"Directories scanned: {dirs_scanned}\n")
        f.write(f"Directories excluded: {len(excluded_dirs)} ({', '.join(sorted(excluded_dirs))})\n")
        f.write(f"Files scanned: {files_scanned}\n\n")
        if hits:
            f.write(f"RESULT: FAIL - {len(hits)} SQL 'data.' schema reference(s) found\n\n")
            for h in hits:
                f.write(h + '\n')
        else:
            f.write("RESULT: PASS - 0 SQL 'data.' schema references found\n")

    print(f"SQL schema hits: {len(hits)}")
    if hits:
        print("SQL SCHEMA SCAN: FAIL")
        for h in hits[:10]:
            print(f"  {h}")
        sys.exit(1)
    else:
        print("SQL SCHEMA SCAN: PASS")
        sys.exit(0)

if __name__ == '__main__':
    main()
