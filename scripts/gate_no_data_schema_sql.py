#!/usr/bin/env python3
import json, os, re, glob

OUT_JSON = "proof/runtime/logs/gate_no_data_schema_sql.json"
OUT_TXT = "proof/runtime/logs/gate_no_data_schema_sql.txt"
os.makedirs("proof/runtime/logs", exist_ok=True)

violations = []
patterns = [
    (r'DROP\s+TABLE', "DROP TABLE"),
    (r'TRUNCATE\s+TABLE', "TRUNCATE TABLE"),
    (r'DELETE\s+FROM\s+(?!.*WHERE)', "DELETE FROM without WHERE"),
    (r'ALTER\s+TABLE.*DROP\s+COLUMN', "ALTER TABLE DROP COLUMN"),
]

EXCLUDED_FILES = ["SeedData.cshtml.cs", "SeedData.cs", "DataSeeder.cs"]

scan_dirs = ["Pages", "Services", "Controllers", "Helpers"]
for d in scan_dirs:
    for fpath in glob.glob(f"{d}/**/*.cs", recursive=True):
        if any(fpath.endswith(ex) for ex in EXCLUDED_FILES):
            continue
        try:
            with open(fpath, "r") as f:
                content = f.read()
            for pat, label in patterns:
                matches = re.findall(pat, content, re.IGNORECASE)
                if matches:
                    violations.append({"file": fpath, "pattern": label, "count": len(matches)})
        except:
            pass

overall = "PASS" if len(violations) == 0 else "FAIL"
result = {"gate": "gate_no_data_schema_sql", "overall": overall, "violations": violations, "scanned_dirs": scan_dirs}

with open(OUT_JSON, "w") as f:
    json.dump(result, f, indent=2)

lines = [
    "No Dangerous SQL in Application Code",
    "=====================================",
    f"Scanned: {', '.join(scan_dirs)}",
    f"Violations: {len(violations)}",
]
for v in violations:
    lines.append(f"  [FAIL] {v['file']}: {v['pattern']} x{v['count']}")
lines.append(f"\nOVERALL: {overall}")
txt = "\n".join(lines)
with open(OUT_TXT, "w") as f:
    f.write(txt)
print(txt)
