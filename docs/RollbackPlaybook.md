# Rollback Playbook

## Purpose
Step-by-step guide to safely roll back code and data if a release causes issues.

---

## 1. Pre-Change Preparation

### Database Snapshot (PostgreSQL/Neon)

Before deploying, create a backup:

```bash
# Export current database state
pg_dump $DATABASE_URL > /tmp/backup_$(date +%Y%m%d_%H%M%S).sql

# Store in a safe location (outside workspace)
# Or use Neon's built-in branching/snapshot feature
```

**Where to Store:**
- Neon Dashboard: Create a branch before deployment
- Local: `/tmp/backup_YYYYMMDD_HHMMSS.sql`
- External: Upload to secure cloud storage

### Code Checkpoint
```bash
# Note current commit hash before changes
git rev-parse HEAD > /tmp/pre_deploy_commit.txt
```

---

## 2. Rollback Triggers

Roll back if any of these occur:
- Core routes return 500 errors
- Database connection failures
- Data corruption detected
- PMTA linkage broken (WOs not updating assignments)
- Idempotency violations (duplicate records)

---

## 3. Database Restore Steps

### Option A: Restore from SQL Dump
```bash
# Drop and recreate (DESTRUCTIVE - use with caution)
psql $DATABASE_URL -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
psql $DATABASE_URL < /tmp/backup_YYYYMMDD_HHMMSS.sql
```

### Option B: Neon Branch Restore
1. Go to Neon Dashboard
2. Find the pre-deployment branch
3. Restore or switch to that branch
4. Update DATABASE_URL if branch endpoint changed

---

## 4. Code Rollback Steps

### Identify Safe Checkpoint
```bash
# View recent checkpoints
git log --oneline -n 20

# Key checkpoints:
# b6b9f45 - PMTA linkage code fix
# 55dd178 - PM assignments feature
# fb8a5a43 - Documentation update
```

### Revert to Checkpoint
```bash
# Soft reset (keeps changes as unstaged)
git reset --soft <COMMIT_HASH>

# Hard reset (discards all changes - DESTRUCTIVE)
git reset --hard <COMMIT_HASH>

# Or create revert commit (safer, preserves history)
git revert <COMMIT_HASH>
```

### Restart Application
```bash
# Rebuild and restart
dotnet build
# Workflow will auto-restart
```

---

## 5. Post-Restore Checklist

### Database Verification
- [ ] Database connection successful
- [ ] `dotnet ef database update` runs without errors (if migrations exist)

### Data Integrity
```sql
-- Verify core tables exist and have data
SELECT 'Assets' as tbl, COUNT(*) FROM "Assets"
UNION ALL SELECT 'PMTemplates', COUNT(*) FROM "PMTemplates"
UNION ALL SELECT 'PMTemplateAssets', COUNT(*) FROM "PMTemplateAssets"
UNION ALL SELECT 'MaintenanceEvents', COUNT(*) FROM "MaintenanceEvents";
```

### Seed Verification
- [ ] Run System Reference Seed (idempotent - safe to run)
- [ ] Run EAM Core Masters Load (idempotent - safe to run)
- [ ] Confirm no duplicate errors

### Smoke Test Routes
- [ ] `/Maintenance` loads
- [ ] `/Maintenance/Schedules` loads
- [ ] `/Maintenance/Assignments` loads
- [ ] `/Admin/PMTemplates` loads
- [ ] `/Admin/DataImport` loads

### Core Loop Verification
- [ ] Create WO from schedule row
- [ ] Verify CustomField1 contains `PMTA:<id>`
- [ ] Complete WO
- [ ] Verify only linked assignment updated

---

## 6. Emergency Contacts

| Role | Action |
|------|--------|
| Developer | Code rollback, debugging |
| DBA | Database restore, connection issues |
| Product Owner | Approve rollback decision |

---

## 7. Post-Mortem Template

After rollback, document:
1. What failed?
2. When was it detected?
3. What was the root cause?
4. What was the fix?
5. How can we prevent this in the future?

---

## Date
2026-01-21
