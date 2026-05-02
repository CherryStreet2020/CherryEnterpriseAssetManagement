# Production Safety Policy
Last updated: 2026-01-24


## Overview

This document defines the safety policies and controls that prevent destructive operations in production environments. All team members must understand and follow these policies.

## Environment Classification

| Environment | Purpose | Safety Level |
|-------------|---------|--------------|
| Development | Local development, feature work | Low - all operations allowed |
| LAB | Integration testing, smoke tests | Medium - rollback enabled |
| Staging | Pre-production validation | High - production-like restrictions |
| Production | Live customer data | Critical - maximum restrictions |

## Demo/Seed Prevention in Production

### Seed Pipeline Controls

The seed pipelines are **strictly prohibited** from running in production. The following controls are in place:

#### Environment Variable Gate

```csharp
// Services/Seeding/SeedPipelineOrchestrator.cs
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
{
    throw new InvalidOperationException("Seed pipelines cannot run in Production");
}
```

#### Required Environment Checks

| Variable | Required Value | Effect |
|----------|----------------|--------|
| `ASPNETCORE_ENVIRONMENT` | NOT "Production" | Blocks seed execution |
| `ENABLE_DEMO_DATA` | Must NOT be set | Blocks demo data seeding |
| `ENABLE_LAB_MODE` | Must NOT be set | Blocks LAB-only features |

### LAB Mode Restrictions

LAB mode enables destructive test operations and must never be enabled in production:

| Feature | LAB Mode | Production |
|---------|----------|------------|
| Smoke test harness | Enabled | Disabled |
| Transaction rollback tests | Enabled | Disabled |
| Demo data seeding | Enabled | Disabled |
| Reset commands | Enabled | Disabled |

#### Enforcement

```bash
# Production startup check
if [ "$ASPNETCORE_ENVIRONMENT" = "Production" ] && [ -n "$ENABLE_LAB_MODE" ]; then
    echo "FATAL: ENABLE_LAB_MODE cannot be set in Production"
    exit 1
fi
```

### Admin UI Protection

The following Admin pages are hidden or disabled in production:

| Page | Development | Production |
|------|-------------|------------|
| `/Admin/SmokeTests` | Visible | Hidden |
| `/Admin/SeedData` | Visible | Hidden |
| `/Admin/ResetDatabase` | Visible | Disabled |
| `/Admin/Import` (bulk) | Visible | Requires approval |

## Required Production Environment Variables

### Mandatory Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Must be "Production" | `Production` |
| `DATABASE_URL` | Production database connection | `postgresql://...` |
| `ASPNETCORE_URLS` | Binding URLs | `http://0.0.0.0:5000` |

### Security Variables

| Variable | Description | Rotation Frequency |
|----------|-------------|-------------------|
| `JWT_SECRET` | JWT signing key | 90 days |
| `WEBHOOK_SIGNING_SECRET` | Webhook HMAC key | 90 days |
| `ENCRYPTION_KEY` | Data encryption key | Annual |

### Prohibited Variables in Production

| Variable | Why Prohibited |
|----------|----------------|
| `ENABLE_LAB_MODE` | Enables destructive operations |
| `ENABLE_DEMO_DATA` | Seeds fake data |
| `SKIP_MIGRATION_CHECK` | Bypasses migration safety |
| `DISABLE_AUTH` | Disables authentication |

## Migration Safety Workflow

### Pre-Migration Checklist

Before running any migration in production:

