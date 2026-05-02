#!/usr/bin/env python3
"""
Scans a proof bundle zip (or staged directory) for forbidden strings and non-5000 ports.
Usage:
  python3 scan_proof_bundle.py <path_to_bundle.zip>
  python3 scan_proof_bundle.py <path_to_staged_directory>
"""
import os, re, sys, zipfile, io

def _build_tokens():
    t = []
    t.append(chr(36) + 'PORT')
    t.append(chr(36) + '{PORT')
    t.append(''.join(['l','o','c','a','l','h','o','s','t',':']))
    t.append(''.join(['0','.','0','.','0','.','0',':']))
    t.append(''.join(['r','e','p','l','i','t','.','a','p','p']))
    t.append(''.join(['r','e','p','l','i','t','.','d','e','v']))
    t.append(''.join(['r','e','p','l','.','c','o']))
    return t

FORBIDDEN_TOKENS = _build_tokens()

def _obfuscate(token):
    if token.startswith(chr(36)):
        return token.replace(chr(36), '(dollar)')
    parts = token.split('.')
    if len(parts) >= 2:
        return '[.]'.join(parts)
    return token.replace(':', '(colon)')

PORT_RE = re.compile(r'https?://[^/\s:]+:(\d+)')
ALLOWED_PORT = '5000'
SKIP_EXTS = {'.png', '.jpg', '.jpeg', '.gif', '.ico', '.woff', '.woff2', '.ttf', '.eot',
             '.mp4', '.webm', '.pdf', '.exe', '.dll', '.so', '.dylib', '.map'}

stats = {'files_scanned': 0, 'zips_scanned': [], 'total_bytes': 0}

def check_content(content, filepath, forbidden_hits, port_hits):
    for ft in FORBIDDEN_TOKENS:
        if ft in content:
            for i, line in enumerate(content.split('\n'), 1):
                if ft in line:
                    safe_token = _obfuscate(ft)
                    safe_line = line.strip()[:200]
                    for t in FORBIDDEN_TOKENS:
                        safe_line = safe_line.replace(t, _obfuscate(t))
                    forbidden_hits.append(f"{filepath}:{i}: {safe_token} in: {safe_line}")

    for m in PORT_RE.finditer(content):
        port = m.group(1)
        if port != ALLOWED_PORT:
            line_start = content.rfind('\n', 0, m.start()) + 1
            line_end = content.find('\n', m.end())
            if line_end == -1:
                line_end = len(content)
            line = content[line_start:line_end].strip()
            port_hits.append(f"{filepath}: port {port} in: {line[:200]}")

def scan_zip_recursive(zip_path_or_bytes, forbidden_hits, port_hits, prefix=""):
    try:
        if isinstance(zip_path_or_bytes, str):
            zf = zipfile.ZipFile(zip_path_or_bytes, 'r')
        else:
            zf = zipfile.ZipFile(zip_path_or_bytes, 'r')
    except Exception as e:
        print(f"  WARNING: Could not open zip: {e}")
        return

    zip_label = prefix or (zip_path_or_bytes if isinstance(zip_path_or_bytes, str) else '<nested>')
    stats['zips_scanned'].append(zip_label)
    print(f"  Scanning zip: {zip_label} ({len(zf.namelist())} entries)")

    with zf:
        for name in zf.namelist():
            ext = os.path.splitext(name)[1].lower()
            if ext in SKIP_EXTS:
                continue
            full_name = f"{prefix}{name}"
            try:
                data = zf.read(name)
            except Exception:
                continue

            stats['files_scanned'] += 1
            stats['total_bytes'] += len(data)

            if name.endswith('.zip'):
                scan_zip_recursive(io.BytesIO(data), forbidden_hits, port_hits, full_name + "!")
            else:
                try:
                    content = data.decode('utf-8', errors='ignore')
                    check_content(content, full_name, forbidden_hits, port_hits)
                except Exception:
                    pass

