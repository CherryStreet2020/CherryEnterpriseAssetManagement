# Gate 06 — SQL Schema Scan

## Command
```bash
python3 scripts/proof/scan_sql_schema.py
```

## Environment
- Repo root scanned for .cs, .sql, .cshtml, .json, .xml, .yaml files
- Excludes: .git, node_modules, bin, obj, .local, .cache, .pythonlibs, proof

## Pass Criteria
- 0 occurrences of `data.` schema prefix in SQL contexts (migrations, raw SQL strings, .sql files)
- No `CREATE SCHEMA data` or `SET search_path TO data`
- False positives excluded: System.Data.*, metadata.*, FormData.*, ViewData.*, TempData.*, etc.

## Evidence Artifacts (in proof bundle)
- `SCANS/sql_schema_scan.txt` — Full scan output with PASS/FAIL result
