#!/usr/bin/env python3
"""
CherryAI Hardcoded Data Audit Scanner
Scans codebase for hardcoded business/reference data that should live in the database.
"""

import argparse
import csv
import json
import os
import re
import sys
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import List, Optional


@dataclass
class Finding:
    file: str
    line: int
    severity: str
    rule: str
    snippet: str
    remediation: str
    end_line: Optional[int] = None


SEVERITY_ORDER = {"HIGH": 3, "MEDIUM": 2, "LOW": 1, "NONE": 0}

DEFAULT_IGNORE_DIRS = {
    "bin", "obj", "node_modules", "wwwroot/lib", ".git", ".vs",
    "Migrations", "tools", "artifacts", "audit-bundle", ".local"
}

DEFAULT_IGNORE_FILES = {
    "AppDbContextModelSnapshot.cs"
}


def load_allowlist(path: Optional[str]) -> dict:
    if not path or not os.path.exists(path):
        return {"ignore_dirs": [], "ignore_files": [], "ignore_patterns": []}
    with open(path) as f:
        return json.load(f)


def should_skip(filepath: str, allowlist: dict) -> bool:
    parts = Path(filepath).parts
    for d in DEFAULT_IGNORE_DIRS:
        if d in parts:
            return True
    for d in allowlist.get("ignore_dirs", []):
        if d in parts:
            return True
    fname = os.path.basename(filepath)
    if fname in DEFAULT_IGNORE_FILES:
        return True
    for pat in allowlist.get("ignore_files", []):
        if re.search(pat, filepath):
            return True
    return False


RAZOR_OPTION_RE = re.compile(
    r'<option\s+value="[^"]*"[^>]*>[^<]+</option>',
    re.IGNORECASE
)

OPTION_BLOCK_TRIVIAL = re.compile(
    r'<option\s+value=""[^>]*>\s*(?:--|Select|Choose|All |None|--\s*Select)',
    re.IGNORECASE
)

SELECT_LIST_ITEM_RE = re.compile(
    r'new\s+SelectListItem\s*[({]',
    re.IGNORECASE
)

INLINE_LIST_RE = re.compile(
    r'new\s*(?:List<string>|string\[\]|\[\])\s*\{[^}]*"[A-Z]',
)

INLINE_DICT_RE = re.compile(
    r'(?:new\s+Dictionary|new\s+Dictionary<string\s*,\s*string>)\s*\{',
)

HAS_DATA_RE = re.compile(r'\.HasData\s*\(', re.IGNORECASE)

SEED_PATTERN_RE = re.compile(
    r'(?:class\s+\w*(?:Seed|DbInit|DataInit)\w*|'
    r'(?:async\s+)?Task\s+(?:InitializeAsync|EnsureSeeded|SeedAsync)\s*\(|'
    r'static\s+void\s+(?:Initialize|Seed)\s*\()',
    re.IGNORECASE
)

FALLBACK_DEFAULT_RE = re.compile(
    r'\?\?\s*(?:new\s+\w+\s*\{[^}]*Name\s*=|"[A-Z][a-z])',
)

ENUM_DROPDOWN_RE = re.compile(
    r'Enum\.(?:GetValues|GetNames)\s*[(<]',
)

GUID_LITERAL_RE = re.compile(
    r'(?:Guid\.Parse|new\s+Guid)\s*\(\s*"[0-9a-f]{8}-',
    re.IGNORECASE
)

TODO_HARDCODED_RE = re.compile(
    r'(?://|/\*|<!--|@\*)\s*(?:TODO|HACK|FIXME|HARDCODED|DEMO|MOCK)',
    re.IGNORECASE
)


