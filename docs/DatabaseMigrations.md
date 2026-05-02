# CherryAI EAM - Database Migrations
Last updated: 2026-05-02


## Overview

CherryAI EAM uses Entity Framework Core Code-First migrations for database schema management. This document describes the migration workflow and best practices.

## Migration Workflow

### Creating a Migration

```bash
# Create new migration
dotnet ef migrations add MigrationName

# Example
dotnet ef migrations add AddCalibrationFields
```

### Applying Migrations

```bash
# Apply all pending migrations
dotnet ef database update

# Apply to specific migration
dotnet ef database update MigrationName
```

### Auto-Migration in Development

In Development/LAB environments, migrations apply automatically on startup:

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

## Migration Best Practices

### DO

- **Review generated SQL** before applying
- **Backup database** before production migrations
- **Test on staging** before production
- **Use descriptive names** (e.g., `AddAssetCalibrationFields`)
- **Keep migrations small** and focused
- **Add data migrations** separately from schema changes

### DON'T

- **Never change ID column types** (serial ↔ varchar)
- **Never delete migrations** that have been applied
- **Never modify migrations** after they've been applied
- **Avoid destructive operations** without explicit approval

## Common Operations

### Adding a Column

```csharp
// In entity
public class Asset
{
    // Existing properties...
    
    public string? NewField { get; set; }  // Nullable for existing rows
}

// Generate migration
dotnet ef migrations add AddAssetNewField
```

### Adding a Table

```csharp
// Create new entity
public class NewEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// Register in DbContext
public DbSet<NewEntity> NewEntities { get; set; }

// Generate migration
dotnet ef migrations add AddNewEntityTable
```

### Adding an Index

```csharp
// In DbContext.OnModelCreating
modelBuilder.Entity<Asset>()
    .HasIndex(a => a.AssetNumber)
    .IsUnique();

// Generate migration
dotnet ef migrations add AddAssetNumberUniqueIndex
```

### Adding Foreign Key

```csharp
// In entity
public class WorkOrder
{
    public int? SupervisorId { get; set; }
    public User? Supervisor { get; set; }
}

// In DbContext.OnModelCreating
modelBuilder.Entity<WorkOrder>()
    .HasOne(w => w.Supervisor)
    .WithMany()
    .HasForeignKey(w => w.SupervisorId)
    .OnDelete(DeleteBehavior.SetNull);
```

## Migration Files

### Structure

```
Migrations/
├── 20260101000000_InitialCreate.cs
├── 20260101000000_InitialCreate.Designer.cs
├── 20260115000000_AddPMScheduleFields.cs
├── 20260115000000_AddPMScheduleFields.Designer.cs
├── AppDbContextModelSnapshot.cs
└── ...
```

### Migration Class

```csharp
public partial class AddAssetNewField : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NewField",
            table: "Assets",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "NewField",
            table: "Assets");
    }
}
```

## Rollback Strategy

### Rolling Back a Migration

```bash
# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Rollback all migrations (DANGEROUS)
dotnet ef database update 0
```

### Safe Rollback Pattern

1. **Backup first**
   ```bash
   pg_dump $DATABASE_URL > backup.sql
   ```

2. **Test rollback** on staging
   ```bash
   dotnet ef database update PreviousMigration
   ```

3. **Apply to production** if staging succeeds

See [RollbackPlaybook.md](RollbackPlaybook.md) for detailed procedures.

## Data Migrations

### Separating Schema from Data

```csharp
// Migration 1: Add column (nullable)
migrationBuilder.AddColumn<string>("Status", "Assets", nullable: true);

// Migration 2: Populate data
migrationBuilder.Sql("UPDATE Assets SET Status = 'Active' WHERE Status IS NULL");

// Migration 3: Make non-nullable
migrationBuilder.AlterColumn<string>("Status", "Assets", nullable: false);
```

### Using SQL for Data Migration

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE assets 
        SET status = 'Active' 
        WHERE status IS NULL OR status = '';
    ");
}
```

## Troubleshooting

### Pending Migrations

```bash
# List pending migrations
dotnet ef migrations list

# Check current database version
dotnet ef database script --idempotent
```

### Migration Conflicts

If migrations conflict:

1. **Don't merge both** - choose one
2. **Re-create migration** from clean state
3. **Update snapshot** manually if needed

### Failed Migration

If migration fails mid-way:

1. Check error message
2. Fix the migration code
3. Rollback to last good state
4. Re-apply migration

## Production Considerations

### Pre-Production Checklist

- [ ] Migration tested on staging
- [ ] Rollback tested on staging
- [ ] Backup taken before apply
- [ ] Maintenance window scheduled
- [ ] Team notified

### Zero-Downtime Migrations

For additive changes (new columns, tables):
- Can apply without downtime
- Use nullable columns
- Add default values

For destructive changes (drops, renames):
- Requires maintenance window
- Deploy new code first
- Run migration
- Verify and rollback if needed

## Idempotent Migrations for Out-of-Band Schema Changes

When a schema change has already been applied directly via `psql` (e.g. an
emergency hotfix in production) and you later need to add the matching EF
migration so fresh databases get the same schema, write the migration body
with raw SQL guarded by `IF NOT EXISTS` / `IF EXISTS` so it is safe on both
already-patched and brand-new databases.

Example — `20260502205715_AddAssetCompanyAssetNumberUniqueIndex`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Assets_CompanyId_AssetNumber_Unique\" " +
        "ON \"Assets\" (\"CompanyId\", \"AssetNumber\");");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "DROP INDEX IF EXISTS \"IX_Assets_CompanyId_AssetNumber_Unique\";");
}
```

The EF model snapshot still gets updated automatically by `dotnet ef migrations
add` — only the `Up`/`Down` bodies are hand-edited. Do not hand-edit
`AppDbContextModelSnapshot.cs`.

### Verifying an index post-deploy

```bash
psql "$DATABASE_URL" -c "SELECT indexname FROM pg_indexes \
  WHERE tablename='Assets' \
  AND indexname='IX_Assets_CompanyId_AssetNumber_Unique';"

psql "$DATABASE_URL" -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" \
  WHERE \"MigrationId\" LIKE '%AddAssetCompanyAssetNumberUniqueIndex';"
```

Both queries should return exactly one row.

## Related Documents

- [RollbackPlaybook.md](RollbackPlaybook.md) - Rollback procedures
- [Deployment.md](Deployment.md) - Deployment guide
- [DatabaseSchema.md](DatabaseSchema.md) - Schema documentation
