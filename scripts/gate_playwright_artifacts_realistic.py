#!/usr/bin/env python3
import json, os, glob

OUT_TXT = "proof/runtime/logs/gate_playwright_artifacts_realistic.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

checks = []
overall = "PASS"

def check(name, passed, detail=""):
    global overall
    checks.append({"check": name, "pass": passed, "detail": detail})
    if not passed:
        overall = "FAIL"

light_pngs = glob.glob("proof/ui/playwright/screenshots/after/light/*.png")
dark_pngs = glob.glob("proof/ui/playwright/screenshots/after/dark/*.png")
all_pngs = light_pngs + dark_pngs

check(f"Light PNGs >= 5", len(light_pngs) >= 5, f"found {len(light_pngs)}")
check(f"Dark PNGs >= 5", len(dark_pngs) >= 5, f"found {len(dark_pngs)}")
check(f"Total PNGs >= 10", len(all_pngs) >= 10, f"found {len(all_pngs)}")

small_pngs = []
for p in all_pngs:
    size = os.path.getsize(p)
    if size < 20480:
        small_pngs.append(f"{os.path.basename(p)} ({size} bytes)")
check(f"All PNGs > 20KB", len(small_pngs) == 0, 
      f"{len(small_pngs)} too small: {small_pngs[:3]}" if small_pngs else f"all {len(all_pngs)} PNGs OK")

trace_path = "proof/ui/playwright/trace.zip"
trace_exists = os.path.exists(trace_path)
trace_size = os.path.getsize(trace_path) if trace_exists else 0
check(f"trace.zip exists and > 50KB", trace_exists and trace_size > 51200,
      f"{'exists' if trace_exists else 'MISSING'}, {trace_size} bytes ({trace_size/1024:.1f} KB)")

report_files = glob.glob("proof/ui/playwright/playwright-report/**", recursive=True)
report_files = [f for f in report_files if os.path.isfile(f)]
check(f"playwright-report >= 10 files", len(report_files) >= 10, f"found {len(report_files)} files")

has_index = os.path.exists("proof/ui/playwright/playwright-report/index.html")
check("playwright-report/index.html exists", has_index)

lines = [
    "Playwright Artifacts Sanity Check",
    "==================================",
]
for c in checks:
    status = "PASS" if c["pass"] else "FAIL"
    lines.append(f"  [{status}] {c['check']}")
    if c.get("detail"):
        lines.append(f"         {c['detail']}")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