def scan_file(filepath: str, lines: List[str]) -> List[Finding]:
    findings = []
    ext = os.path.splitext(filepath)[1].lower()
    is_cshtml = ext == ".cshtml"
    is_cs = ext == ".cs"
    is_any = True

    consecutive_options = []

    for i, line in enumerate(lines, 1):
        stripped = line.strip()

        if is_cshtml or is_cs:
            if RAZOR_OPTION_RE.search(line) and not OPTION_BLOCK_TRIVIAL.search(line):
                if not re.search(r'@[a-zA-Z]', line) and not re.search(r'value="@', line):
                    consecutive_options.append(i)
                else:
                    if len(consecutive_options) >= 2:
                        findings.append(Finding(
                            file=filepath, line=consecutive_options[0],
                            severity="HIGH", rule="RAZOR_HARDCODED_OPTIONS",
                            snippet=f"Hardcoded <option> block ({len(consecutive_options)} options, lines {consecutive_options[0]}-{consecutive_options[-1]})",
                            remediation="Move values to LookupType/LookupValue DB tables; load via ILookupService; render from PageModel property",
                            end_line=consecutive_options[-1]
                        ))
                    consecutive_options = []
            else:
                if len(consecutive_options) >= 2:
                    sample = lines[consecutive_options[0]-1].strip()[:80]
                    findings.append(Finding(
                        file=filepath, line=consecutive_options[0],
                        severity="HIGH", rule="RAZOR_HARDCODED_OPTIONS",
                        snippet=f"Hardcoded <option> block ({len(consecutive_options)} options): {sample}",
                        remediation="Move values to LookupType/LookupValue DB tables; load via ILookupService; render from PageModel property",
                        end_line=consecutive_options[-1]
                    ))
                consecutive_options = []

        if is_cs:
            if SELECT_LIST_ITEM_RE.search(line):
                if not re.search(r'//\s*(?:DB|lookup|dynamic)', line, re.IGNORECASE):
                    context = " ".join(lines[max(0,i-3):min(len(lines),i+3)])
                    if re.search(r'(?:Text|Value)\s*=\s*"[A-Z]', context):
                        findings.append(Finding(
                            file=filepath, line=i,
                            severity="HIGH", rule="CS_HARDCODED_SELECT_LIST",
                            snippet=stripped[:120],
                            remediation="Replace with ILookupService.GetValuesAsync() mapped to SelectListItem"
                        ))

            if INLINE_LIST_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="HIGH", rule="CS_INLINE_STRING_LIST",
                    snippet=stripped[:120],
                    remediation="Move list to LookupType/LookupValue DB table; retrieve via ILookupService"
                ))

            if INLINE_DICT_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="HIGH", rule="CS_INLINE_DICT",
                    snippet=stripped[:120],
                    remediation="Move dictionary to LookupType/LookupValue DB table with Code/Name mapping"
                ))

            if HAS_DATA_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="HIGH", rule="EF_HAS_DATA_SEED",
                    snippet=stripped[:120],
                    remediation="Replace HasData with idempotent seed pipeline visible in Admin UI"
                ))

            if SEED_PATTERN_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="HIGH", rule="SEED_HELPER_PATTERN",
                    snippet=stripped[:120],
                    remediation="Ensure seed data uses LookupType/LookupValue tables; make admin-manageable"
                ))

            if FALLBACK_DEFAULT_RE.search(line):
                if not re.search(r'\?\?\s*""\s*;|\?\?\s*0|\?\?\s*false|\?\?\s*null|\?\?\s*string\.Empty', line):
                    findings.append(Finding(
                        file=filepath, line=i,
                        severity="HIGH", rule="RUNTIME_FALLBACK_DEFAULT",
                        snippet=stripped[:120],
                        remediation="Remove fallback; require DB value; fail-fast if missing"
                    ))

            if ENUM_DROPDOWN_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="HIGH", rule="ENUM_DRIVEN_DROPDOWN",
                    snippet=stripped[:120],
                    remediation="Migrate enum-driven dropdown to LookupType/LookupValue loaded via ILookupService"
                ))

            if GUID_LITERAL_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="MEDIUM", rule="MAGIC_GUID",
                    snippet=stripped[:120],
                    remediation="Replace magic GUID with DB lookup by Code or configuration"
                ))

        if is_any:
            if TODO_HARDCODED_RE.search(line):
                findings.append(Finding(
                    file=filepath, line=i,
                    severity="LOW", rule="TODO_HARDCODED_COMMENT",
                    snippet=stripped[:120],
                    remediation="Address TODO/HACK comment; remove hardcoded/demo/mock references"
                ))

    if len(consecutive_options) >= 2:
        sample = lines[consecutive_options[0]-1].strip()[:80]
        findings.append(Finding(
            file=filepath, line=consecutive_options[0],
            severity="HIGH", rule="RAZOR_HARDCODED_OPTIONS",
            snippet=f"Hardcoded <option> block ({len(consecutive_options)} options): {sample}",
            remediation="Move values to LookupType/LookupValue DB tables; load via ILookupService; render from PageModel property",
            end_line=consecutive_options[-1]
        ))

    return findings


def scan_directory(root: str, allowlist: dict) -> List[Finding]:
    all_findings = []
    extensions = {".cs", ".cshtml", ".js", ".ts", ".sql"}

    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in DEFAULT_IGNORE_DIRS]

        for fname in filenames:
            ext = os.path.splitext(fname)[1].lower()
            if ext not in extensions:
                continue

            filepath = os.path.join(dirpath, fname)
            rel_path = os.path.relpath(filepath, root)

            if should_skip(rel_path, allowlist):
                continue

            try:
                with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
                    lines = f.readlines()
                findings = scan_file(rel_path, lines)
                all_findings.extend(findings)
            except Exception as e:
                print(f"Warning: Could not scan {rel_path}: {e}", file=sys.stderr)

    return all_findings


def write_csv(findings: List[Finding], path: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["File", "Line", "EndLine", "Severity", "Rule", "Snippet", "Remediation"])
        for finding in findings:
            writer.writerow([
                finding.file, finding.line, finding.end_line or "",
                finding.severity, finding.rule, finding.snippet, finding.remediation
            ])


