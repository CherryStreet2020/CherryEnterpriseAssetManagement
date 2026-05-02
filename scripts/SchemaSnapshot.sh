#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="$PROJECT_ROOT/docs/Schema"

echo "=== CherryAI Schema Snapshot Generator ==="
echo "Output directory: $OUTPUT_DIR"

if [ -z "${DATABASE_URL:-}" ]; then
    echo "ERROR: DATABASE_URL environment variable is not set."
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

TIMESTAMP=$(date -u +"%Y-%m-%d %H:%M:%S UTC")
PG_VERSION=$(psql "$DATABASE_URL" -t -c "SELECT version();" 2>/dev/null | head -1 | sed 's/^[[:space:]]*//' | cut -d' ' -f1-2)

echo ""
echo "Step 1/2: Generating CurrentSchema.sql via pg_dump..."

{
    echo "-- CherryAI Enterprise Asset Management - Database Schema Snapshot"
    echo "-- Generated: $TIMESTAMP"
    echo "-- Environment: Development (Replit)"
    echo "-- Method: pg_dump --schema-only"
    echo "-- $PG_VERSION"
    echo ""
    echo "-- NOTE: This is schema-only. NO DATA is included."
    echo "-- This file is safe to commit to version control."
    echo ""
    pg_dump "$DATABASE_URL" --schema-only --no-owner --no-privileges --if-exists --clean 2>/dev/null
} | grep -v '^\connect' | grep -v '^\\restrict' > "$OUTPUT_DIR/CurrentSchema.sql"

TABLE_COUNT=$(grep -c "CREATE TABLE" "$OUTPUT_DIR/CurrentSchema.sql" || echo "0")
echo "  -> Created CurrentSchema.sql ($TABLE_COUNT tables)"

echo ""
echo "Step 2/2: Generating SchemaMap.json from information_schema..."

psql "$DATABASE_URL" -t 2>/dev/null << 'QUERY_EOF' | python3 -c "import sys, json; data = json.load(sys.stdin); print(json.dumps(data, indent=2))" > "$OUTPUT_DIR/SchemaMap.json"
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
QUERY_EOF

JSON_TABLE_COUNT=$(python3 -c "import json; print(len(json.load(open('$OUTPUT_DIR/SchemaMap.json'))['tables']))")
echo "  -> Created SchemaMap.json ($JSON_TABLE_COUNT tables)"

echo ""
echo "=== Schema Snapshot Complete ==="
echo "Files generated:"
ls -lh "$OUTPUT_DIR"/*.sql "$OUTPUT_DIR"/*.json 2>/dev/null | awk '{print "  " $NF " (" $5 ")"}'
echo ""
echo "Safety checks:"
echo "  - No data exported (schema-only)"
echo "  - No connection strings in output"
echo "  - No owner/privilege information"
