# CherryAI EAM - Security Response Guide
Last updated: 2026-01-24


## Table of Contents

1. [Key Rotation Procedures](#key-rotation-procedures)
2. [Incident Response](#incident-response)
3. [Audit Log Guidance](#audit-log-guidance)
4. [Security Contacts](#security-contacts)
5. [Vulnerability Disclosure](#vulnerability-disclosure)

---

## Key Rotation Procedures

### Database Credentials

**When to rotate:** Suspected compromise, employee offboarding, annual rotation

1. **Generate new credentials** in PostgreSQL:
   ```sql
   ALTER USER app_user WITH PASSWORD 'new_secure_password';
   ```

2. **Update connection string** in Replit Secrets:
   - Navigate to Secrets panel
   - Update `DATABASE_URL` with new password

3. **Restart application:**
   - Redeploy or restart workflow

4. **Verify connectivity:**
   - Check application logs
   - Run health check: `/health`

5. **Invalidate old credentials** (if using separate rotation account)

### OpenAI API Key

**When to rotate:** Suspected compromise, billing anomalies, annual rotation

1. **Generate new key** at platform.openai.com

2. **Update in Replit Secrets:**
   - Update `AI_INTEGRATIONS_OPENAI_API_KEY`

3. **Restart application**

4. **Test AI Assistant** functionality

5. **Revoke old key** at OpenAI dashboard

### Webhook Signing Secret

**When to rotate:** Suspected compromise, integration partner offboarding

1. **Generate new secret:**
   ```bash
   openssl rand -base64 32
   ```

2. **Update in application:**
   - Update `WEBHOOK_SIGNING_SECRET` in Secrets

3. **Notify integration partners:**
   - Provide new secret via secure channel
   - Agree on cutover time

4. **Coordinate cutover:**
   - Partners update their verification
   - Restart application

5. **Verify webhook delivery** at `/Admin/Webhooks/Health`

See [OperationsRunbook.md](OperationsRunbook.md) for operational procedures.

---

## Incident Response

### Severity Levels

| Level | Description | Response Time |
|-------|-------------|---------------|
| Critical | Active data breach, system compromise | Immediate (< 1 hour) |
| High | Vulnerability with exploit potential | < 4 hours |
| Medium | Vulnerability without active exploit | < 24 hours |
| Low | Minor security improvement | Next release |

### Incident Response Steps

#### 1. Detection and Triage (0-15 minutes)

- Identify scope of incident
- Classify severity level
- Notify security contacts

#### 2. Containment (15-60 minutes)

**For system compromise:**
```bash
# Stop application to prevent further damage
# (Via Replit: Stop workflow)

# If database compromise suspected:
# Revoke external access
```

**For credential exposure:**
- Rotate affected credentials immediately (see Key Rotation)
- Check audit logs for unauthorized access

#### 3. Investigation (1-4 hours)

- Review audit logs (see Audit Log Guidance)
- Identify attack vector
- Assess data exposure
- Document timeline

#### 4. Remediation (4-24 hours)

- Apply security patches
- Close vulnerability
- Restore from clean backup if needed
- Rotate all potentially compromised credentials

#### 5. Recovery (24-48 hours)

- Restore normal operations
- Verify system integrity with smoke tests
- Monitor for recurrence

#### 6. Post-Incident (48-72 hours)

- Document incident report
- Conduct root cause analysis
- Update security procedures
- Notify affected parties (if required)

### Evidence Preservation

When investigating:

1. **Do not modify** production database directly
2. **Export audit logs** before any cleanup
3. **Take database snapshot** for forensics
4. **Preserve application logs**

See [DatabaseMigrations.md](DatabaseMigrations.md) for backup procedures.

---

## Audit Log Guidance

### Audit Log Schema

The `AuditLogs` table tracks all significant operations:

| Column | Purpose |
|--------|---------|
| `Id` | Unique identifier |
| `EntityType` | Type of entity affected |
| `EntityId` | ID of entity affected |
| `Action` | Create, Update, Delete |
| `UserId` | User who performed action |
| `Timestamp` | When action occurred |
| `OldValues` | Previous state (JSON) |
| `NewValues` | New state (JSON) |
| `IpAddress` | Client IP address |

### Common Audit Queries

**Recent authentication attempts:**
```sql
SELECT * FROM audit_logs 
WHERE entity_type = 'User' AND action = 'Login'
ORDER BY timestamp DESC 
LIMIT 100;
```

**Changes to critical entities:**
```sql
SELECT * FROM audit_logs 
WHERE entity_type IN ('User', 'Role', 'SystemSetting')
ORDER BY timestamp DESC 
LIMIT 100;
```

**Activity by specific user:**
```sql
SELECT * FROM audit_logs 
WHERE user_id = '<user_id>'
ORDER BY timestamp DESC;
```

**Changes during incident window:**
```sql
SELECT * FROM audit_logs 
WHERE timestamp BETWEEN '2026-01-24 10:00:00' AND '2026-01-24 12:00:00'
ORDER BY timestamp;
```

### Audit Log Retention

| Environment | Retention Period |
|-------------|------------------|
| Production | 2 years |
| Staging | 90 days |
| Development | 30 days |

### Audit Log Integrity

- Audit logs are append-only
- Deletion requires DBA access
- Consider external log shipping for compliance

See [TenancyAndSecurity.md](TenancyAndSecurity.md) for security architecture.

---

## Security Contacts

| Role | Contact | Escalation |
|------|---------|------------|
| Security Lead | [Contact Info] | First responder |
| DevOps | [Contact Info] | Infrastructure issues |
| Database Admin | [Contact Info] | Database access |
| Legal | [Contact Info] | Data breach notification |

### External Resources

| Resource | Purpose |
|----------|---------|
| [OpenAI Status](https://status.openai.com) | AI service status |
| [PostgreSQL Security](https://www.postgresql.org/support/security/) | Database advisories |

---

## Vulnerability Disclosure

### Responsible Disclosure

If you discover a security vulnerability:

1. **Do not** publicly disclose until fixed
2. Report via secure channel to Security Lead
3. Provide detailed reproduction steps
4. Allow 90 days for remediation

### Security Updates

Security updates are prioritized:

1. Assess vulnerability severity
2. Develop and test fix
3. Deploy to staging
4. Deploy to production
5. Document in release notes

### Dependency Vulnerabilities

Monitor for vulnerabilities in dependencies:

```bash
# Check for known vulnerabilities
dotnet list package --vulnerable
```

See [ThirdPartyDependencies.md](ThirdPartyDependencies.md) for dependency inventory.

---

## Related Documentation

- [OperationsRunbook.md](OperationsRunbook.md) - Operational procedures
- [TenancyAndSecurity.md](TenancyAndSecurity.md) - Security architecture
- [ThirdPartyDependencies.md](ThirdPartyDependencies.md) - Dependencies and licenses
- [ReleaseChecklist.md](ReleaseChecklist.md) - Release verification
