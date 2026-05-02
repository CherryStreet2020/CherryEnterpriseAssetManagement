#!/usr/bin/env python3
"""Gate: verify every logged proxy request includes required headers."""
import os
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "proxy_requests.log")
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_proxy_headers_present.txt")

REQUIRED_HEADERS = ["X-Tenant-Id", "X-User-Id", "X-Org-Node-Id"]

def main():
    results = []
    results.append("=" * 60)
    results.append("GATE: Proxy Headers Present")
    results.append("=" * 60)
    results.append(f"Parsed log file: {LOG_FILE}")

    if not os.path.exists(LOG_FILE):
        results.append(f"FAIL: Log file not found: {LOG_FILE}")
        results.append("No proxy requests have been logged yet.")
        results.append("=" * 60)
        results.append("OVERALL: FAIL")
        results.append("=" * 60)
        output = "\n".join(results)
        print(output)
        os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
        with open(OUTPUT_FILE, "w") as f:
            f.write(output)
        sys.exit(1)

    total = 0
    missing = 0
    failures = []

    with open(LOG_FILE, "r") as f:
        for lineno, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            total += 1
            missing_headers = []
            for hdr in REQUIRED_HEADERS:
                token = f"{hdr}="
                idx = line.find(token)
                if idx == -1:
                    missing_headers.append(hdr)
                else:
                    val = ""
                    after = line[idx + len(token):]
                    val = after.split("|")[0]
                    if not val or val == "":
                        missing_headers.append(f"{hdr}(empty)")
            if missing_headers:
                missing += 1
                failures.append(f"  Line {lineno}: missing {', '.join(missing_headers)}")

    passed = missing == 0 and total > 0
    results.append(f"Total requests logged: {total}")
    results.append(f"Requests with all headers: {total - missing}")
    results.append(f"Requests missing headers: {missing}")

    if failures:
        results.append("")
        results.append("Failures:")
        for f_line in failures[:50]:
            results.append(f_line)

    results.append("=" * 60)
    if total == 0:
        results.append("OVERALL: FAIL (no requests logged)")
        passed = False
    else:
        results.append(f"OVERALL: {'PASS' if passed else 'FAIL'}")
    results.append("=" * 60)

    output = "\n".join(results)
    print(output)

    os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
    with open(OUTPUT_FILE, "w") as f:
        f.write(output)

    sys.exit(0 if passed else 1)

if __name__ == "__main__":
    main()