def write_json(findings: List[Finding], path: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump([asdict(f) for f in findings], f, indent=2)


def write_summary(findings: List[Finding], path: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)

    high = [f for f in findings if f.severity == "HIGH"]
    medium = [f for f in findings if f.severity == "MEDIUM"]
    low = [f for f in findings if f.severity == "LOW"]

    by_rule = {}
    for f in findings:
        by_rule.setdefault(f.rule, []).append(f)

    by_file = {}
    for f in findings:
        by_file.setdefault(f.file, []).append(f)

    with open(path, "w", encoding="utf-8") as out:
        out.write("# Hardcoded Data Audit Summary\n\n")
        out.write(f"**Total Findings:** {len(findings)}\n\n")
        out.write(f"| Severity | Count |\n")
        out.write(f"|----------|-------|\n")
        out.write(f"| HIGH     | {len(high)} |\n")
        out.write(f"| MEDIUM   | {len(medium)} |\n")
        out.write(f"| LOW      | {len(low)} |\n\n")

        out.write("## Findings by Rule\n\n")
        out.write("| Rule | Severity | Count |\n")
        out.write("|------|----------|-------|\n")
        for rule in sorted(by_rule.keys()):
            items = by_rule[rule]
            sev = items[0].severity
            out.write(f"| {rule} | {sev} | {len(items)} |\n")

        out.write("\n## HIGH Findings by File\n\n")
        for filepath in sorted(by_file.keys()):
            file_high = [f for f in by_file[filepath] if f.severity == "HIGH"]
            if not file_high:
                continue
            out.write(f"### {filepath} ({len(file_high)} HIGH)\n\n")
            for f in file_high:
                line_info = f"L{f.line}" + (f"-{f.end_line}" if f.end_line else "")
                out.write(f"- **{f.rule}** ({line_info}): {f.snippet}\n")
                out.write(f"  - Fix: {f.remediation}\n")
            out.write("\n")

        if medium:
            out.write("## MEDIUM Findings\n\n")
            for f in medium:
                out.write(f"- **{f.rule}** `{f.file}` L{f.line}: {f.snippet[:80]}\n")
            out.write("\n")

        if low:
            out.write("## LOW Findings\n\n")
            for f in low:
                out.write(f"- **{f.rule}** `{f.file}` L{f.line}: {f.snippet[:80]}\n")
            out.write("\n")


def main():
    parser = argparse.ArgumentParser(description="CherryAI Hardcoded Data Audit Scanner")
    parser.add_argument("--root", default=".", help="Root directory to scan")
    parser.add_argument("--out", default="artifacts/hardcoded-data", help="Output directory for artifacts")
    parser.add_argument("--allowlist", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "hardcoded_allowlist.json"), help="Path to allowlist JSON")
    parser.add_argument("--fail-on", default="HIGH", choices=["NONE", "LOW", "MEDIUM", "HIGH"],
                        help="Fail if findings at this severity or above exist")
    args = parser.parse_args()

    allowlist = load_allowlist(args.allowlist)
    findings = scan_directory(args.root, allowlist)

    ignore_patterns = allowlist.get("ignore_patterns", [])
    if ignore_patterns:
        filtered = []
        for f in findings:
            skip = False
            for ip in ignore_patterns:
                rule_match = ip.get("rule") is None or ip["rule"] == f.rule
                file_match = ip.get("file_pattern") is None or re.search(ip["file_pattern"], f.file)
                snippet_match = ip.get("snippet_pattern") is None or re.search(ip["snippet_pattern"], f.snippet)
                if rule_match and file_match and snippet_match:
                    skip = True
                    break
            if not skip:
                filtered.append(f)
        findings = filtered

    findings.sort(key=lambda f: (SEVERITY_ORDER.get(f.severity, 0) * -1, f.file, f.line))

    csv_path = os.path.join(args.out, "hardcoded-data-findings.csv")
    json_path = os.path.join(args.out, "hardcoded-data-findings.json")
    md_path = os.path.join(args.out, "hardcoded-data-summary.md")

    write_csv(findings, csv_path)
    write_json(findings, json_path)
    write_summary(findings, md_path)

    high_count = sum(1 for f in findings if f.severity == "HIGH")
    medium_count = sum(1 for f in findings if f.severity == "MEDIUM")
    low_count = sum(1 for f in findings if f.severity == "LOW")

    print(f"Scan complete: {len(findings)} findings (HIGH={high_count}, MEDIUM={medium_count}, LOW={low_count})")
    print(f"Artifacts written to: {args.out}/")

    fail_level = SEVERITY_ORDER.get(args.fail_on, 0)
    max_found = 0
    if high_count > 0:
        max_found = max(max_found, SEVERITY_ORDER["HIGH"])
    if medium_count > 0:
        max_found = max(max_found, SEVERITY_ORDER["MEDIUM"])
    if low_count > 0:
        max_found = max(max_found, SEVERITY_ORDER["LOW"])

    if fail_level > 0 and max_found >= fail_level:
        print(f"FAIL: Found findings at or above {args.fail_on} severity")
        sys.exit(1)
    else:
        print(f"PASS: No findings at or above {args.fail_on} severity")
        sys.exit(0)


if __name__ == "__main__":
    main()
