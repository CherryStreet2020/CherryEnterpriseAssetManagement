# Support Playbook
Last updated: 2026-01-24


## Overview

This playbook provides first-response procedures for the most common issues encountered in CherryAI EAM. Use this document to quickly diagnose and resolve problems.

## What to Monitor

### Health Endpoints

| Endpoint | Purpose | Expected Response |
|----------|---------|-------------------|
| `/health` | Application health | `200 OK` with JSON status |
| `/health/ready` | Readiness probe | `200 OK` when ready |
| `/health/live` | Liveness probe | `200 OK` when alive |

### Key Metrics

| Metric | Warning Threshold | Critical Threshold |
|--------|-------------------|-------------------|
| Response time (p95) | > 500ms | > 2000ms |
| Error rate | > 1% | > 5% |
| Database connections | > 80% pool | > 95% pool |
| Memory usage | > 80% | > 95% |
| Webhook queue depth | > 100 | > 1000 |
| Dead-letter queue | > 10 | > 50 |

### Log Locations

| Log Type | Location | Retention |
|----------|----------|-----------|
| Application logs | `/var/log/cherryai/app.log` | 30 days |
| Error logs | `/var/log/cherryai/error.log` | 90 days |
| Access logs | `/var/log/cherryai/access.log` | 7 days |
| Audit logs | Database `audit_logs` table | 1 year |

### Alert Configuration

| Alert | Condition | Action |
|-------|-----------|--------|
| High error rate | > 5% for 5 min | Page on-call |
| Database slow | p95 > 1s for 10 min | Notify team |
| Webhook backup | Queue > 500 | Investigate |
| Memory pressure | > 95% for 5 min | Restart pod |

## Top 10 Failure Modes

### 1. Database Connection Exhaustion

**Symptoms:**
- Timeout errors
- "Too many connections" errors
- Slow response times

**First Response:**
```sql
-- Check connection count
SELECT count(*) FROM pg_stat_activity WHERE datname = current_database();

-- Find long-running queries
SELECT pid, now() - pg_stat_activity.query_start AS duration, query
FROM pg_stat_activity
WHERE state = 'active' AND query_start < now() - interval '30 seconds'
ORDER BY duration DESC;

-- Kill long-running query if necessary
SELECT pg_terminate_backend(pid);
```

**Resolution:**
1. Restart application to release connections
2. Identify and fix connection leak in code
3. Increase connection pool size if legitimate load

---

### 2. Webhook Delivery Failures

**Symptoms:**
- Webhook queue growing
- Integration partner not receiving events
- Retry attempts increasing

**First Response:**
```sql
-- Check webhook outbox status
SELECT status, COUNT(*) 
FROM webhook_outbox 
GROUP BY status;

-- Find failed webhooks
SELECT id, endpoint_url, status, retry_count, last_error, created_at
FROM webhook_outbox
WHERE status = 'Failed'
ORDER BY created_at DESC
LIMIT 20;
```

**Resolution:**
1. Check endpoint availability (is partner down?)
2. Verify webhook secret hasn't changed
3. Manually retry failed webhooks if endpoint recovered
4. Move to dead-letter if endpoint permanently unavailable

