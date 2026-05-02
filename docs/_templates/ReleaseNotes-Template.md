# Release Notes: v[X.Y.Z]

**Release Date:** YYYY-MM-DD  
**Release Type:** Major | Minor | Patch | Hotfix

---

## Highlights

[Brief summary of the most important changes in this release - 2-3 sentences]

## New Features

### [Feature Name]
[Description of the feature and its benefits]

**How to use:**
1. [Step 1]
2. [Step 2]

### [Feature Name]
[Description]

## Improvements

- **[Area]:** [Improvement description]
- **[Area]:** [Improvement description]

## Bug Fixes

- **[Issue ID]:** [Description of the bug and fix]
- **[Issue ID]:** [Description of the bug and fix]

## Breaking Changes

### [Change Name]
**What changed:** [Description]

**Migration steps:**
1. [Step 1]
2. [Step 2]

**Affected areas:** [List of affected modules/features]

## Database Changes

### New Tables
- `table_name`: [Purpose]

### Modified Tables
- `table_name`: Added column `column_name` ([type])

### Migrations Required
```bash
dotnet ef database update
```

## Configuration Changes

| Setting | Old Value | New Value | Notes |
|---------|-----------|-----------|-------|
| `Setting.Name` | [old] | [new] | [notes] |

## Security Updates

- [Security update description]

## Known Issues

- **[Issue]:** [Description and workaround if available]

## Deprecations

- **[Feature/API]:** Deprecated in this release. Will be removed in v[X.Y.Z].

## Upgrade Instructions

1. Backup database
2. Stop application
3. Deploy new version
4. Run migrations: `dotnet ef database update`
5. Update configuration if needed
6. Start application
7. Run smoke tests

## Rollback Instructions

If issues occur after upgrade:

1. Stop application
2. Restore database backup
3. Deploy previous version
4. Start application

## Contributors

- [Name] - [Contribution area]

## Related Documentation

- [Link to feature docs]
- [Link to migration guide]
