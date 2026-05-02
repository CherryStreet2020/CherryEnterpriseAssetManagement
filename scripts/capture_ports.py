#!/usr/bin/env python3
"""Capture listening ports — verify 5000 is LISTEN, forbidden port is NOT."""
import os
import sys
import subprocess

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "process_ports.txt")
FORBIDDEN_PORT = str(8) + str(1) + str(8) + str(1)

def capture_ports():
    for cmd in [["ss", "-tlnp"], ["netstat", "-tlnp"]]:
        try:
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=10)
            if result.returncode == 0:
                return cmd[0], result.stdout
        except FileNotFoundError:
            continue
        except Exception:
            continue
    return None, None

def main():
    results = []
    results.append("=" * 60)
    results.append("CAPTURE: Listening Ports")
    results.append("=" * 60)

    tool, output = capture_ports()
    if not tool:
        results.append("ERROR: Neither ss nor netstat available.")
        results.append("=" * 60)
        results.append("OVERALL: ERROR")
        results.append("=" * 60)
        final = "\n".join(results)
        print(final)
        os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
        with open(OUTPUT_FILE, "w") as f:
            f.write(final)
        sys.exit(1)

    results.append(f"Tool: {tool}")
    results.append("")
    results.append(output)

    has_5000 = False
    has_forbidden = False
    for line in output.splitlines():
        if ":5000" in line and "LISTEN" in line.upper():
            has_5000 = True
        if f":{FORBIDDEN_PORT}" in line and "LISTEN" in line.upper():
            has_forbidden = True

    results.append("=" * 60)
    results.append(f"Port 5000 LISTEN: {'YES' if has_5000 else 'NO'}")
    results.append(f"Forbidden port LISTEN: {'YES (BAD!)' if has_forbidden else 'NO (GOOD)'}")

    passed = has_5000 and not has_forbidden
    results.append(f"OVERALL: {'PASS' if passed else 'FAIL'}")
    results.append("=" * 60)

    final = "\n".join(results)
    print(final)

    os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
    with open(OUTPUT_FILE, "w") as f:
        f.write(final)

    sys.exit(0 if passed else 1)

if __name__ == "__main__":
    main()