See [Webhook Recovery](#webhook-recovery) for detailed steps.

---

### 3. Slow Page Load / Query Performance

**Symptoms:**
- Pages taking > 5 seconds to load
- Users reporting timeouts
- High database CPU

**First Response:**
```sql
-- Find slow queries
SELECT query, calls, mean_time, total_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;

-- Check for missing indexes
SELECT schemaname, tablename, attname, n_distinct, correlation
FROM pg_stats
WHERE schemaname = 'public' AND n_distinct > 1000;
```

**Resolution:**
1. Add missing indexes for common query patterns
2. Optimize N+1 query patterns in code
3. Add pagination to large result sets
4. Consider read replicas for heavy read loads

---

### 4. Memory Pressure / OOM

**Symptoms:**
- Application restarts
- Increasing memory usage
- OOM killer messages in logs

**First Response:**
```bash
# Check memory usage
free -h
ps aux --sort=-%mem | head -10

# Check for memory leaks in .NET
dotnet-counters monitor --process-id $(pidof Abs.FixedAssets)
```

**Resolution:**
1. Restart application to free memory
2. Increase memory limits if legitimate usage
3. Profile application for memory leaks
4. Add pagination to large data operations

---

### 5. Authentication Failures

**Symptoms:**
- Users unable to login
- "Invalid token" errors
- Session expiration issues

**First Response:**
```sql
-- Check user lockouts
SELECT user_name, lockout_end, access_failed_count
FROM asp_net_users
WHERE lockout_end IS NOT NULL AND lockout_end > now();

-- Check recent login attempts
SELECT * FROM audit_logs
WHERE action = 'Login' AND created_at > now() - interval '1 hour'
ORDER BY created_at DESC;
```

**Resolution:**
1. Unlock user account if legitimate user
2. Check JWT secret hasn't changed/expired
3. Verify cookie settings for domain
4. Check for clock skew issues

---

### 6. File Upload Failures

**Symptoms:**
- Attachment uploads failing
- "File too large" errors
- Timeout during upload

**First Response:**
```bash
# Check disk space
df -h

# Check upload temp directory
ls -la /tmp/uploads

# Check nginx/proxy limits
grep client_max_body_size /etc/nginx/nginx.conf
```

**Resolution:**
1. Clear disk space if full
2. Increase upload size limits
3. Check file type restrictions
4. Verify object storage connectivity

---

### 7. Scheduled Job Failures (PM Execution)

**Symptoms:**
- Work orders not auto-generating
- PM schedules not executing
- Background job errors in logs

**First Response:**
```sql
-- Check PM schedule status
SELECT id, name, next_due_date, last_execution_date, is_active
FROM pm_schedules
WHERE is_active = true AND next_due_date < now()
ORDER BY next_due_date;

-- Check background job status
SELECT name, last_run, status, error_message
FROM background_jobs
ORDER BY last_run DESC;
```

**Resolution:**
1. Check PM execution service is running
2. Verify schedule configuration is correct
3. Manually trigger PM execution if needed
4. Check for database locks blocking execution

---

### 8. Integration ID Mapping Failures

**Symptoms:**
- Inbound webhooks failing
- "Entity not found" errors
- External system sync broken

**First Response:**
```sql
-- Check mapping status
SELECT external_system, entity_type, COUNT(*)
FROM integration_id_mappings
GROUP BY external_system, entity_type;

-- Find unmapped entities
SELECT * FROM inbound_events
WHERE status = 'Failed' AND error_message LIKE '%mapping%'
ORDER BY created_at DESC
LIMIT 10;
```

**Resolution:**
1. Create missing ID mappings
2. Verify external system is sending correct IDs
3. Check integration endpoint configuration
4. Manually process failed events after mapping

---

### 9. Report Generation Timeouts

**Symptoms:**
- Reports failing to generate
- Timeout errors on report pages
- High database load during reports

**First Response:**
```sql
-- Check for report queries
SELECT pid, query_start, state, query
FROM pg_stat_activity
WHERE query LIKE '%report%' OR query LIKE '%depreciation%';

-- Check table sizes
SELECT relname, pg_size_pretty(pg_total_relation_size(relid))
FROM pg_catalog.pg_statio_user_tables
ORDER BY pg_total_relation_size(relid) DESC
LIMIT 10;
```

**Resolution:**
1. Add report-specific indexes
2. Implement report caching
3. Schedule heavy reports off-peak
4. Consider materialized views for common reports

---

### 10. Deployment Failures

**Symptoms:**
- Application won't start after deploy
- Database migration errors
- Configuration errors

**First Response:**
```bash
# Check application logs
tail -100 /var/log/cherryai/app.log

# Check migration status
dotnet ef migrations list

# Verify environment variables
env | grep -E "DATABASE|ASPNET|JWT"
```

**Resolution:**
1. Check migration error and fix or rollback
2. Verify all required environment variables
3. Check database connectivity
4. Rollback to previous version if necessary

## Webhook Recovery

### Outbound Webhook Recovery

```sql
-- View failed webhooks
SELECT id, endpoint_url, event_type, retry_count, last_error, created_at
FROM webhook_outbox
WHERE status = 'Failed'
ORDER BY created_at DESC;

-- Retry specific webhook
UPDATE webhook_outbox
SET status = 'Pending', retry_count = 0, next_retry_at = now()
WHERE id = '<webhook_id>';

-- Retry all failed webhooks for an endpoint
UPDATE webhook_outbox
SET status = 'Pending', retry_count = 0, next_retry_at = now()
WHERE status = 'Failed' AND endpoint_url = '<endpoint_url>';
```

### Inbound Webhook Failures

```sql
-- View failed inbound events
SELECT id, event_type, external_id, status, error_message, created_at
FROM inbound_events
WHERE status = 'Failed'
ORDER BY created_at DESC;

-- Reprocess specific event
UPDATE inbound_events
SET status = 'Pending', error_message = NULL
WHERE id = '<event_id>';

-- Bulk reprocess after fix deployed
UPDATE inbound_events
SET status = 'Pending', error_message = NULL
WHERE status = 'Failed' AND event_type = '<event_type>';
```

### Dead-Letter Queue Recovery

```sql
-- View dead-letter items
SELECT id, event_type, payload, error_message, failed_at, retry_count
FROM dead_letter_queue
ORDER BY failed_at DESC;

-- Move item back to processing queue
INSERT INTO inbound_events (event_type, payload, status, created_at)
SELECT event_type, payload, 'Pending', now()
FROM dead_letter_queue
WHERE id = '<dlq_id>';

DELETE FROM dead_letter_queue WHERE id = '<dlq_id>';

-- Purge old dead-letter items (after investigation)
DELETE FROM dead_letter_queue
WHERE failed_at < now() - interval '30 days';
```

## Database Performance Troubleshooting

### Lock Investigation

```sql
-- Find blocking locks
SELECT blocked_locks.pid AS blocked_pid,
       blocked_activity.usename AS blocked_user,
       blocking_locks.pid AS blocking_pid,
       blocking_activity.usename AS blocking_user,
       blocked_activity.query AS blocked_query
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid
JOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype
    AND blocking_locks.relation = blocked_locks.relation
    AND blocking_locks.pid != blocked_locks.pid
JOIN pg_catalog.pg_stat_activity blocking_activity ON blocking_activity.pid = blocking_locks.pid
WHERE NOT blocked_locks.granted;

-- Kill blocking session (use with caution)
SELECT pg_terminate_backend(<blocking_pid>);
```

### Index Health

```sql
-- Find unused indexes
SELECT schemaname, tablename, indexname, idx_scan
FROM pg_stat_user_indexes
WHERE idx_scan = 0 AND schemaname = 'public'
ORDER BY pg_relation_size(indexrelid) DESC;

-- Find missing indexes (slow seq scans)
SELECT schemaname, tablename, seq_scan, idx_scan, 
       seq_scan - idx_scan AS diff
FROM pg_stat_user_tables
WHERE seq_scan > idx_scan
ORDER BY diff DESC
LIMIT 10;
```

### Query Analysis

```sql
-- Enable query stats (if not already)
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- Top queries by total time
SELECT query, calls, mean_time, total_time
FROM pg_stat_statements
ORDER BY total_time DESC
LIMIT 10;

-- Reset stats after optimization
SELECT pg_stat_statements_reset();
```

## Escalation Procedures

### When to Escalate

| Situation | Escalate To | Method |
|-----------|-------------|--------|
| P1 incident | Engineering lead | Phone call |
| Data loss suspected | CTO + Legal | Immediate meeting |
| Security breach | Security team | Security hotline |
| Customer data access | Privacy officer | Email + ticket |
| Extended outage (> 1 hour) | Management | Status page update |

### Escalation Contacts

| Role | Primary | Backup |
|------|---------|--------|
| On-call engineer | Pager duty | Slack #oncall |
| Engineering lead | Direct contact | Engineering channel |
| Security | security@company.com | Security Slack |
| Management | Status page | Email distribution |

## Related Documents

- [OperationsRunbook.md](OperationsRunbook.md) - Operational procedures
- [SecurityResponse.md](SecurityResponse.md) - Security incident response
- [ProductionSafety.md](ProductionSafety.md) - Production safety policies
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration procedures
- [RollbackPlaybook.md](RollbackPlaybook.md) - Rollback procedures
