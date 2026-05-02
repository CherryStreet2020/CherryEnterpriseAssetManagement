# CherryAI Enterprise Asset Management - Documentation Index
Last updated: 2026-01-24


## Quick Start

| Audience | Start Here |
|----------|------------|
| New Developer | [Developer Getting Started](DeveloperGettingStarted.md) |
| Operations/DevOps | [Deployment Guide](Deployment.md) |
| Product Owner | [Architecture Overview](Architecture.md) |
| QA Engineer | [Testing & Smoke Suite](TestingAndSmokeSuite.md) |

---

## Documentation Map

### Product & Architecture

| Document | Description |
|----------|-------------|
| [Architecture.md](Architecture.md) | System overview, modules, boundaries, high-level diagrams |
| [DomainModel.md](DomainModel.md) | Core entities and relationships; multi-tenant hierarchy |
| [TenancyAndSecurity.md](TenancyAndSecurity.md) | Tenant isolation, RBAC, auth, guardrails, threat model |
| [Integrations.md](Integrations.md) | Webhooks hub, outbox pattern, dead-letter queue, idempotency |
| [WorkExecution.md](WorkExecution.md) | WR → WO → Ops → Closeout; state machines; audit log |
| [PreventiveMaintenance.md](PreventiveMaintenance.md) | PM templates, revisions, assignments, schedules, occurrences |
| [FinancialsAndDepreciation.md](FinancialsAndDepreciation.md) | GAAP + Tax books, US/Canada engines, journaling |
| [Materials.md](Materials.md) | Item master, revisions, vendor cross-ref, AVL, alternates |

### Engineering & Operations

| Document | Description |
|----------|-------------|
| [DeveloperGettingStarted.md](DeveloperGettingStarted.md) | Setup, local run, env vars, DB setup, migrations |
| [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) | How to run tests; rollback behavior; smoke test catalog |
| [SeedingAndDemoData.md](SeedingAndDemoData.md) | Seed pipelines, idempotency, demo data packages |
| [DatabaseMigrations.md](DatabaseMigrations.md) | EF migrations workflow, rollback strategy |
| [Deployment.md](Deployment.md) | Build pipeline, config, hosting notes |
| [Observability.md](Observability.md) | Logging conventions, error handling, background jobs |

### UX Standards

| Document | Description |
|----------|-------------|
| [UXStandards.md](UXStandards.md) | Layout contract, hero action contract, no inline styles rule |
| [DataGridPremium.md](DataGridPremium.md) | Grid contract, row navigation, filters, exports, state |
| [NavigationAndRouting.md](NavigationAndRouting.md) | Sidebar rules, RouteRegistry, returnUrl/backlink rules |

### Reference Documents

| Document | Description |
|----------|-------------|
| [RouteRegistry.md](RouteRegistry.md) | **CANONICAL SOURCE** - All application routes |
| [DatabaseSchema.md](DatabaseSchema.md) | PostgreSQL schema documentation |
| [DECISION_LOG.md](DECISION_LOG.md) | Historical decision log (see also ADRs) |
| [NavigationReleaseNotes.md](NavigationReleaseNotes.md) | Navigation system changelog |
| [RollbackPlaybook.md](RollbackPlaybook.md) | Database rollback procedures |

### Architecture Decision Records (ADRs)

| ADR | Decision |
|-----|----------|
| [ADR-001](adr/ADR-001-PMSchedule-Canonical-Model.md) | PMSchedule is the canonical model for PM execution |
| [ADR-002](adr/ADR-002-DemoPackV2-Canonical-Seed.md) | DemoPackV2 is canonical seed entry for smoke tests |
| [ADR-003](adr/ADR-003-SmokeTest-Transaction-Rollback.md) | Smoke tests run inside DB transaction with rollback |
| [ADR-004](adr/ADR-004-UI-Hygiene-No-Inline-Styles.md) | UI Hygiene prohibits inline style blocks |
| [ADR-005](adr/ADR-005-DataGrid-Premium-Contract.md) | DataGrid contract + premium controls + data-row-href |
| [ADR-006](adr/ADR-006-ReturnUrl-Security-Hardening.md) | ReturnUrl helper hardened against open redirect |

