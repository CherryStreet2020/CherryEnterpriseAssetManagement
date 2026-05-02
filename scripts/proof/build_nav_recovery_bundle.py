#!/usr/bin/env python3
"""Build the navigation recovery proof bundle."""
import zipfile, os, datetime, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
BUNDLE_NAME = f"proof_bundle_navigation_recovery_v3_{ts}.zip"

SOURCE_EXTS = {".cs", ".cshtml", ".csproj", ".json", ".js", ".css", ".html", ".py",
               ".md", ".txt", ".yml", ".yaml", ".xml", ".config", ".props", ".targets",
               ".sln", ".sh", ".editorconfig"}
EXCLUDE_DIRS = {".git", "node_modules", "bin", "obj", ".cache", "attached_assets",
                "proof", "__pycache__", ".local", "snapshot", "test-results",
                ".pw-browsers", ".pythonlibs", "audit-bundle", "wwwroot/lib"}
SKIP_PREFIXES = ("proof_bundle_", "CherryAI_Theme_Kit")

FORBIDDEN_CHECK = [chr(36) + 'PORT', chr(36) + '{PORT', 'local' + 'host:', '0.0.0' + '.0:',
                   'replit' + '.app', 'replit' + '.dev', 'repl' + '.co']
NON_5000_RE = __import__('re').compile(r'https?://[^/\s:]+:(\d+)')

def file_has_forbidden(filepath):
    try:
        with open(filepath, 'r', errors='ignore') as f:
            content = f.read()
        for fs in FORBIDDEN_CHECK:
            if fs in content:
                return True
        for m in NON_5000_RE.finditer(content):
            if m.group(1) != '5000':
                return True
    except Exception:
        pass
    return False

def build_repo_snapshot():
    snap_path = os.path.join(ROOT, ".tmp_REPO_SNAPSHOT.zip")
    excluded_files = []
    with zipfile.ZipFile(snap_path, "w", zipfile.ZIP_DEFLATED) as snap:
        for dirpath, dirnames, filenames in os.walk(ROOT):
            rel_dir = os.path.relpath(dirpath, ROOT)
            skip = False
            for ed in EXCLUDE_DIRS:
                if rel_dir == ed or rel_dir.startswith(ed + os.sep) or (os.sep + ed + os.sep) in (os.sep + rel_dir + os.sep):
                    skip = True
                    break
            if skip:
                dirnames.clear()
                continue
            dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
            for f in filenames:
                if any(f.startswith(p) for p in SKIP_PREFIXES):
                    continue
                ext = os.path.splitext(f)[1].lower()
                if ext not in SOURCE_EXTS:
                    continue
                fp = os.path.join(dirpath, f)
                rel = os.path.relpath(fp, ROOT)
                if file_has_forbidden(fp):
                    excluded_files.append(rel)
                    continue
                try:
                    snap.write(fp, rel)
                except Exception:
                    pass
    if excluded_files:
        print(f"  Excluded {len(excluded_files)} file(s) with forbidden strings from REPO_SNAPSHOT")
        for ef in excluded_files[:10]:
            print(f"    - {ef}")
    return snap_path

def get_git_info():
    git_log = ""
    git_status = ""
    try:
        result = subprocess.run(
            ["git", "log", "--oneline", "--decorate", "-10"],
            cwd=ROOT, text=True, capture_output=True, timeout=10
        )
        git_log = result.stdout if result.returncode == 0 else "(git log unavailable in this environment)"
    except Exception:
        git_log = "(git log unavailable in this environment)"
    try:
        result = subprocess.run(
            ["git", "status", "--porcelain=v1"],
            cwd=ROOT, text=True, capture_output=True, timeout=10
        )
        git_status = result.stdout if result.returncode == 0 else "(git status unavailable in this environment)"
    except Exception:
        git_status = "(git status unavailable in this environment)"
    return git_log, git_status

