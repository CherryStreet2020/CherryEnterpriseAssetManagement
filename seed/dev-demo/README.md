# `seed/dev-demo/` — Tenant-specific demo data (dev-only)

Per Dean lock 2026-05-23 (`feedback_no_shortcuts_multi_tenant_lineage.md`):

> **Tenant-shaped demo data does NOT live in migrations.** Reference data
> (ISO country codes, enum value sets, US Federal holidays) is fine in
> migrations because it's the same across every tenant. Anything specific
> to a customer — their vendor names, their part numbers, their work
> centers, their recipes — belongs here instead.

## Files

| File | Origin | Replays |
|---|---|---|
| `abs-machining-receiving.sql` | PR #5c.3 quarantine of `20260519_AddAdvancedShippingNotice.cs` + `20260519_SeedOrphanStockReceipts.cs` | 13 ABS-shaped ASN headers + 29 ASN lines + 7 orphan StockReceipts |

## How these files run

A dev-only seeder pipeline (lands in PR #5c.4) replays each `.sql` file when:

1. `ASPNETCORE_ENVIRONMENT=Development`
2. **AND** the target tenant exists (e.g., the "ABS MACHINING" company is present)
3. **AND** the config flag is enabled (e.g., `Seed:DemoData:AbsMachining:Enabled=true`)

The seeder uses `DbContext.Database.ExecuteSqlRawAsync` per file. Files MUST
be idempotent (use `ON CONFLICT DO NOTHING`, `WHERE NOT EXISTS`, etc.) so
re-running on an already-seeded dev DB is safe.

## Production safety

Prod environments **never** run these files. The existing prod data (the
original rows that the now-quarantined migrations created on 2026-05-19)
is left untouched by PR #5c.3 — only the *future* fresh-install behavior
changes.

## When to add a new file

Add a `.sql` file here whenever you'd be tempted to put a hardcoded
tenant-specific INSERT into a migration. Naming convention:

```
<tenant-slug>-<domain>.sql
```

Examples that would land here in future sprints:
- `abs-machining-production.sql` — ABS work centers, routings, BOMs
- `evs-eto.sql` — EVS-specific engineer-to-order demo data
- `pwh-financials.sql` — PWH-specific chart of accounts seed
