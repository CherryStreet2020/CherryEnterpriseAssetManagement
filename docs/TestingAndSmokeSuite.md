# CherryAI EAM - Testing and Smoke Suite
Last updated: 2026-01-24


## Overview

CherryAI EAM uses a comprehensive smoke test suite for automated integration testing. Tests run inside database transactions with automatic rollback for isolation.

## Smoke Test Architecture

### Transaction Isolation

All smoke tests run within a database transaction that is rolled back after completion:

```csharp
// SmokeTestRunner.cs
await using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // Run all tests
    var results = await RunAllTestsAsync();
    
    // Always rollback - tests should not persist changes
    await transaction.RollbackAsync();
    
    return results;
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

See [ADR-003](adr/ADR-003-SmokeTest-Transaction-Rollback.md) for the design decision.

### Test Categories

| Category | Purpose | Count |
|----------|---------|-------|
| Schema | Database schema validation | ~15 |
| Navigation | Route and link validation | ~10 |
| UI Conformance | Layout and style rules | ~8 |
| Data Integrity | Seed data validation | ~12 |
| Security | Auth and access control | ~6 |
| Docs Gate | Documentation validation | ~5 |

## Running Tests

### Via Browser

Navigate to `/Admin/SmokeTests`:

1. Click "Run All Tests"
2. View results with pass/fail status
3. Expand failed tests for details

### Via API

```bash
# Run all tests
curl http://localhost:5000/api/smoke/run

# Response
{
  "passed": 55,
  "failed": 2,
  "skipped": 0,
  "duration": "3.2s",
  "results": [...]
}
```

### In CI

Tests run automatically in CI pipeline:

```bash
# CI script checks exit code
dotnet run --project Abs.FixedAssets.csproj -- --smoke-tests
```

## Test Catalog

### Schema Tests (10-19)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 10 | Schema → Asset Table Exists | Core table presence |
| 11 | Schema → Required Columns | Non-nullable fields |
| 12 | Schema → Foreign Keys | Referential integrity |
| 13 | Schema → Indexes | Performance indexes |
| 14 | Schema → Unique Constraints | Business key uniqueness |

### Navigation Tests (40-49)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 40 | Navigation → All Sidebar Links Resolve | Sidebar href targets exist |
| 41 | Navigation → asp-page Targets Valid | Razor Page links valid |
| 42 | Navigation → No Broken Anchor Links | Internal anchors exist |
| 43 | Navigation → Breadcrumb Consistency | Breadcrumb trail valid |

### UI Conformance Tests (50-59)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 50 | UI → No Inline Style Blocks | No `<style>` in pages |
| 51 | UI → Hero Layout Consistency | Hero section contract |
| 52 | UI → Modal System Correct | Global modal usage |
| 53 | Return Path → Open Redirect Protection | URL security |
| 54 | Return Path → Detail Pages Accept returnUrl | Back link support |
| 55 | Return Path → Source Pages Pass returnUrl | URL passing |

### DataGrid Tests (60-69)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 60 | DataGrid → Premium Controls Present | Toolbar elements |
| 61 | DataGrid → Row Navigation Contract | data-row-href usage |
| 62 | DataGrid → Filter Attributes | Column filter config |
| 63 | DataGrid → Sort Badge CSS | Sort indicator styling |

### Route Health Tests

In-process deterministic page rendering tests that validate migrated pages render correctly with authenticated users and proper tenant scoping.

| Test Name | Validates |
|-----------|-----------|
| Route Health → Purchasing Index Renders | Purchasing page renders with _ScreenHeader |
| Route Health → Assets Index Renders | Assets page renders with _ScreenHeader |
| Route Health → Items Index Renders | Items page renders with _ScreenHeader |
| Route Health → Help Index Renders | Help page renders with _ScreenHeader |
| Route Health → UsTax Index Renders | UsTax page renders with _ScreenHeader |
| Route Health → Asset Detail Renders | Asset details page renders without duplicate layout header |

**Pattern Requirements:**
- Uses canonical principal helper (`BuildSmokeTestPrincipal()`)
- Uses tenant scoping via `ITenantContextOverride.BeginScope(tenantId: 1, companyId: 1, siteId: 1)`
- Uses in-process Razor Page invocation via `IActionInvokerFactory.CreateInvoker`
- Validates HTTP 200, expected title text, `screen-header` class presence

**Location:** `Services/Testing/SmokeTestRunner.cs`

**How to Run:**
Navigate to `/Admin/SmokeTests` and run the smoke test suite. These are in-process tests, not xUnit CLI tests.

### Docs Gate Tests (70-79)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 70 | Docs → README.md Exists | Index file present |
| 71 | Docs → Required Files Present | Core docs exist |
| 72 | Docs → RouteRegistry.md Exists | Route source exists |
| 73 | Docs → ADR Folder Not Empty | At least one ADR |

### Data Integrity Tests (80-89)

| ID | Test Name | Validates |
|----|-----------|-----------|
| 80 | Seed → DemoPackV2 Complete | Canonical seed data |
| 81 | Seed → All Required Fields Populated | Non-null requirements |
| 82 | Seed → Referential Integrity | FK relationships |

## Writing New Tests

### Test Structure

```csharp
private async Task<SmokeTestResult> Test61_DataGridRowNavigation()
{
    var testName = "DataGrid → Row Navigation Contract";
    
    try
    {
        // Arrange
        var files = GetRazorPageFiles("Pages/Assets", "Pages/Admin");
        
        // Act
        var violations = new List<string>();
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            if (content.Contains("data-row-click-page") && 
                !content.Contains("data-row-href"))
            {
                violations.Add(file);
            }
        }
        
        // Assert
        if (violations.Any())
        {
            return Fail(testName, $"Legacy attributes found: {string.Join(", ", violations)}");
        }
        
        return Pass(testName);
    }
    catch (Exception ex)
    {
        return Fail(testName, ex.Message);
    }
}
```

### Adding to Runner

```csharp
// Add to test collection in SmokeTestRunner.cs
private readonly Dictionary<int, Func<Task<SmokeTestResult>>> _tests = new()
{
    // ... existing tests
    { 61, Test61_DataGridRowNavigation },
};
```

## Rollback Verification

### Ensuring Clean Rollback

Tests that create data must verify rollback:

```csharp
// Before transaction
var countBefore = await _db.Assets.CountAsync();