- [ ] Backup created and verified (see [Backup Policy](#backup-policy))
- [ ] Migration tested in staging environment
- [ ] Rollback script prepared and tested
- [ ] Maintenance window scheduled
- [ ] Stakeholders notified
- [ ] On-call engineer available

### Migration Execution

```bash
# 1. Take backup
pg_dump $DATABASE_URL > backup_$(date +%Y%m%d_%H%M%S).sql

# 2. Verify backup
pg_restore --list backup_*.sql | head -20

# 3. Run migration with timeout
timeout 300 dotnet ef database update

# 4. Verify migration
dotnet ef migrations list | tail -5

# 5. Smoke test critical paths
curl -s https://app.example.com/health | jq .
```

### Rollback Procedure

If migration fails:

1. **Stop application** - Prevent further damage
2. **Assess damage** - Check what changed
3. **Restore backup** - If necessary
4. **Apply rollback migration** - If available
5. **Verify functionality** - Test critical paths
6. **Post-mortem** - Document what happened

See [RollbackPlaybook.md](RollbackPlaybook.md) for detailed procedures.

### Dangerous Migration Patterns

**Never run these without explicit approval:**

| Pattern | Risk | Mitigation |
|---------|------|------------|
| `DROP TABLE` | Data loss | Rename first, drop after 30 days |
| `DROP COLUMN` | Data loss | Add nullable, migrate, then drop |
| `ALTER COLUMN TYPE` | Data corruption | Create new column, migrate, swap |
| `TRUNCATE` | Data loss | Never in production |
| Large `UPDATE` without `WHERE` | Corruption | Always include `WHERE` clause |

## Backup Policy

### Backup Schedule

| Backup Type | Frequency | Retention | Location |
|-------------|-----------|-----------|----------|
| Full database | Daily | 30 days | Off-site |
| Incremental | Hourly | 7 days | Primary site |
| Transaction logs | Continuous | 7 days | Primary site |
| Pre-deployment | Before each deploy | 90 days | Off-site |

### Backup Verification

**Weekly verification required:**

```bash
# 1. Download latest backup
./scripts/download-backup.sh latest

# 2. Restore to test environment
pg_restore --dbname=test_restore backup.sql

# 3. Verify data integrity
psql test_restore -c "SELECT COUNT(*) FROM assets;"
psql test_restore -c "SELECT COUNT(*) FROM work_orders;"

# 4. Run smoke tests against restored data
ASPNETCORE_ENVIRONMENT=Test dotnet run --test-mode
```

### Restore Drill Expectations

| Drill Type | Frequency | Max RTO | Documentation |
|------------|-----------|---------|---------------|
| Table restore | Monthly | 15 minutes | Log in runbook |
| Full database restore | Quarterly | 2 hours | Full report |
| Point-in-time recovery | Semi-annual | 4 hours | Full report |
| Disaster recovery | Annual | 8 hours | Full report |

**All drills must be documented with:**
- Date/time
- Personnel involved
- Actual recovery time
- Issues encountered
- Lessons learned

## Key Rotation Policy

See [SecurityResponse.md](SecurityResponse.md) for complete key rotation procedures.

### Rotation Schedule Summary

| Secret | Rotation Frequency | Notification Lead Time |
|--------|-------------------|------------------------|
| Database passwords | 90 days | 14 days |
| JWT signing keys | 90 days | 7 days |
| Webhook secrets | 90 days | 7 days |
| API keys (external) | Per vendor policy | 14 days |
| Encryption keys | Annual | 30 days |

### Emergency Rotation

If a secret is compromised:

1. **Rotate immediately** - Don't wait for scheduled rotation
2. **Revoke old secret** - Ensure it cannot be used
3. **Update all consumers** - Distribute new secret
4. **Audit access** - Review logs for unauthorized use
5. **Document incident** - Follow incident response process

## Deployment Safety

### Pre-Deployment Checklist

- [ ] All tests pass (CI green)
- [ ] Code review approved
- [ ] Security scan clean
- [ ] Performance tested
- [ ] Backup verified
- [ ] Rollback plan ready
- [ ] Monitoring alerts configured

### Deployment Windows

| Day | Allowed Windows | Prohibited Times |
|-----|-----------------|------------------|
| Mon-Thu | 9am-3pm local | After 5pm |
| Friday | 9am-12pm only | Afternoon/Evening |
| Weekend | Emergency only | Non-emergency deploys |

### Post-Deployment Verification

```bash
# 1. Health check
curl -s https://app.example.com/health

# 2. Version check
curl -s https://app.example.com/api/version

# 3. Critical path tests
./scripts/smoke-test-production.sh

# 4. Monitor error rates for 30 minutes
./scripts/monitor-errors.sh --duration=30m
```

## Incident Classification

| Severity | Description | Response Time | Escalation |
|----------|-------------|---------------|------------|
| P1 - Critical | System down, data loss | 15 minutes | Immediate |
| P2 - High | Major feature broken | 1 hour | Same day |
| P3 - Medium | Minor feature broken | 4 hours | Next day |
| P4 - Low | Cosmetic/minor issues | 24 hours | Next sprint |

See [SecurityResponse.md](SecurityResponse.md) for incident response procedures.

## Compliance Checklist

### Weekly

- [ ] Review error logs
- [ ] Verify backup completion
- [ ] Check security alerts

### Monthly

- [ ] Backup restore drill
- [ ] Access review
- [ ] Security patch assessment

### Quarterly

- [ ] Full disaster recovery drill
- [ ] Key rotation review
- [ ] Policy review and update

## Related Documents

- [OperationsRunbook.md](OperationsRunbook.md) - Operational procedures
- [SecurityResponse.md](SecurityResponse.md) - Security incident response
- [ReleaseChecklist.md](ReleaseChecklist.md) - Release verification
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration procedures
- [SupportPlaybook.md](SupportPlaybook.md) - Support procedures
