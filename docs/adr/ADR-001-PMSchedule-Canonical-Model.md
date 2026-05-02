# ADR-001: PMSchedule is the Canonical Model for PM Execution

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** Architecture, Data

---

## Context

CherryAI EAM has two entities that could represent preventive maintenance schedules:

1. **PMSchedule** - Newer model with full tenant scoping, template revision linking, and occurrence tracking
2. **MaintenanceSchedule** - Legacy model with simpler structure

There was confusion about which model to use for PM execution, KPI reporting, and UI display. This led to inconsistencies in:
- Which data appeared in PM Schedule list pages
- What data was used for PM compliance metrics
- How the PM execution loop generated work orders

## Decision

**PMSchedule is the canonical model for all PM execution functionality.**

Specifically:
1. All PM schedule CRUD operations use `PMSchedule` entity
2. PM execution loop queries `PMSchedule` to generate work orders
3. PM compliance KPIs are calculated from `PMSchedule` and `PMOccurrence`
4. UI pages (`/Maintenance/Schedules`) display `PMSchedule` data
5. Template revisions link to `PMScheduleTemplate` → `PMSchedule`

## Alternatives Considered

### Alternative 1: Use MaintenanceSchedule
- **Description:** Continue using the legacy MaintenanceSchedule model
- **Pros:** Already has existing data, simpler structure
- **Cons:** No tenant isolation, no template versioning, no occurrence tracking
- **Why rejected:** Lacks required enterprise features

### Alternative 2: Merge Both Models
- **Description:** Combine fields from both into single unified model
- **Pros:** Single source of truth
- **Cons:** Breaking migration, data loss risk
- **Why rejected:** Too risky for production data

### Alternative 3: Dual Support
- **Description:** Support both models with adapter layer
- **Pros:** Backward compatibility
- **Cons:** Maintenance burden, confusion about which to use
- **Why rejected:** Adds complexity without clear benefit

## Consequences

### Positive
- Clear single source of truth for PM data
- Full tenant isolation (Company + Site scoping)
- Template revision control with version history
- Occurrence tracking for audit and compliance
- Consistent KPI calculations

### Negative
- MaintenanceSchedule data may need migration
- Some legacy code may reference wrong model

### Neutral
- Both entities remain in database (legacy data preserved)
- MaintenanceSchedule can be deprecated gradually

## Implementation Notes

### Key Files
- `Models/PMSchedule.cs` - Canonical entity
- `Models/PMScheduleTemplate.cs` - Template revisions
- `Models/PMOccurrence.cs` - Execution tracking
- `Services/PMExecutionHostedService.cs` - Execution loop
- `Pages/Maintenance/Schedules.cshtml` - UI display

### Migration Path
1. Ensure all new PM schedules use PMSchedule
2. Migrate historical MaintenanceSchedule data to PMSchedule
3. Update all queries to use PMSchedule
4. Deprecate MaintenanceSchedule (keep for audit history)

## Related Documents

- [PreventiveMaintenance.md](../PreventiveMaintenance.md) - PM system documentation
- [PM-Schedule-Consistency.md](../PM-Schedule-Consistency.md) - Analysis report

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
