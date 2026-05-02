#!/usr/bin/env python3
"""Gate: assert proof/ui/playwright contains required evidence."""
import os
import sys
import glob

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI_DIR = os.path.join(REPO_ROOT, "proof", "ui", "playwright")
OUTPUT_FILE = os.path.join(REPO_ROOT, "proof", "runtime", "logs", "gate_playwright_ui_present.txt")

def main():
    results = []
    results.append("=" * 60)
    results.append("GATE: Playwright UI Evidence Present")
    results.append("=" * 60)

    checks = []

    screenshots_dir = os.path.join(UI_DIR, "screenshots")
    pngs = glob.glob(os.path.join(screenshots_dir, "*.png"))
    has_6_pngs = len(pngs) >= 6
    checks.append(("screenshots >= 6 PNG files", has_6_pngs, f"found {len(pngs)}"))
    for p in sorted(pngs):
        results.append(f"  PNG: {os.path.basename(p)} ({os.path.getsize(p)} bytes)")

    trace_path = os.path.join(UI_DIR, "trace.zip")
    trace_exists = os.path.exists(trace_path) and os.path.getsize(trace_path) > 0
    checks.append(("trace.zip exists and non-empty", trace_exists,
                    f"{'exists, ' + str(os.path.getsize(trace_path)) + ' bytes' if trace_exists else 'MISSING'}"))

    report_index = os.path.join(UI_DIR, "playwright-report", "index.html")
    report_exists = os.path.exists(report_index) and os.path.getsize(report_index) > 0
    checks.append(("playwright-report/index.html exists and non-empty", report_exists,
                    f"{'exists, ' + str(os.path.getsize(report_index)) + ' bytes' if report_exists else 'MISSING'}"))

    all_pass = all(c[1] for c in checks)
    for name, passed, detail in checks:
        status = "OK" if passed else "FAIL"
        results.append(f"  [{status}] {name}: {detail}")

    results.append("=" * 60)
    results.append(f"OVERALL: {'PASS' if all_pass else 'FAIL'}")
    results.append("=" * 60)

    output = "\n".join(results)
    print(output)
    os.makedirs(os.path.dirname(OUTPUT_FILE), exist_ok=True)
    with open(OUTPUT_FILE, "w") as f:
        f.write(output)
    sys.exit(0 if all_pass else 1)

if __name__ == "__main__":
    main()
