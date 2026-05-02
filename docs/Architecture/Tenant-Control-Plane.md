# Tenant Control Plane Architecture

## Overview

CherryAI EAM implements a flexible multi-tenancy architecture that supports both single-tenant (on-premise) and multi-tenant (SaaS) deployment models through a unified codebase.

## Organizational Hierarchy

```
Tenant (top-level isolation boundary)
  └── Company (legal/financial entity)
       └── Site (physical location)
            └── Location (building/floor/area)
                 └── Asset (equipment/machinery)
```

## Deployment Modes

### SingleTenant Mode
- Default deployment for on-premise installations
- Uses pre-configured default TenantId, CompanyId, SiteId
- No tenant headers required on requests
- Fail-safe: falls back to defaults if context unavailable

### MultiTenant Mode
- SaaS deployment with multiple customers
- Requires `X-CherryAI-Tenant` header on all requests
- Optional `X-CherryAI-Company` and `X-CherryAI-Site` headers for narrower scoping
- Fail-safe: returns HTTP 400 if tenant cannot be resolved

## TenantContext Service

The `ITenantContext` service provides deterministic tenant resolution:

```csharp
public interface ITenantContext
{
    int? TenantId { get; }
    int? CompanyId { get; }
    int? SiteId { get; }
    bool IsResolved { get; }
    string? ResolutionError { get; }
}
```

### Resolution Flow

1. Check `DeploymentMode` from configuration
2. **SingleTenant**: Load defaults from `appsettings.json`
3. **MultiTenant**: Extract from request headers
4. Validate tenant/company/site exist in database
5. Cache resolved context for request lifetime

## Scope Stamping

All services that create records MUST stamp scope from TenantContext:

| Service | Entities Stamped |
|---------|-----------------|
| SmartAssistService | WorkRequest, MaintenanceEvent, WorkOrderOperations, AuditLog |
| CloseoutService | LessonLearned, AuditLog |
| OutboxEvent creation | OutboxEvent |
| InboundWebhookService | InboundEvent, AuditLog |
| WebhookEnvelopeBuilder | Envelope.tenantId, Envelope.companyId |

## Integration Tenant Safety

### Outbound Webhooks
- WebhookEnvelopeBuilder reads tenant/company/site from TenantContext
- Envelope always includes `tenantId` in payload

### Inbound Webhooks
- IntegrationEndpoint is tenant-scoped
- IntegrationMapping lookups filter by TenantId
- Idempotency keys are tenant-scoped (same key allowed in different tenants)

## Configuration

```json
{
  "TenantSettings": {
    "DeploymentMode": "SingleTenant",
    "DefaultTenantId": 1,
    "DefaultCompanyId": 1,
    "DefaultSiteId": 1
  }
}
```

## Database Schema

### Tenant Table
```sql
CREATE TABLE "Tenants" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Code" VARCHAR(20) NOT NULL UNIQUE,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);
```

### Foreign Key Additions
- Company.TenantId -> Tenant.Id
- (Existing) Site.CompanyId -> Company.Id

## Security Considerations

1. **Tenant Isolation**: Queries must always filter by TenantId
2. **Cross-Tenant Prevention**: Integration mappings validate tenant match
3. **Audit Trail**: All AuditLogs include TenantId
4. **API Safety**: MultiTenant mode rejects requests without valid tenant header
