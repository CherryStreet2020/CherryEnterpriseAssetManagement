# ADR-003: Smoke Tests Run Inside Database Transaction with Rollback Verification

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** Architecture, Operations

---

## Context

Smoke tests need to validate data integrity and database operations, but they should not:

1. Leave test artifacts in the database
2. Interfere with other tests or users
3. Corrupt production or shared development data
4. Require manual cleanup after test runs

Previously, some tests created data without cleanup, leading to:
- Database pollution with test records
- False positives when tests found their own old data
- Difficulty distinguishing test data from real data

## Decision

**All smoke tests run inside a database transaction that is rolled back after completion.**

Specifically:
1. `SmokeTestRunner` starts a transaction before any tests
2. All tests execute within the same transaction
3. Transaction is always rolled back (even on success)
4. Rollback is verified to ensure no data persists

## Alternatives Considered

### Alternative 1: Delete Test Data After Each Test
- **Description:** Each test cleans up its own data
- **Pros:** Simple to understand
- **Cons:** Risk of incomplete cleanup, slow, order-dependent
- **Why rejected:** Too error-prone

### Alternative 2: Use Separate Test Database
- **Description:** Run tests against isolated database
- **Pros:** Complete isolation
- **Cons:** Schema sync issues, infrastructure cost, migration complexity
- **Why rejected:** Overhead not justified

### Alternative 3: Use In-Memory Database
- **Description:** Use SQLite or in-memory provider
- **Pros:** Fast, no cleanup needed
- **Cons:** PostgreSQL-specific features won't be tested
- **Why rejected:** Doesn't test real database behavior

## Consequences

### Positive
- Zero test data pollution
- Tests are fully isolated
- Can test data mutations safely
- Fast cleanup (single rollback vs multiple deletes)
- Reproducible test runs

### Negative
- All tests share single transaction (one failure might affect others)
- Cannot test transaction behavior within tests
- Long-running tests hold transaction lock

### Neutral
- Rollback verification adds small overhead
- Transaction isolation level affects concurrent access

## Implementation Notes

### Transaction Wrapper

```csharp
// SmokeTestRunner.cs
public async Task<SmokeTestResults> RunAllTestsAsync()
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    
    try
    {
        // Run all tests
        var results = await ExecuteAllTestsAsync();
        
        // Always rollback - tests should not persist data
        await transaction.RollbackAsync();
        
        return results;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Rollback Verification

```csharp
// Verify rollback actually worked
var countBefore = await _db.Assets.CountAsync();
await transaction.RollbackAsync();
var countAfter = await _db.Assets.CountAsync();

if (countAfter != countBefore)
{
    throw new InvalidOperationException("Rollback verification failed");
}
```

### Test Isolation

Each test should:
1. Assume DemoPackV2 base state
2. Create any additional needed data
3. Assert on results
4. Not depend on other test's data

## Related Documents

- [TestingAndSmokeSuite.md](../TestingAndSmokeSuite.md) - Testing documentation
- [SmokeTestGate.md](../SmokeTestGate.md) - Gate documentation

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
