# Gate 05 — Forbidden Strings & Ports Scan

## Command
```bash
python3 scripts/proof/scan_proof_bundle.py <bundle.zip>
```

## Environment
- Scans the final proof bundle zip (including nested zips like REPO_SNAPSHOT.zip and trace.zip)

## Pass Criteria
- 0 forbidden strings found (see scan script for token list)
- 0 non-5000 ports found in http(s) URLs
- Exit code 0

## Tokens Checked
Dollar-PORT variable references, host-with-colon patterns, and external domain leakage.

## Evidence
- `SCANS/forbidden_strings_scan.txt`
- `SCANS/ports_scan.txt`
