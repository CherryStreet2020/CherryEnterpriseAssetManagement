#!/usr/bin/env python3
import json, os, re, glob

OUT_JSON = "proof/runtime/logs/gate_dark_mode_surfaces_not_white.json"
OUT_TXT = "proof/runtime/logs/gate_dark_mode_surfaces_not_white.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

WHITE_PATTERNS = [
    re.compile(r'background\s*:\s*white\b', re.IGNORECASE),
    re.compile(r'background\s*:\s*#fff\b', re.IGNORECASE),
    re.compile(r'background\s*:\s*#ffffff\b', re.IGNORECASE),
    re.compile(r'background-color\s*:\s*white\b', re.IGNORECASE),
    re.compile(r'background-color\s*:\s*#fff\b', re.IGNORECASE),
    re.compile(r'background-color\s*:\s*#ffffff\b', re.IGNORECASE),
]

EXCLUDE_PATTERNS = [
    re.compile(r'var\('),
    re.compile(r'fallback'),
    re.compile(r'/\*'),
    re.compile(r'html\.dark'),
    re.compile(r'\[style\*='),
    re.compile(r'\.dark\s'),
    re.compile(r'\.dark\['),
]

violations = []
css_files = glob.glob("wwwroot/css/**/*.css", recursive=True)

DARK_CONTEXT_RE = re.compile(r'html\.dark|\.dark\s|\.dark\[|\[style\*=')

for fpath in css_files:
    try:
        with open(fpath, "r") as f:
            file_lines = f.readlines()
        for i, line in enumerate(file_lines, 1):
            for wp in WHITE_PATTERNS:
                if wp.search(line):
                    skip = any(ep.search(line) for ep in EXCLUDE_PATTERNS)
                    if not skip:
                        context_start = max(0, i - 6)
                        context_lines = file_lines[context_start:i]
                        context_text = "".join(context_lines)
                        if DARK_CONTEXT_RE.search(context_text):
                            skip = True
                    if not skip:
                        violations.append({"file": fpath, "line": i, "content": line.strip()[:120]})
    except:
        pass

dark_compliance_exists = os.path.exists("wwwroot/css/cherryai-dark-compliance.css")
dark_compliance_size = 0
if dark_compliance_exists:
    dark_compliance_size = os.path.getsize("wwwroot/css/cherryai-dark-compliance.css")

theme_has_dark_block = False
try:
    with open("wwwroot/css/cherryai-theme.css", "r") as f:
        theme_has_dark_block = "html.dark" in f.read()
except:
    pass

overall = "PASS" if len(violations) == 0 else "FAIL"
result = {
    "gate": "gate_dark_mode_surfaces_not_white",
    "overall": overall,
    "violations_count": len(violations),
    "violations": violations[:20],
    "css_files_scanned": len(css_files),
    "dark_compliance_css_exists": dark_compliance_exists,
    "dark_compliance_css_bytes": dark_compliance_size,
    "theme_has_dark_block": theme_has_dark_block,
}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    "Dark Mode: No White Surface Backgrounds",
    "========================================",
    f"CSS files scanned: {len(css_files)}",
    f"cherryai-dark-compliance.css: {'EXISTS' if dark_compliance_exists else 'MISSING'} ({dark_compliance_size} bytes)",
    f"cherryai-theme.css html.dark block: {'YES' if theme_has_dark_block else 'NO'}",
    f"Hardcoded white backgrounds: {len(violations)}",
]
if violations:
    for v in violations[:10]:
        lines.append(f"  [WARN] {v['file']}:{v['line']} — {v['content']}")
    if len(violations) > 10:
        lines.append(f"  ... and {len(violations) - 10} more")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
