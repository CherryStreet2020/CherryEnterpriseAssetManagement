# CherryAI EAM - Release Checklist
Last updated: 2026-01-24


## Pre-Release Verification

Use this checklist before every release to production.

### Code Quality

- [ ] All code changes reviewed
- [ ] No commented-out code in production paths
- [ ] No `TODO` or `FIXME` in critical paths
- [ ] No hardcoded credentials or secrets

### Testing

- [ ] **Run smoke test suite** - Navigate to `/Admin/SmokeTests` or:
  ```bash
  curl http://localhost:5000/api/smoke/run
  ```
- [ ] All smoke tests pass (0 failures)
- [ ] Rollback verification passes
- [ ] Manual testing of changed features

### Database

- [ ] **Verify migrations applied**:
  ```bash
  dotnet ef migrations list
  ```
- [ ] No pending migrations on production
- [ ] Backup taken before applying new migrations
- [ ] Migration tested on staging first
- [ ] Rollback procedure documented for new migrations

### Seed Data

- [ ] **Verify seed data** - Check seed receipts:
  ```sql
  SELECT * FROM seed_run_receipts ORDER BY executed_at_utc DESC LIMIT 5;
  ```
- [ ] Demo data intact (if applicable)
- [ ] No test fixtures in production

### Documentation

- [ ] **Update RouteRegistry.md** if routes changed
- [ ] **Update NavigationReleaseNotes.md** if navigation changed
- [ ] **Create/update ADR** if architectural decision made
- [ ] Update replit.md if major feature added
- [ ] Docs Gate tests pass

### Configuration

- [ ] Environment variables set correctly
- [ ] No development settings in production
- [ ] Database connection string verified
- [ ] API keys/secrets configured

### Security

- [ ] No sensitive data in logs
- [ ] HTTPS enforced
- [ ] Authentication working
- [ ] Authorization rules applied

---

## Release Steps

### 1. Prepare Release

```bash
# Ensure on main branch with latest
git checkout main
git pull origin main

# Verify build
dotnet build

# Run all tests
curl http://localhost:5000/api/smoke/run
```

### 2. Database Changes (if any)

```bash
# Backup production database FIRST
pg_dump $PROD_DATABASE_URL > backup_$(date +%Y%m%d).sql

# Apply migrations
dotnet ef database update
```

### 3. Deploy

```bash
# Deploy via Replit
# Click "Deploy" button in Replit UI

# Or via CLI if available
# replit deploy
```

### 4. Verify Deployment

- [ ] Application starts without errors
- [ ] Health endpoint responds: `curl https://example.com/health`
- [ ] Login works
- [ ] Key pages load (Dashboard, Assets, Work Orders)
- [ ] Smoke tests pass on production

### 5. Monitor

- [ ] Check error logs for first hour
- [ ] Verify no error rate spikes
- [ ] Confirm integrations working (webhooks, etc.)

---

## Rollback Procedure

If issues occur after deployment:

### Quick Rollback

1. **Stop current deployment**

2. **Restore previous version**
   - Via Replit: Use deployment history
   - Via Git: `git revert HEAD`

3. **Rollback database** (if migrations applied):
   ```bash
   # Restore from backup
   psql $PROD_DATABASE_URL < backup_YYYYMMDD.sql
   
   # Or rollback specific migration
   dotnet ef database update PreviousMigrationName
   ```

4. **Verify rollback**
   - Application starts
   - Health check passes
   - Core functionality works

### Post-Rollback

- [ ] Document what failed
- [ ] Create incident report
- [ ] Fix issues in development
- [ ] Retest thoroughly before retry

---

## Release Notes

After successful release:

1. **Update NavigationReleaseNotes.md** with:
   - Version number
   - Date
   - Changes summary
   - Breaking changes (if any)

2. **Notify stakeholders**:
   - New features
   - Fixed bugs
   - Known issues

---

## Emergency Contacts

| Role | Contact |
|------|---------|
| Development Lead | [Contact Info] |
| DevOps | [Contact Info] |
| Database Admin | [Contact Info] |

---

## Related Documents

- [Deployment.md](Deployment.md) - Deployment guide
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration procedures
- [RollbackPlaybook.md](RollbackPlaybook.md) - Detailed rollback steps
- [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) - Testing guide
