#!/usr/bin/env python3
"""Gate: verify every logged proxy request has exact required header values."""
import os
import sys
import re

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "proxy_requests.log")
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_proxy_headers_exact_values.txt")

REQUIRED = {
    "X-Tenant-Id": "default",
    "X-User-Id": "system@localhost",
}
UUID_RE = re.compile(r'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$', re.IGNORECASE)

def main():
    results = []
    results.append("=" * 60)
    results.append("GATE: Proxy Headers Exact Values")
    results.append("=" * 60)
    results.append(f"Parsed log file: {LOG_FILE}")

    if not os.path.exists(LOG_FILE):
        results.append(f"FAIL: Log file not found: {LOG_FILE}")
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
    ok_count = 0
    failures = []

    with open(LOG_FILE, "r") as f:
        for lineno, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            total += 1
            line_failures = []

            for hdr, expected in REQUIRED.items():
                token = f"{hdr}="
                idx = line.find(token)
                if idx == -1:
                    line_failures.append(f"{hdr} missing")
                else:
                    after = line[idx + len(token):]
                    val = after.split("|")[0].strip()
                    if val != expected:
                        line_failures.append(f"{hdr}={val} (expected {expected})")

            org_token = "X-Org-Node-Id="
            org_idx = line.find(org_token)
            if org_idx == -1:
                line_failures.append("X-Org-Node-Id missing")
            else:
                after = line[org_idx + len(org_token):]
                org_val = after.split("|")[0].strip()
                if not UUID_RE.match(org_val):
                    line_failures.append(f"X-Org-Node-Id={org_val} (not a UUID)")

            if line_failures:
                failures.append(f"  Line {lineno}: {', '.join(line_failures)}")
            else:
                ok_count += 1

    passed = total > 0 and len(failures) == 0
    results.append(f"Total requests: {total}")
    results.append(f"Compliant: {ok_count}")
    results.append(f"Non-compliant: {len(failures)}")

    if failures:
        results.append("")
        results.append("Failures:")
        for f_line in failures[:50]:
            results.append(f_line)

    results.append("=" * 60)
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
