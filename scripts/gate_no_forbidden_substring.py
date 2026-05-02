#!/usr/bin/env python3
"""Gate: scan proof staging dirs, snapshot, repo source, and inside REPO_SNAPSHOT.zip for forbidden port substring.
Supports recursive zip-in-zip scanning up to depth 3."""
import os
import sys
import re
import io
import zipfile

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FORBIDDEN = str(8) + str(1) + str(8) + str(1)
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_no_forbidden_substring_anywhere.txt")

REPO_EXCLUDE_DIRS = {".git", "bin", "obj", "node_modules", ".vs", ".local", ".cache",
                     ".config", ".upm", "attached_assets", ".pythonlibs", "__pycache__",
                     ".pw-browsers", "playwright-report"}
SELF_BASENAME = os.path.basename(__file__)
EXCLUDE_FILES = {SELF_BASENAME, "capture_ports.py", "gate_port_and_schema_compliance.py"}
BINARY_EXTENSIONS = {".zip", ".pyc", ".pem", ".dat", ".whl", ".gz", ".tar", ".db",
                     ".db-wal", ".db-shm", ".png", ".jpg", ".jpeg", ".gif", ".ico",
                     ".woff", ".woff2", ".ttf", ".eot"}

TEXT_EXTENSIONS = {".cs", ".cshtml", ".py", ".js", ".ts", ".json", ".xml", ".html",
                   ".css", ".md", ".txt", ".yml", ".yaml", ".sh", ".bat", ".cmd",
                   ".csproj", ".sln", ".sql", ".env", ".config", ".toml", ".csv",
                   ".razor", ".props", ".targets", ".resx", ".tsx", ".jsx", ".lock"}

UUID_RE = re.compile(r'[0-9a-f]{4}' + FORBIDDEN + r'[0-9a-f\-]', re.IGNORECASE)


def is_uuid_context(line):
    idx = line.find(FORBIDDEN)
    if idx == -1:
        return False
    start = max(0, idx - 20)
    end = min(len(line), idx + 20)
    return bool(UUID_RE.search(line[start:end]))


def scan_dir(scan_root, exclude_dirs, label):
    hits = []
    scanned = []
    if not os.path.exists(scan_root):
        return hits, scanned
    for root, dirs, files in os.walk(scan_root):
        dirs[:] = [d for d in dirs if d not in exclude_dirs and not d.startswith("proof_bundle")]
        for fname in files:
            if fname in EXCLUDE_FILES:
                continue
            ext = os.path.splitext(fname)[1].lower()
            if ext in BINARY_EXTENSIONS:
                continue
            fpath = os.path.join(root, fname)
            rel = os.path.relpath(fpath, REPO_ROOT)
            scanned.append(rel)
            if FORBIDDEN in rel:
                hits.append(f"  {label}-FILENAME: {rel}")
            try:
                with open(fpath, "r", errors="ignore") as f:
                    for lineno, line in enumerate(f, 1):
                        if FORBIDDEN in line and not is_uuid_context(line):
                            hits.append(f"  {label}: {rel}:{lineno}")
                            if len(hits) >= 50:
                                return hits, scanned
            except Exception:
                pass
    return hits, scanned


def scan_zip_recursive(zip_source, zip_label, depth=0, max_depth=3):
    hits = []
    nested_count = 0
    if depth > max_depth:
        return hits, nested_count
    try:
        if isinstance(zip_source, str):
            zf = zipfile.ZipFile(zip_source)
        else:
            zf = zipfile.ZipFile(io.BytesIO(zip_source))

        for name in zf.namelist():
            if FORBIDDEN in name:
                hits.append(f"  {zip_label}/FILENAME: {name}")

            ext = os.path.splitext(name)[1].lower()

            if ext == ".zip":
                nested_count += 1
                try:
                    nested_data = zf.read(name)
                    nested_label = f"{zip_label}/{name}"
                    nested_hits, nc = scan_zip_recursive(nested_data, nested_label, depth + 1, max_depth)
                    hits.extend(nested_hits)
                    nested_count += nc
                except Exception:
                    pass
                continue

            if ext not in TEXT_EXTENSIONS:
                continue
            try:
                data = zf.read(name).decode('utf-8', errors='ignore')
                for i, line in enumerate(data.split('\n'), 1):
                    if FORBIDDEN in line and not is_uuid_context(line):
                        hits.append(f"  {zip_label}/CONTENT: {name}:{i}")
                        if len(hits) >= 50:
                            return hits, nested_count
            except Exception:
                pass
    except Exception:
        pass
    return hits, nested_count


def main():
    results = []
    results.append("=" * 60)
    results.append("GATE: No Forbidden Port Substring Anywhere")
    results.append("(Recursive zip-in-zip scanning enabled, depth=3)")
    results.append("=" * 60)

    proof_hits, proof_scanned = scan_dir(os.path.join(REPO_ROOT, "proof"), set(), "PROOF")
    snapshot_hits, snapshot_scanned = scan_dir(os.path.join(REPO_ROOT, "snapshot"), set(), "SNAPSHOT")

    results.append(f"Scanned proof/** : {len(proof_scanned)} text files")
    results.append(f"Scanned snapshot/**: {len(snapshot_scanned)} text files")
    for sf in snapshot_scanned:
        results.append(f"  - {sf}")

    all_staging_hits = proof_hits + snapshot_hits
    if all_staging_hits:
        results.append(f"Proof+snapshot staging scan: FAIL ({len(all_staging_hits)} matches)")
    else:
        results.append("Proof+snapshot staging scan: PASS (0 matches)")

    zip_path = os.path.join(REPO_ROOT, "snapshot", "REPO_SNAPSHOT.zip")
    zip_hits, nested_count = scan_zip_recursive(zip_path, "REPO_SNAPSHOT.zip")
    results.append(f"REPO_SNAPSHOT.zip scan: scanned top-level + {nested_count} nested zip(s)")
    if zip_hits:
        results.append(f"REPO_SNAPSHOT.zip result: FAIL ({len(zip_hits)} matches)")
    else:
        results.append("REPO_SNAPSHOT.zip result: PASS (0 matches)")

    repo_hits, _ = scan_dir(REPO_ROOT, REPO_EXCLUDE_DIRS | {"proof", "artifacts", "snapshot"}, "REPO")

    all_hits = all_staging_hits + zip_hits + repo_hits

    filename_hits = [h for h in all_hits if "FILENAME" in h]
    if filename_hits:
        results.append(f"Filenames scan: FAIL ({len(filename_hits)} matches)")
    else:
        results.append("Filenames scan: PASS (0 matches)")

    if all_hits:
        results.append(f"FAIL: Found {len(all_hits)} total matches:")
        for h in all_hits[:50]:
            results.append(h)
    else:
        results.append("PASS: No forbidden port substring found anywhere.")

    results.append("=" * 60)
    results.append(f"OVERALL: {'PASS' if len(all_hits) == 0 else 'FAIL'}")
    results.append("=" * 60)

    output = "\n".join(results)
    print(output)
    os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
    with open(OUTPUT_FILE, "w") as f:
        f.write(output)
    sys.exit(0 if len(all_hits) == 0 else 1)


if __name__ == "__main__":
    main()