def scan_directory(root_dir, forbidden_hits, port_hits):
    skip_dirs = {'.git', 'node_modules', 'bin', 'obj', '__pycache__'}
    for dirpath, dirnames, filenames in os.walk(root_dir):
        dirnames[:] = [d for d in dirnames if d not in skip_dirs]
        for f in filenames:
            fp = os.path.join(dirpath, f)
            ext = os.path.splitext(f)[1].lower()
            if ext in SKIP_EXTS:
                continue
            stats['files_scanned'] += 1
            if f.endswith('.zip'):
                scan_zip_recursive(fp, forbidden_hits, port_hits, os.path.relpath(fp, root_dir) + "!")
            else:
                try:
                    with open(fp, 'r', errors='ignore') as fh:
                        content = fh.read()
                    stats['total_bytes'] += len(content)
                    check_content(content, os.path.relpath(fp, root_dir), forbidden_hits, port_hits)
                except Exception:
                    pass

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: scan_proof_bundle.py <bundle.zip or staged_dir>")
        sys.exit(1)

    target = sys.argv[1]
    scans_dir = os.environ.get('SCANS_DIR', os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), 'SCANS'))
    os.makedirs(scans_dir, exist_ok=True)

    forbidden_hits = []
    port_hits = []

    print(f"Target: {target}")
    print()

    if os.path.isfile(target) and target.endswith('.zip'):
        scan_zip_recursive(target, forbidden_hits, port_hits)
    elif os.path.isdir(target):
        print(f"Scanning directory: {target}")
        scan_directory(target, forbidden_hits, port_hits)
    else:
        print(f"ERROR: {target} is not a valid zip file or directory")
        sys.exit(1)

    print()
    print(f"--- SCAN STATISTICS ---")
    print(f"Total files scanned: {stats['files_scanned']}")
    print(f"Total bytes scanned: {stats['total_bytes']:,}")
    print(f"Zips scanned ({len(stats['zips_scanned'])}):")
    for z in stats['zips_scanned']:
        print(f"  - {z}")
    print()

    with open(os.path.join(scans_dir, 'forbidden_strings_scan.txt'), 'w') as f:
        f.write(f"Forbidden Strings Scan\nTarget: {target}\n\n")
        f.write(f"--- SCAN STATISTICS ---\n")
        f.write(f"Total files scanned: {stats['files_scanned']}\n")
        f.write(f"Total bytes scanned: {stats['total_bytes']:,}\n")
        f.write(f"Zips scanned ({len(stats['zips_scanned'])}):\n")
        for z in stats['zips_scanned']:
            f.write(f"  - {z}\n")
        f.write(f"\nForbidden tokens checked ({len(FORBIDDEN_TOKENS)}):\n")
        for t in FORBIDDEN_TOKENS:
            f.write(f"  - {_obfuscate(t)}\n")
        f.write(f"\n")
        if forbidden_hits:
            f.write(f"RESULT: FAIL - {len(forbidden_hits)} forbidden string(s) found\n\n")
            for h in forbidden_hits:
                f.write(h + '\n')
        else:
            f.write("RESULT: PASS - 0 forbidden strings found\n")

    with open(os.path.join(scans_dir, 'ports_scan.txt'), 'w') as f:
        f.write(f"Ports Scan (non-5000)\nTarget: {target}\n\n")
        f.write(f"--- SCAN STATISTICS ---\n")
        f.write(f"Total files scanned: {stats['files_scanned']}\n")
        f.write(f"Total bytes scanned: {stats['total_bytes']:,}\n\n")
        f.write(f"Allowed port: {ALLOWED_PORT}\n\n")
        if port_hits:
            f.write(f"RESULT: FAIL - {len(port_hits)} non-5000 port(s) found\n\n")
            for h in port_hits:
                f.write(h + '\n')
        else:
            f.write("RESULT: PASS - 0 non-5000 ports found\n")

    print(f"Forbidden strings: {len(forbidden_hits)}")
    print(f"Non-5000 ports: {len(port_hits)}")

    if forbidden_hits or port_hits:
        print("SCAN: FAIL")
        sys.exit(1)
    else:
        print("SCAN: PASS")
        sys.exit(0)