### Templates

| Template | Purpose |
|----------|---------|
| [ADR Template](_templates/ADR-Template.md) | Architecture Decision Record format |
| [Feature Spec Template](_templates/FeatureSpec-Template.md) | Feature specification format |
| [Runbook Template](_templates/Runbook-Template.md) | Operational runbook format |
| [Release Notes Template](_templates/ReleaseNotes-Template.md) | Release notes format |

### Operational

| Document | Description |
|----------|-------------|
| [OperationsRunbook.md](OperationsRunbook.md) | Complete operations guide: deploy, config, secrets, migrations, backups, webhooks, seeds, tenants |
| [ThirdPartyDependencies.md](ThirdPartyDependencies.md) | All dependencies, licenses, vendored assets, update policy |
| [SecurityResponse.md](SecurityResponse.md) | Key rotation procedures, incident response, audit log guidance |
| [ProductionSafety.md](ProductionSafety.md) | Demo/seed prevention, migration safety, backup policy, key rotation |
| [SupportPlaybook.md](SupportPlaybook.md) | Monitoring, top 10 failure modes, webhook recovery, DB troubleshooting |
| [DocumentationCoverageMap.md](DocumentationCoverageMap.md) | System area → doc → code → runbook mappings |
| [ReleaseChecklist.md](ReleaseChecklist.md) | Pre-release verification checklist |

---

## Documentation Standards

### File Naming
- Use PascalCase for document names: `DomainModel.md`
- ADRs use format: `ADR-NNN-Short-Title.md`
- Prefix with category if needed: `Architecture/ItemSourcing.md`

### Content Standards
- Use Mermaid for diagrams where applicable
- Keep docs concise but complete
- Include "Last Updated" date in each doc
- Cross-reference related documents

### Maintenance
- Update docs when making code changes (enforced by Docs Gate in CI)
- Create ADR for architectural decisions
- Keep RouteRegistry.md as canonical route source

---

## Existing Specialized Documentation

These documents contain detailed audit reports and implementation specifics:

| Document | Description |
|----------|-------------|
| [Architecture/Tenant-Control-Plane.md](Architecture/Tenant-Control-Plane.md) | Multi-tenant infrastructure details |
| [Architecture/ItemSourcing.md](Architecture/ItemSourcing.md) | Item procurement value cascade |
| [Architecture/ItemCrossReference.md](Architecture/ItemCrossReference.md) | Three-way part number resolution |
| [PM-Schedule-Consistency.md](PM-Schedule-Consistency.md) | PM Schedule canonical model analysis |
| [SeedPackages.md](SeedPackages.md) | Versioned seed package documentation |
| [SeedCoverageMatrix.md](SeedCoverageMatrix.md) | Seed data coverage analysis |
| [SeederAudit.md](SeederAudit.md) | Seeder implementation audit |
| [SmokeTestGate.md](SmokeTestGate.md) | Smoke test gate documentation |
| [UI-UX-Conformance-Audit.md](UI-UX-Conformance-Audit.md) | UI conformance audit results |
| [UI-Conformance-Allowlist.md](UI-Conformance-Allowlist.md) | Approved UI deviations |
| [NavigationAuditReport.md](NavigationAuditReport.md) | Navigation audit results |
| [NavigationMap.md](NavigationMap.md) | Visual navigation structure |
| [ReturnPathAuditReport.md](ReturnPathAuditReport.md) | Return path implementation report |
| [BrandGuardrails.md](BrandGuardrails.md) | Brand and style guidelines |
| [MasterDataBootstrap.md](MasterDataBootstrap.md) | Master data initialization |
| [MasterDataRegister.md](MasterDataRegister.md) | Master data entity catalog |
| [NamingMap.md](NamingMap.md) | Entity naming conventions |
| [LAB-vs-DEMO-Workflow.md](LAB-vs-DEMO-Workflow.md) | LAB vs DEMO environment differences |

---

## Getting Help

1. Check this index for relevant documentation
2. Review ADRs for architectural decisions
3. Consult DECISION_LOG.md for historical context
4. Run smoke tests to verify system health
