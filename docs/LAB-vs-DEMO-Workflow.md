# LAB vs DEMO Environment Workflow

## Overview

CherryAI EAM uses a dual-environment architecture to protect production data while enabling safe development and testing.

## Environment Profiles

### LAB (Development)
- **Purpose**: Safe sandbox for development, testing, and demos
- **Seeding**: Fully enabled with one-click Seed Packs
- **Protection Level**: None - free to experiment
- **Detection**: Database name contains "lab" OR `ENVIRONMENT_PROFILE=LAB`

### DEMO (Protected)
- **Purpose**: Customer demos, training, production-like testing
- **Seeding**: Blocked by default, requires explicit override
- **Protection Level**: High - requires `ALLOW_DEMO_SEED=true`
- **Detection**: Database name contains "demo" or "prod" OR `ENVIRONMENT_PROFILE=DEMO`

## Environment Variables

| Variable | Values | Description |
|----------|--------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Development, Production | Must be "Development" for any seeding |
| `ENVIRONMENT_PROFILE` | LAB, DEMO | Explicit profile override |
| `ALLOW_DEMO_SEED` | true, false | Override to allow seeding in protected environments |
| `DATABASE_URL` | Connection string | Used to detect environment from DB name |

## Seed Guard Logic

The Seed Guard blocks seeding operations unless ALL conditions pass:

1. **Environment Check**: `ASPNETCORE_ENVIRONMENT` must be "Development"
2. **Protected DB Check**: If database name contains "demo" or "prod", requires `ALLOW_DEMO_SEED=true`
3. **Admin Role Check**: User must have Admin role

### Guard Result Examples

```
✅ ALLOWED: Development + LAB database
✅ ALLOWED: Development + DEMO database + ALLOW_DEMO_SEED=true
❌ BLOCKED: Production environment (any database)
❌ BLOCKED: Development + DEMO database (no override)
```

## Seed Packs

Three pre-configured seed packs are available, all idempotent (safe to run multiple times):

### Small Manufacturer
- 1 company, 1 site, 5 locations
- 25 assets, 5 vendors
- 15 maintenance events
- Best for: Quick testing, minimal data

### Mid-Size Manufacturer
- 2 companies, 3 sites, 12 locations
- 100 assets, 8 vendors
- 75 maintenance events with work orders
- Best for: Feature testing, integration testing

### Enterprise
- 3 companies, 5 sites, 21 locations
- 321 assets, 10 vendors
- 239 maintenance events with work orders
- Best for: Performance testing, production-like demos

## Usage Guide

### Setting Up LAB Environment

1. Ensure `ASPNETCORE_ENVIRONMENT=Development`
2. Database name should contain "lab" (e.g., `eam_lab_db`)
3. Navigate to Admin > Data Import
4. Select and run desired Seed Pack

### Protecting DEMO Environment

1. Database name should contain "demo" (e.g., `eam_demo_db`)
2. Do NOT set `ALLOW_DEMO_SEED=true` unless intentional
3. Seed Guard will automatically block seeding attempts

### Emergency Seeding in DEMO

If you must seed a DEMO environment:

1. Set `ALLOW_DEMO_SEED=true` in environment variables
2. Run the desired seed operation
3. **IMMEDIATELY** remove `ALLOW_DEMO_SEED` after seeding
4. Document the action in your change log

## Verification

Use the Environment Status page (`/Admin/EnvironmentStatus`) to verify:

- Current environment profile (LAB/DEMO)
- Seed Guard status (Locked/Unlocked)
- Database connection (masked for security)
- Table row counts
- All guard check results

### API Endpoint

```
GET /Admin/EnvironmentStatus?handler=ApiStatus
```

Returns JSON with full environment and guard status.

## Best Practices

1. **Never share LAB database credentials** with non-developers
2. **Always verify environment** before running seed operations
3. **Create checkpoints** before major seeding operations
4. **Remove ALLOW_DEMO_SEED** immediately after emergency use
5. **Use Environment Status page** to confirm guard state

## Troubleshooting

### Seed Guard Shows "Locked" in LAB
- Check `ASPNETCORE_ENVIRONMENT` is "Development"
- Verify database name contains "lab"
- Try setting `ENVIRONMENT_PROFILE=LAB` explicitly

### Cannot Seed Despite Override
- Confirm `ALLOW_DEMO_SEED=true` (exact case)
- Restart application after changing environment variables
- Check Admin role is assigned to your user

### Seed Pack Creates Duplicates
- This should not happen - packs are idempotent
- Check for database connection issues
- Review application logs for upsert errors
