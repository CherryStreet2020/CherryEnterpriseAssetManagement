# CherryAI EAM - Quality Runbook

## Proof Bundle Commands

All verification commands use `http://127.0.0.1:5000` (port 5000 only).  
Every API request includes headers: `X-Tenant-Id: default`, `X-User-Id: system@localhost`, `X-Org-Node-Id: <uuid>`.

### Gate 1: Detail Card Completeness (21 types)

```bash
python3 scripts/gate_detail_card_completeness.py
```

- Validates all 21 detail endpoint types against `config/detail_contract.json`
- Each type must return HTTP 200, >=12 header fields, >=3 sections
- Output: `artifacts/21_api_endpoints.json`, `proof/quality/after/gate_detail_card_completeness.txt`

### Gate 2: Drilldown Semantic Completeness

```bash
python3 scripts/gate_drilldown_semantic_completeness.py
```

- customer_invoice drilldown: first 200 rows have non-blank customer_name
- purchase_order drilldown: first 200 rows have non-blank vendor_name
- First 3 rows include identifiers + valid detail_refs
- Output: `proof/quality/after/gate_drilldown_semantic_completeness.txt`

### Gate 3: Org Selector and Scope UI

```bash
python3 scripts/gate_org_selector_and_scope_ui.py
```

- Org tree returns >10 nodes with all 4 hierarchy types (holding, company, site, location)
- Company-scoped and site-scoped drilldowns return filtered data
- Output: `proof/org/after/gate_org_selector_and_scope.json`, `proof/org/after/gate_org_selector_and_scope.txt`

### Gate 4: Surface Area Crawl (5000 only)

```bash
python3 scripts/gate_surface_crawl.py
```

- Crawls 27 API + 12 page endpoints on port 5000
- Output: `proof/coverage/surface_area_no_exceptions.json`

### Gate 5: Port & Schema Enforcement

```bash
python3 scripts/gate_port_and_schema_compliance.py
```

- Hard fail if any script references forbidden port
- Hard fail if any SQL references `data.` schema

### Data Volume Requirements

| Entity | Required | Actual |
|--------|----------|--------|
| PurchaseOrders | >=250 | 250 |
| CustomerInvoices | >=250 | 425 |
| Assets | >0 | 321 |
| OrgNodes | >10 | 53 |
| CipProjects | >0 | 5 |
| FiscalYears | >0 | 3 |
| FiscalPeriods | >0 | 36 |

### Rebuild Proof Bundle

```bash
# Run all gates
python3 scripts/gate_detail_card_completeness.py
python3 scripts/gate_drilldown_semantic_completeness.py
python3 scripts/gate_org_selector_and_scope_ui.py
python3 scripts/gate_surface_crawl.py

# Capture counts
psql "$DATABASE_URL" -c "SELECT ... FROM ..." > proof/quality/after/entity_counts.txt

# Create bundle zip
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BUNDLE="proof_bundle_ENTERPRISE_ORG_UI_SEMANTIC_DETAIL_${TIMESTAMP}"
mkdir -p "$BUNDLE"
cp -r proof/ "$BUNDLE/"
cp -r artifacts/ "$BUNDLE/"
cp -r config/ "$BUNDLE/"
cp RUNBOOK_QUALITY.md CHATGPT.MD "$BUNDLE/"
git diff --stat > "$BUNDLE/git_diff_stat.txt"
cd "$BUNDLE" && zip -r "../proof/${BUNDLE}.zip" . && cd ..
sha256sum "proof/${BUNDLE}.zip"
```
