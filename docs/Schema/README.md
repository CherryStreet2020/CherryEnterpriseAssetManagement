# Database Schema Documentation

This directory contains schema snapshots of the CherryAI Enterprise Asset Management PostgreSQL database.

## Files

| File | Description |
|------|-------------|
| `CurrentSchema.sql` | Complete DDL dump (tables, indexes, constraints, sequences, views, functions) |
| `SchemaMap.json` | Machine-readable JSON with tables, columns, types, and relationships |
| `README.md` | This documentation file |

**Important:** These files contain ONLY schema definitions. NO data is ever exported.

## How to Regenerate (Replit)

### Quick Method: Use the Script

From the Replit Shell, run:

```bash
./scripts/SchemaSnapshot.sh
```

This script:
- Runs `pg_dump --schema-only` with safe flags
- Generates `CurrentSchema.sql` (DDL dump)
- Generates `SchemaMap.json` (metadata JSON)
- Validates output and prints table counts

### Manual Method

If you need to regenerate files individually:

**CurrentSchema.sql:**
```bash
pg_dump "$DATABASE_URL" --schema-only --no-owner --no-privileges --if-exists --clean > docs/Schema/CurrentSchema.sql
```

**SchemaMap.json:**
```bash
psql "$DATABASE_URL" -t << 'EOF' | python3 -c "import sys, json; print(json.dumps(json.load(sys.stdin), indent=2))" > docs/Schema/SchemaMap.json
WITH tables AS (
    SELECT table_name FROM information_schema.tables
    WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
),
columns AS (
    SELECT c.table_name,
        json_agg(json_build_object(
            'name', c.column_name,
            'type', c.data_type,
            'nullable', c.is_nullable = 'YES',
            'default', c.column_default,
            'ordinal', c.ordinal_position
        ) ORDER BY c.ordinal_position) as cols
    FROM information_schema.columns c
    INNER JOIN tables t ON c.table_name = t.table_name
    WHERE c.table_schema = 'public'
    GROUP BY c.table_name
),
pk AS (
    SELECT tc.table_name,
        json_agg(kcu.column_name ORDER BY kcu.ordinal_position) as pk_columns
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu 
        ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
    WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_schema = 'public'
    GROUP BY tc.table_name
),
fk AS (
    SELECT tc.table_name,
        json_agg(json_build_object(
            'name', tc.constraint_name,
            'column', kcu.column_name,
            'references_table', ccu.table_name,
            'references_column', ccu.column_name
        )) as fk_refs
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu 
        ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
    JOIN information_schema.constraint_column_usage ccu 
        ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public'
    GROUP BY tc.table_name
)
SELECT json_build_object(
    'generated', to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
    'database', 'PostgreSQL 16',
    'environment', 'Development',
    'tables', (
        SELECT json_agg(json_build_object(
            'name', t.table_name,
            'columns', COALESCE(c.cols, '[]'::json),
            'primaryKey', COALESCE(pk.pk_columns, '[]'::json),
            'foreignKeys', COALESCE(fk.fk_refs, '[]'::json)
        ) ORDER BY t.table_name)
        FROM tables t
        LEFT JOIN columns c ON t.table_name = c.table_name
        LEFT JOIN pk ON t.table_name = pk.table_name
        LEFT JOIN fk ON t.table_name = fk.table_name
    )
);
EOF
```

## Safety Rules

| Rule | Details |
|------|---------|
| **Schema Only** | NO data is ever exported. Only DDL (CREATE TABLE, etc.) |
| **No Secrets** | Connection strings and credentials are NEVER in output files |
| **No Owner Info** | `--no-owner --no-privileges` flags strip user/role info |
| **Safe to Commit** | Both files are safe for version control |

### What the Script Does NOT Do

- Export any table data
- Log or echo `DATABASE_URL` or any secrets
- Include connection tokens or authentication info
- Modify any database state (read-only operations)

## When to Regenerate

- After applying new EF Core migrations
- Before major releases to document schema state
- When troubleshooting schema-related issues
- For schema comparison between environments
- When onboarding new team members

## Drift Detection

The smoke test "Schema Integrity → Key Columns Exist" validates that critical columns exist in the database. If migrations haven't been applied, this test fails with actionable output listing missing columns.

To verify schema integrity:
1. Navigate to **Admin → Smoke Tests**
2. Run the full test suite
3. Check the "Schema Integrity" test result

## Current Schema Statistics

- **Tables:** 116
- **Key domains:** Assets, Depreciation, Work Orders, Inventory, Purchasing, Integrations, Multi-Tenant

### Domain Coverage

| Domain | Example Tables |
|--------|----------------|
| Asset Management | Assets, AssetCategories, AssetTransfers |
| Depreciation | DepreciationBooks, DepreciationSchedules |
| Maintenance | WorkOrders, PMSchedules, MaintenanceEvents |
| Inventory | Items, ItemRevisions, VendorItemParts |
| Purchasing | PurchaseOrders, Vendors, PurchaseRequisitions |
| Integration | IntegrationEndpoints, WebhookSubscriptions |
| Multi-Tenant | Tenants, Companies, Sites, Locations |
