# Documentation Coverage Map
Last updated: 2026-01-24


## Overview

This document maps every major system area to its documentation, code locations, and operational runbook references. Use this as a navigation index when onboarding, debugging, or extending the system.

## Coverage Matrix

### Core Asset Management

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Asset Lifecycle | [Architecture.md](Architecture.md) | [DataGridPremium.md](DataGridPremium.md) | `Models/Asset.cs`, `Pages/Assets/` | [OperationsRunbook.md#data-management](OperationsRunbook.md#data-management) |
| Depreciation Engine | [Architecture.md](Architecture.md) | [TaxReferenceData.md](adr/ADR-014-TaxReferenceData.md) | `Services/DepreciationService.cs`, `Models/DepreciationBook.cs` | [OperationsRunbook.md#depreciation-runs](OperationsRunbook.md#depreciation-runs) |
| Capital Improvements (CIP) | [Architecture.md](Architecture.md) | - | `Pages/CIP/`, `Models/CapitalProject.cs` | - |
| Bulk Operations | [Architecture.md](Architecture.md) | - | `Pages/BulkOperations/` | - |

### Maintenance & Work Orders

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Work Order System | [Architecture.md](Architecture.md) | - | `Pages/WorkOrders/`, `Models/WorkOrder.cs` | [SupportPlaybook.md#work-order-issues](SupportPlaybook.md#work-order-issues) |
| PM Schedules | [Architecture.md](Architecture.md) | [ADR-016-PMRevisionControl.md](adr/ADR-016-PMRevisionControl.md) | `Services/PMExecutionService.cs`, `Models/PMSchedule.cs` | [OperationsRunbook.md#pm-execution](OperationsRunbook.md#pm-execution) |
| PM Template Revisions | [ADR-016-PMRevisionControl.md](adr/ADR-016-PMRevisionControl.md) | - | `Models/PMTemplateRevision.cs` | - |
| Smart Assist Work Requests | [Architecture.md](Architecture.md) | - | `Services/SmartAssistService.cs` | - |
| Labor Tracking | [Architecture.md](Architecture.md) | - | `Models/LaborEntry.cs`, `Pages/WorkOrders/Labor.cshtml` | - |

### Inventory & Procurement

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Item Master | [Architecture.md](Architecture.md) | [ADR-018-ItemRevisionManagement.md](adr/ADR-018-ItemRevisionManagement.md) | `Models/Item.cs`, `Pages/Materials/` | - |
| Item Revisions | [ADR-018-ItemRevisionManagement.md](adr/ADR-018-ItemRevisionManagement.md) | - | `Services/ItemRevisionService.cs`, `Models/ItemRevision.cs` | - |
| Cross-Reference (MPN/VPN) | [ADR-019-ItemMasterCrossReference.md](adr/ADR-019-ItemMasterCrossReference.md) | - | `Models/ItemCrossReference.cs` | - |
| Vendor Management | [Architecture.md](Architecture.md) | - | `Models/Vendor.cs`, `Pages/Vendors/` | - |
| Purchase Orders | [Architecture.md](Architecture.md) | - | `Pages/Purchasing/` | - |
| Requisitions | [Architecture.md](Architecture.md) | - | `Pages/Admin/Requisitions.cshtml` | - |

### Integration & Webhooks

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Outbound Webhooks | [ADR-010-WebhookIntegration.md](adr/ADR-010-WebhookIntegration.md) | [OperationsRunbook.md](OperationsRunbook.md) | `Services/Webhooks/` | [OperationsRunbook.md#webhook-recovery](OperationsRunbook.md#webhook-recovery) |
| Inbound Webhooks | [ADR-012-InboundWebhooks.md](adr/ADR-012-InboundWebhooks.md) | - | `Services/Integrations/InboundEventProcessor.cs` | [SupportPlaybook.md#inbound-webhook-failures](SupportPlaybook.md#inbound-webhook-failures) |
| Integration Endpoints | [ADR-011-IntegrationEndpointsUI.md](adr/ADR-011-IntegrationEndpointsUI.md) | - | `Pages/Admin/IntegrationEndpoints.cshtml` | - |
| ID Mapping | [ADR-013-IntegrationIdMapping.md](adr/ADR-013-IntegrationIdMapping.md) | - | `Models/IntegrationIdMapping.cs` | - |
| Dead-Letter Queue | [OperationsRunbook.md](OperationsRunbook.md) | - | `Pages/Admin/InboundEventQueue.cshtml` | [SupportPlaybook.md#dead-letter-recovery](SupportPlaybook.md#dead-letter-recovery) |

### Multi-Tenant & Organization

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Tenant Resolution | [ADR-015-TenantControlPlane.md](adr/ADR-015-TenantControlPlane.md) | - | `Services/TenantResolutionService.cs` | [OperationsRunbook.md#tenant-bootstrap](OperationsRunbook.md#tenant-bootstrap) |
| Company Management | [Architecture.md](Architecture.md) | - | `Models/Company.cs`, `Pages/Admin/Companies.cshtml` | - |
| Site/Location Hierarchy | [Architecture.md](Architecture.md) | - | `Models/Site.cs`, `Models/Location.cs` | - |
| Fiscal Calendar | [Architecture.md](Architecture.md) | - | `Models/FiscalYear.cs`, `Models/FiscalPeriod.cs` | - |

### Security & Authentication

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Authentication | [Architecture.md](Architecture.md) | [SecurityResponse.md](SecurityResponse.md) | `Services/AuthService.cs` | [SecurityResponse.md#incident-response](SecurityResponse.md#incident-response) |
| RBAC (Roles) | [Architecture.md](Architecture.md) | - | `Models/ApplicationRole.cs` | - |
| Audit Trail | [Architecture.md](Architecture.md) | [SecurityResponse.md](SecurityResponse.md) | `Services/AuditService.cs` | [SecurityResponse.md#audit-logs](SecurityResponse.md#audit-logs) |
| Key Rotation | [SecurityResponse.md](SecurityResponse.md) | - | - | [SecurityResponse.md#key-rotation](SecurityResponse.md#key-rotation) |

### Data Seeding & Testing

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Seed Pipelines | [OperationsRunbook.md](OperationsRunbook.md) | [ProductionSafety.md](ProductionSafety.md) | `Services/Seeding/` | [OperationsRunbook.md#seed-behavior](OperationsRunbook.md#seed-behavior) |
| Smoke Tests | [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) | - | `Services/Testing/SmokeTestRunner.cs` | - |
| LAB Mode | [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) | [ProductionSafety.md](ProductionSafety.md) | Environment: `ENABLE_LAB_MODE` | [ProductionSafety.md#lab-mode-restrictions](ProductionSafety.md#lab-mode-restrictions) |

### UI & Frontend

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Premium DataGrid | [DataGridPremium.md](DataGridPremium.md) | [UIConformance.md](UIConformance.md) | `wwwroot/js/datagrid-premium.js`, `Pages/Shared/_DataGrid*.cshtml` | - |
| Design System | [UIConformance.md](UIConformance.md) | - | `wwwroot/css/` | - |
| Sidebar Navigation | [Architecture.md](Architecture.md) | - | `Pages/Shared/_Layout.cshtml`, `Pages/Shared/_Sidebar.cshtml` | - |
| Help Center | [Architecture.md](Architecture.md) | - | `Pages/Help/` | - |

### Deployment & Operations

| System Area | Primary Doc | Secondary Docs | Key Code Anchors | Runbook Section |
|-------------|-------------|----------------|------------------|-----------------|
| Deployment | [Deployment.md](Deployment.md) | [ReleaseChecklist.md](ReleaseChecklist.md) | `Abs.FixedAssets.csproj` | - |
| Database Migrations | [DatabaseMigrations.md](DatabaseMigrations.md) | [OperationsRunbook.md](OperationsRunbook.md) | `Migrations/` | [OperationsRunbook.md#migrations](OperationsRunbook.md#migrations) |
| Backup & Restore | [OperationsRunbook.md](OperationsRunbook.md) | [ProductionSafety.md](ProductionSafety.md) | - | [OperationsRunbook.md#backups](OperationsRunbook.md#backups) |
| CI/CD Pipeline | [Deployment.md](Deployment.md) | - | `tools/ci-verify.sh`, `tools/validate-docs-change.sh` | - |

## Quick Reference by Role

### For Developers

| Task | Start Here |
|------|------------|
| Understanding architecture | [Architecture.md](Architecture.md) |
| Setting up dev environment | [DeveloperGettingStarted.md](DeveloperGettingStarted.md) |
| Running tests | [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) |
| Adding new features | [Architecture.md](Architecture.md) → relevant ADR |
| UI/DataGrid work | [DataGridPremium.md](DataGridPremium.md), [UIConformance.md](UIConformance.md) |

### For Operations

| Task | Start Here |
|------|------------|
| Deploying to production | [Deployment.md](Deployment.md), [ReleaseChecklist.md](ReleaseChecklist.md) |
| Database migrations | [DatabaseMigrations.md](DatabaseMigrations.md) |
| Webhook troubleshooting | [OperationsRunbook.md](OperationsRunbook.md), [SupportPlaybook.md](SupportPlaybook.md) |
| Backup/restore | [OperationsRunbook.md](OperationsRunbook.md) |
| Security incidents | [SecurityResponse.md](SecurityResponse.md) |

### For Support

| Task | Start Here |
|------|------------|
| Common failure modes | [SupportPlaybook.md](SupportPlaybook.md) |
| Dead-letter recovery | [SupportPlaybook.md](SupportPlaybook.md) |
| Performance issues | [SupportPlaybook.md](SupportPlaybook.md) |
| Escalation procedures | [SupportPlaybook.md](SupportPlaybook.md) |

## ADR Index

| ADR | Topic | Status |
|-----|-------|--------|
| [ADR-001](adr/ADR-001-RazorPagesArchitecture.md) | Razor Pages Architecture | Accepted |
| [ADR-002](adr/ADR-002-PostgreSQL.md) | PostgreSQL Database | Accepted |
| [ADR-003](adr/ADR-003-TailwindCSS.md) | Tailwind-Inspired CSS | Accepted |
| [ADR-010](adr/ADR-010-WebhookIntegration.md) | Outbound Webhooks | Accepted |
| [ADR-011](adr/ADR-011-IntegrationEndpointsUI.md) | Integration Endpoints UI | Accepted |
| [ADR-012](adr/ADR-012-InboundWebhooks.md) | Inbound Webhooks | Accepted |
| [ADR-013](adr/ADR-013-IntegrationIdMapping.md) | Integration ID Mapping | Accepted |
| [ADR-014](adr/ADR-014-TaxReferenceData.md) | Historical Tax Reference Data | Accepted |
| [ADR-015](adr/ADR-015-TenantControlPlane.md) | Tenant Control Plane | Accepted |
| [ADR-016](adr/ADR-016-PMRevisionControl.md) | PM Template Revision Control | Accepted |
| [ADR-017](adr/ADR-017-SmartModeArchitecture.md) | Smart Mode Architecture | Accepted |
| [ADR-018](adr/ADR-018-ItemRevisionManagement.md) | Item Revision Management | Accepted |
| [ADR-019](adr/ADR-019-ItemMasterCrossReference.md) | Item Master Cross-Reference | Accepted |

## Related Documents

- [README.md](README.md) - Documentation index
- [Architecture.md](Architecture.md) - System architecture overview
- [OperationsRunbook.md](OperationsRunbook.md) - Operational procedures
- [SupportPlaybook.md](SupportPlaybook.md) - Support procedures
- [ProductionSafety.md](ProductionSafety.md) - Production safety policies