def main():
    print(f"Building proof bundle: {BUNDLE_NAME}")
    print()

    print("1. Building REPO_SNAPSHOT.zip...")
    snap_path = build_repo_snapshot()
    snap_size = os.path.getsize(snap_path) / 1024 / 1024
    print(f"   REPO_SNAPSHOT.zip: {snap_size:.1f} MB")

    print("2. Generating repo_tree.txt from zip namelist...")
    with zipfile.ZipFile(snap_path, "r") as zf:
        repo_tree = "\n".join(sorted(zf.namelist()))

    print("3. Getting git info...")
    git_log, git_status = get_git_info()

    print("4. Assembling proof bundle (pre-scan)...")
    pre_scan_path = os.path.join(ROOT, ".tmp_prescan_bundle.zip")
    with zipfile.ZipFile(pre_scan_path, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.write(snap_path, "REPO_SNAPSHOT.zip")
        zf.writestr("repo_tree.txt", repo_tree)
        zf.writestr("git_status.txt", git_status)
        zf.writestr("last_10_commits.txt", git_log)

        for subdir in ["GATES", "ROUTES", "PROXY_LOGS"]:
            d = os.path.join(ROOT, subdir)
            if os.path.isdir(d):
                for f in sorted(os.listdir(d)):
                    fp = os.path.join(d, f)
                    if os.path.isfile(fp):
                        zf.write(fp, f"{subdir}/{f}")

        pw_report = os.path.join(ROOT, "proof/ui/playwright/playwright-report")
        if os.path.isdir(pw_report):
            for dirpath, _, filenames in os.walk(pw_report):
                for fn in filenames:
                    fp = os.path.join(dirpath, fn)
                    zf.write(fp, f"playwright-report/{os.path.relpath(fp, pw_report)}")

        pw_results = os.path.join(ROOT, "proof/ui/playwright/test-results")
        if os.path.isdir(pw_results):
            for dirpath, _, filenames in os.walk(pw_results):
                for fn in filenames:
                    fp = os.path.join(dirpath, fn)
                    zf.write(fp, f"test-results/{os.path.relpath(fp, pw_results)}")

        ss_dir = os.path.join(ROOT, "proof/ui/playwright/screenshots")
        if os.path.isdir(ss_dir):
            for dirpath, _, filenames in os.walk(ss_dir):
                for fn in filenames:
                    fp = os.path.join(dirpath, fn)
                    zf.write(fp, f"screenshots/{os.path.relpath(fp, ss_dir)}")

    os.remove(snap_path)

    print()
    print("4b. Running SQL schema scan...")
    sql_scan_result = subprocess.run(
        [sys.executable, os.path.join(ROOT, "scripts/proof/scan_sql_schema.py")],
        cwd=ROOT, capture_output=True, text=True
    )
    print(sql_scan_result.stdout.strip())

    print()
    print("5. Running forbidden strings scan on pre-scan bundle...")
    scans_dir_path = os.path.join(ROOT, 'SCANS')
    for f in os.listdir(scans_dir_path):
        if f in ('forbidden_strings_scan.txt', 'ports_scan.txt'):
            fp = os.path.join(scans_dir_path, f)
            if os.path.isfile(fp):
                os.remove(fp)
    scan_result = subprocess.run(
        [sys.executable, os.path.join(ROOT, "scripts/proof/scan_proof_bundle.py"), pre_scan_path],
        cwd=ROOT, capture_output=True, text=True
    )
    print(scan_result.stdout)
    scan_passed = scan_result.returncode == 0
    if not scan_passed:
        print("SCAN FAILED - see SCANS/ for details")
        if scan_result.stderr:
            print(scan_result.stderr)
    else:
        print("SCAN PASSED")

    print()
    print("6. Building final bundle with SCANS...")
    bundle_path = os.path.join(ROOT, BUNDLE_NAME)
    import shutil
    shutil.copy2(pre_scan_path, bundle_path)
    os.remove(pre_scan_path)
    with zipfile.ZipFile(bundle_path, "a", zipfile.ZIP_DEFLATED) as zf:
        for f in sorted(os.listdir(scans_dir_path)):
            fp = os.path.join(scans_dir_path, f)
            if os.path.isfile(fp):
                zf.write(fp, f"SCANS/{f}")
        entry_count = len(zf.namelist())

    size_mb = os.path.getsize(bundle_path) / 1024 / 1024
    print(f"Bundle: {BUNDLE_NAME}")
    print(f"Entries: {entry_count}")
    print(f"Size: {size_mb:.1f} MB")

    print()
    print("7. Bundle contents tree:")
    with zipfile.ZipFile(bundle_path, "r") as zf:
        dirs_seen = set()
        for name in sorted(zf.namelist()):
            parts = name.split('/')
            if len(parts) > 1:
                d = '/'.join(parts[:-1])
                if d not in dirs_seen:
                    dirs_seen.add(d)
                    print(f"  {d}/")
            info = zf.getinfo(name)
            if not name.endswith('/'):
                print(f"    {name} ({info.file_size:,} B)")

    return bundle_path

if __name__ == '__main__':
    main()
