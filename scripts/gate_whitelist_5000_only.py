#!/usr/bin/env python3
"""Gate: every logged HTTP request starts with http://127.0.0.1:5000/api/v1/"""
import os
import sys
import re

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "proxy_requests.log")
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_whitelist_5000_only_and_api_v1_only.txt")

ALLOWED_PREFIX = "http://127.0.0.1:5000/api/v1/"

def main():
    results = []
    results.append("=" * 60)
    results.append("GATE: Whitelist 5000 Only + API v1 Only")
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
    compliant = 0
    violations = []

    url_pattern = re.compile(r'\| \w+ (http://[^\s|]+)')

    with open(LOG_FILE, "r") as f:
        for lineno, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            total += 1
            m = url_pattern.search(line)
            if m:
                url = m.group(1)
                if url.startswith(ALLOWED_PREFIX):
                    compliant += 1
                else:
                    violations.append(f"  Line {lineno}: {url}")
            else:
                violations.append(f"  Line {lineno}: could not parse URL")

    passed = total > 0 and len(violations) == 0
    results.append(f"Total requests: {total}")
    results.append(f"Compliant (5000 + /api/v1/): {compliant}")
    results.append(f"Violations: {len(violations)}")

    if violations:
        results.append("")
        results.append("Violations:")
        for v in violations[:50]:
            results.append(v)

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
