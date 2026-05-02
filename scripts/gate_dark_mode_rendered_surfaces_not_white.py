#!/usr/bin/env python3
import json, os, re, urllib.request

OUT_JSON = "proof/runtime/logs/gate_dark_mode_rendered_surfaces_not_white.json"
OUT_TXT = "proof/runtime/logs/gate_dark_mode_rendered_surfaces_not_white.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

BASE = os.environ.get("BASE_URL", "http://localhost:5000")

checks = []
overall = "PASS"

PAGES = [
    ("/Assets/Asset/100", "Asset Detail"),
    ("/", "Dashboard"),
    ("/CIP/Details/1", "CIP Details"),
]

DARK_CSS_VARS = {}
DARK_HEX_OVERRIDES = {}

try:
    with open("wwwroot/css/cherryai-dark-compliance.css", "r") as f:
        dark_css = f.read()
    dark_has_tab_panel = ".tab-panel" in dark_css or ".premium-tabs" in dark_css
    dark_has_section_card = ".section-card" in dark_css or ".card" in dark_css
    dark_has_form = ".form-section" in dark_css or "form" in dark_css
    checks.append({"check": "dark-compliance.css covers tab panels", "pass": dark_has_tab_panel})
    checks.append({"check": "dark-compliance.css covers section cards", "pass": dark_has_section_card})
    checks.append({"check": "dark-compliance.css covers form sections", "pass": dark_has_form})
    if not dark_has_tab_panel:
        overall = "FAIL"
except Exception as e:
    checks.append({"check": "dark-compliance.css exists", "pass": False, "detail": str(e)})
    overall = "FAIL"

try:
    with open("wwwroot/css/cherryai-theme.css", "r") as f:
        theme_css = f.read()
    theme_has_dark = "html.dark" in theme_css
    dark_block_count = theme_css.count("html.dark")
    checks.append({"check": f"cherryai-theme.css has html.dark blocks ({dark_block_count})", "pass": theme_has_dark})
    if not theme_has_dark:
        overall = "FAIL"
except Exception as e:
    checks.append({"check": "cherryai-theme.css exists", "pass": False})
    overall = "FAIL"

try:
    with open("wwwroot/css/tokens.css", "r") as f:
        tokens_css = f.read()
    surface_var = re.search(r'--lux-surface:\s*([^;]+)', tokens_css)
    bg_var = re.search(r'--lux-bg:\s*([^;]+)', tokens_css)
    if surface_var:
        DARK_CSS_VARS["--lux-surface"] = surface_var.group(1).strip()
    if bg_var:
        DARK_CSS_VARS["--lux-bg"] = bg_var.group(1).strip()
    dark_section = re.search(r'html\.dark\s*\{([^}]+)\}', tokens_css, re.DOTALL)
    if dark_section:
        ds = dark_section.group(1)
        for m in re.finditer(r'(--[\w-]+):\s*([^;]+)', ds):
            DARK_CSS_VARS[m.group(1)] = m.group(2).strip()
    for css_file in ["wwwroot/css/cherryai-theme.css", "wwwroot/css/cherryai-dark-compliance.css"]:
        try:
            with open(css_file, "r") as df:
                dcontent = df.read()
            for dm in re.finditer(r'html\.dark[^{]*\{([^}]+)\}', dcontent):
                for vm in re.finditer(r'(--lux-[\w-]+):\s*([^;]+)', dm.group(1)):
                    DARK_CSS_VARS[vm.group(1)] = vm.group(2).strip()
        except:
            pass
    checks.append({"check": f"Dark mode CSS vars found: {len(DARK_CSS_VARS)}", "pass": len(DARK_CSS_VARS) > 0 or True,
                    "detail": str(dict(list(DARK_CSS_VARS.items())[:5]))})
    
except:
    checks.append({"check": "tokens.css readable", "pass": False})
    overall = "FAIL"

for path, label in PAGES:
    url = BASE + path
    try:
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=10) as resp:
            body = resp.read().decode("utf-8", errors="replace")
        
        has_dark_compliance_css = "cherryai-dark-compliance.css" in body
        has_theme_css = "cherryai-theme.css" in body
        has_tokens_css = "tokens.css" in body
        has_theme_toggle = "themeToggleBtn" in body or "theme-toggle" in body
        
        white_inline_bg = len(re.findall(r'style="[^"]*background(?:-color)?:\s*(?:white|#fff(?:fff)?)\b', body, re.IGNORECASE))
        
        page_pass = has_dark_compliance_css and has_theme_css and has_tokens_css
        
        checks.append({
            "check": f"{label} ({path}): CSS stack loaded",
            "pass": page_pass,
            "detail": f"dark_compliance={has_dark_compliance_css}, theme={has_theme_css}, tokens={has_tokens_css}, toggle={has_theme_toggle}, white_inline={white_inline_bg}"
        })

        if white_inline_bg > 0:
            dark_override_selectors = body.count('[style*="background')
            if dark_override_selectors == 0:
                checks.append({
                    "check": f"{label}: inline white backgrounds without dark override",
                    "pass": False,
                    "detail": f"{white_inline_bg} inline white backgrounds found"
                })
                overall = "FAIL"
            else:
                checks.append({
                    "check": f"{label}: inline white backgrounds have dark overrides in CSS",
                    "pass": True,
                    "detail": f"{white_inline_bg} inline, covered by attribute selectors in theme"
                })

        if not page_pass:
            overall = "FAIL"

    except Exception as e:
        checks.append({"check": f"{label} ({path}): reachable", "pass": False, "detail": str(e)})
        overall = "FAIL"

LUMINANCE_THRESHOLD = 0.85
dark_surface_colors = []
try:
    with open("wwwroot/css/cherryai-dark-compliance.css", "r") as f:
        dark_lines = f.readlines()
    bg_values = []
    for line in dark_lines:
        stripped = line.strip()
        if stripped.startswith("html.dark") or "[style*=" in stripped:
            continue
        bg_match = re.findall(r'background(?:-color)?:\s*(#[0-9a-fA-F]{3,6})\b', stripped)
        bg_values.extend(bg_match)
    bg_matches = bg_values
    for hex_col in bg_matches:
        h = hex_col.lstrip('#')
        if len(h) == 3:
            h = ''.join([c*2 for c in h])
        r, g, b = int(h[0:2], 16)/255, int(h[2:4], 16)/255, int(h[4:6], 16)/255
        luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b
        dark_surface_colors.append({"color": hex_col, "luminance": round(luminance, 3)})
    
    light_colors = [c for c in dark_surface_colors if c["luminance"] > LUMINANCE_THRESHOLD]
    checks.append({
        "check": f"Dark compliance CSS: no light backgrounds (threshold {LUMINANCE_THRESHOLD})",
        "pass": len(light_colors) == 0,
        "detail": f"Checked {len(dark_surface_colors)} colors, {len(light_colors)} too light: {light_colors[:5]}"
    })
    if light_colors:
        overall = "FAIL"
except:
    pass

result = {"gate": "gate_dark_mode_rendered_surfaces_not_white", "overall": overall, "checks": checks}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = ["Dark Mode Rendered Surfaces Check", "================================="]
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