// Inside transaction - create test data
_db.Assets.Add(new Asset { ... });
await _db.SaveChangesAsync();

// After rollback
var countAfter = await _db.Assets.CountAsync();

// Verify rollback worked
Assert.Equal(countBefore, countAfter);
```

### Smoke Test Isolation

Each test run is isolated:
- Transaction starts
- All tests execute
- Transaction rolls back
- No persistent changes

## Test Data

### DemoPackV2 Canonical Seed

Tests rely on `DemoPackV2` seed package for consistent data:

```csharp
// Expected by tests
var asset = await _db.Assets.FirstOrDefaultAsync(a => a.AssetNumber == "ASSET-001");
Assert.NotNull(asset);
```

See [SeedingAndDemoData.md](SeedingAndDemoData.md) for seed package details.

## Troubleshooting

### Common Failures

| Failure | Cause | Fix |
|---------|-------|-----|
| Schema tests fail | Pending migrations | Run `dotnet ef database update` |
| Navigation tests fail | Missing page file | Check route registry |
| UI tests fail | Style in page | Move to CSS file |
| Seed tests fail | Missing demo data | Run seed endpoint |

### Debugging Failed Tests

1. Check test details in UI
2. Look for specific error message
3. Run test in isolation
4. Check relevant code/files
5. Fix and re-run

## CI Integration

### GitHub Actions Example

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'
      - name: Run Smoke Tests
        run: dotnet run -- --smoke-tests
        env:
          DATABASE_URL: ${{ secrets.TEST_DATABASE_URL }}
```

## Related Documents

- [SmokeTestGate.md](SmokeTestGate.md) - Gate documentation
- [SeedingAndDemoData.md](SeedingAndDemoData.md) - Seed data
- [adr/ADR-003-SmokeTest-Transaction-Rollback.md](adr/ADR-003-SmokeTest-Transaction-Rollback.md) - ADR
