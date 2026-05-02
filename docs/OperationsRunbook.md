# CherryAI EAM - Operations Runbook
Last updated: 2026-01-24


## Table of Contents

1. [Deployment](#deployment)
2. [Configuration](#configuration)
3. [Secrets Management](#secrets-management)
4. [Database Migrations](#database-migrations)
5. [Backups and Recovery](#backups-and-recovery)
6. [Webhook Recovery](#webhook-recovery)
7. [Seed Behavior](#seed-behavior)
8. [Tenant Bootstrap](#tenant-bootstrap)
9. [Monitoring and Health Checks](#monitoring-and-health-checks)
10. [Troubleshooting](#troubleshooting)

---

## Deployment

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 15+ database
- Network access to database and external services

### Deployment Steps

1. **Build the application:**
   ```bash
   dotnet build --configuration Release
   ```

2. **Apply database migrations:**
   ```bash
   dotnet ef database update
   ```

3. **Run the application:**
   ```bash
   dotnet run --project Abs.FixedAssets.csproj
   ```

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `DATABASE_URL` | Yes | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | Yes | `Development`, `Staging`, or `Production` |
| `AI_INTEGRATIONS_OPENAI_API_KEY` | No | OpenAI API key for AI Assistant |

See [DeveloperGettingStarted.md](DeveloperGettingStarted.md) for complete setup.

---

## Configuration

### Application Settings

Configuration is managed through `appsettings.json` and environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=cherryai;..."
  },
  "FinancialMode": "Standalone",
  "Modules": {
    "Purchasing": true,
    "CIP": true,
    "Depreciation": true
  }
}
```

### Financial Mode

- **Standalone:** Full financial features (AP, journals, depreciation)
- **ERP Integration:** Minimal financials, integrates with external ERP

Toggle via environment or `SystemSettings` table.

---

## Secrets Management

### Secret Storage

Secrets are stored as Replit Secrets (environment variables) and never committed to source control.

| Secret | Purpose |
|--------|---------|
| `DATABASE_URL` | Production database connection |
| `AI_INTEGRATIONS_OPENAI_API_KEY` | AI Assistant API access |
| `WEBHOOK_SIGNING_SECRET` | HMAC signing for outbound webhooks |

### Rotation Procedure

1. Generate new secret value
2. Update in Replit Secrets panel
3. Restart application
4. Verify functionality
5. Invalidate old secret at source (if applicable)

See [SecurityResponse.md](SecurityResponse.md) for emergency rotation.

---

## Database Migrations

### Applying Migrations

```bash
# Check pending migrations
dotnet ef migrations list

# Apply pending migrations
dotnet ef database update

# Apply specific migration
dotnet ef database update MigrationName
```

### Rollback

```bash
# Rollback to previous migration
dotnet ef database update PreviousMigrationName
```

### Best Practices

- Always backup before migrations
- Test on staging first
- Use `MigrateAsync()` in Development/LAB for auto-apply

See [DatabaseMigrations.md](DatabaseMigrations.md) for detailed procedures.

---

## Backups and Recovery

### Database Backup

```bash
# Create backup
pg_dump $DATABASE_URL > backup_$(date +%Y%m%d_%H%M%S).sql

# Restore backup
psql $DATABASE_URL < backup_20260124_120000.sql
```

### Backup Schedule

| Environment | Frequency | Retention |
|-------------|-----------|-----------|
| Production | Daily | 30 days |
| Staging | Weekly | 7 days |

### Disaster Recovery

1. Identify failure scope (database, application, infrastructure)
2. Stop application to prevent further damage
3. Restore from most recent backup
4. Verify data integrity with smoke tests
5. Resume service

---

## Webhook Recovery

### Outbound Webhook Failures

Webhooks use an outbox pattern with automatic retry:

1. **Check Dead-Letter Queue:**
   Navigate to `/Admin/Webhooks/DeadLetter`

2. **Replay failed events:**
   ```sql
   UPDATE outbox_events 
   SET status = 'Pending', retry_count = 0 
   WHERE status = 'DeadLetter' AND event_type = 'workorder.created';
   ```

3. **Monitor recovery:**
   Check `/Admin/Webhooks/Health` for delivery success

### Inbound Webhook Recovery

1. **Check Inbound Event Queue:**
   Navigate to `/Admin/Integrations/InboundQueue`

2. **Replay failed events:**
   Use the "Retry" action button in the queue UI

See [Integrations.md](Integrations.md) for webhook architecture.

---

## Seed Behavior

### Seed Pipelines

The application uses versioned seed packages executed in order:

| Pipeline | Purpose |
|----------|---------|
| SystemReferencePipeline | Depreciation methods, conventions |
| OrganizationSetupPipeline | Org structure, companies, sites |
| FinancePipeline | Fiscal years, GL accounts |
| VendorPartsPipeline | Vendors, manufacturers |
| PartsInventoryPipeline | Items, categories |
| EAMExecutionPipeline | Work order types, failure codes |
| DemoPackV2Pipeline | Demo data for testing |

### Seed Execution

Seeds run automatically on startup when `ASPNETCORE_ENVIRONMENT=Development`.

To verify seed status:
```sql
SELECT * FROM seed_run_receipts ORDER BY executed_at_utc DESC;
```

### Re-running Seeds

Seeds are idempotent. To force re-run:
```sql
DELETE FROM seed_run_receipts WHERE package_name = 'DemoPackV2';
```

Then restart application.

See [SeedingAndDemoData.md](SeedingAndDemoData.md) for complete documentation.

---

## Tenant Bootstrap

### Single-Tenant Mode

Default configuration for on-premise deployments:

1. Database contains single tenant
2. No tenant header required
3. All data belongs to default tenant

### Multi-Tenant Mode

For SaaS deployments:

1. **Create Tenant:**
   ```sql
   INSERT INTO tenants (id, name, slug, status) 
   VALUES (gen_random_uuid(), 'Acme Corp', 'acme', 'Active');
   ```

2. **Create Company under Tenant:**
   ```sql
   INSERT INTO companies (id, tenant_id, name, code, currency_code)
   VALUES (gen_random_uuid(), '<tenant_id>', 'Acme Manufacturing', 'ACME', 'USD');
   ```

3. **Create Site:**
   ```sql
   INSERT INTO sites (id, company_id, name, code)
   VALUES (gen_random_uuid(), '<company_id>', 'Main Plant', 'MP01');
   ```

4. **Run Tenant Seeds:**
   Trigger seed pipelines for the new tenant.

See [TenancyAndSecurity.md](TenancyAndSecurity.md) for multi-tenant architecture.

---

## Monitoring and Health Checks

### Health Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health` | Basic liveness check |
| `/api/smoke/run` | Full smoke test suite |

### Key Metrics

- Webhook delivery success rate
- Background job queue depth
- Database connection pool usage
- API response times

### Log Locations

- Application logs: stdout/stderr
- Database logs: PostgreSQL log
- Webhook delivery logs: `OutboxEvents` table

---

## Troubleshooting

### Common Issues

#### Application Won't Start

1. Check database connectivity:
   ```bash
   psql $DATABASE_URL -c "SELECT 1"
   ```

2. Verify environment variables set

3. Check for migration failures in logs

#### Smoke Tests Failing

1. Run smoke tests: `/Admin/SmokeTests`
2. Check for seed data issues
3. Verify database state consistency

#### Webhooks Not Delivering

1. Check endpoint health at `/Admin/Webhooks/Health`
2. Verify signing secret matches receiver
3. Check dead-letter queue for failures

#### PM Schedules Not Generating Work Orders

1. Verify PM Template is "Released" status
2. Check schedule `NextDueDate` is in past
3. Verify `PMExecutorHostedService` is running

---

## Related Documentation

- [Deployment.md](Deployment.md) - Build pipeline and hosting
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration procedures
- [SecurityResponse.md](SecurityResponse.md) - Incident response
- [ReleaseChecklist.md](ReleaseChecklist.md) - Pre-release verification
- [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) - Testing guide
