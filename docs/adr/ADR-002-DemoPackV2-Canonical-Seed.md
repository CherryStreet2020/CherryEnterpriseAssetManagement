# ADR-002: DemoPackV2 is the Canonical Seed Entry for Smoke Tests and Demo Data

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** Data, Operations

---

## Context

CherryAI EAM has multiple seed packages that can be run independently or together. For automated testing and demo environments, we need a consistent, reliable seed state that:

1. Provides all required test fixtures
2. Is idempotent (safe to run multiple times)
3. Creates realistic demo data
4. Supports all smoke test assertions

Different developers were using different seed packages, leading to:
- Inconsistent test results across environments
- "Works on my machine" issues
- Confusion about which seed package to run

## Decision

**DemoPackV2 is the canonical seed entry for smoke tests and demo data.**

Specifically:
1. Smoke tests assume DemoPackV2 has been run
2. DemoPackV2 depends on and runs all prerequisite packages
3. Test fixtures use predictable identifiers from DemoPackV2
4. Development/LAB environments automatically run DemoPackV2

## Alternatives Considered

### Alternative 1: Run All Packages Individually
- **Description:** Require explicit running of each package in order
- **Pros:** Granular control
- **Cons:** Error-prone, order-dependent, easy to miss packages
- **Why rejected:** Too complex for typical use

### Alternative 2: Single Monolithic Seed
- **Description:** One giant seed package with everything
- **Pros:** Simple to run
- **Cons:** Hard to maintain, slow, can't run partial seeds
- **Why rejected:** Inflexible for different needs

### Alternative 3: No Canonical Seed
- **Description:** Let each test setup its own fixtures
- **Pros:** Tests are self-contained
- **Cons:** Slow tests, duplication, drift between test data
- **Why rejected:** Doesn't scale for integration tests

## Consequences

### Positive
- Consistent test environment across all developers
- Predictable test fixtures (e.g., `ASSET-001` always exists)
- Single command to set up development environment
- Smoke tests are reliable and reproducible

### Negative
- DemoPackV2 must be maintained as tests evolve
- Changes to demo data require updating tests
- Package dependencies must be kept in sync

### Neutral
- Other packages can still be run independently
- DemoPackV2 orchestrates prerequisite packages

## Implementation Notes

### Canonical Identifiers

Tests can rely on these identifiers:

```csharp
// Assets
"ASSET-001" - CNC Lathe (Active)
"ASSET-002" - Press Brake (Active)

// Work Orders
"WO-2026-0001" - Open work order
"WO-2026-0002" - Completed work order

// Items
"BRG-6205" - Ball bearing
"MTR-001" - Motor

// Vendors
"VENDOR-GRAINGER" - Grainger
"VENDOR-MSC" - MSC Industrial
```

### Package Execution Order

DemoPackV2 ensures these run first:
1. SystemReferencePackage
2. OrganizationPackage
3. FinancePackage
4. VendorPackage
5. PartsPackage
6. EAMExecutionPackage
7. DemoPackV2 (demo-specific data)

### Idempotency

All operations use upsert pattern:
```csharp
var existing = await db.Assets.FirstOrDefaultAsync(a => a.AssetNumber == "ASSET-001");
if (existing != null) { /* update */ } else { /* insert */ }
```

## Related Documents

- [SeedingAndDemoData.md](../SeedingAndDemoData.md) - Seeding documentation
- [SeedPackages.md](../SeedPackages.md) - Package catalog
- [TestingAndSmokeSuite.md](../TestingAndSmokeSuite.md) - Testing guide

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
