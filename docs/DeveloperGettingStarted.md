# CherryAI EAM - Developer Getting Started
Last updated: 2026-01-24


## Overview

This guide helps developers set up a local development environment for CherryAI EAM.

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 9.0+ | Runtime and build |
| PostgreSQL | 14+ | Database (via Neon) |
| Node.js | 18+ | Optional: frontend tooling |
| Git | 2.30+ | Version control |

## Quick Start

### 1. Clone Repository

```bash
git clone <repository-url>
cd CherryAI-EAM
```

### 2. Environment Configuration

The application uses environment variables for configuration. On Replit, these are managed via the Secrets panel.

**Required Variables:**

| Variable | Description | Example |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | `postgresql://user:pass@host/db` |
| `PGHOST` | Database host | `db.neon.tech` |
| `PGUSER` | Database user | `cherryai_user` |
| `PGPASSWORD` | Database password | `********` |
| `PGDATABASE` | Database name | `cherryai_dev` |
| `PGPORT` | Database port | `5432` |

**Optional Variables:**

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |
| `AI_INTEGRATIONS_OPENAI_API_KEY` | OpenAI API key | (for AI Assistant) |

### 3. Database Setup

The application uses EF Core Code-First migrations:

```bash
# Apply migrations
dotnet ef database update

# Or run application (auto-migrates in Development)
dotnet run
```

In Development/LAB mode, the application auto-applies migrations on startup:

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
{
    await db.Database.MigrateAsync();
}
```

### 4. Seed Demo Data

Run the seed endpoint to populate demo data:

```bash
# Via curl
curl -X POST http://localhost:5000/api/seed/run

# Or via browser
# Navigate to /Admin/Seed and click "Run Seed"
```

### 5. Start Application

```bash
# Development mode
dotnet run --project Abs.FixedAssets.csproj

# Or use the configured workflow
# (Replit runs this automatically)
```

Application starts at: `http://localhost:5000`

## Project Structure

```
/
├── Abs.FixedAssets.csproj    # Main project file
├── Program.cs                 # Application entry point
├── appsettings.json          # Configuration
├── Data/
│   └── AppDbContext.cs       # EF Core DbContext
├── Models/                    # Domain entities
├── Pages/                     # Razor Pages
├── Services/                  # Domain services
├── wwwroot/                   # Static files
├── docs/                      # Documentation
└── tools/                     # Build scripts
```

## Development Workflow

### Making Changes

1. **Create feature branch**
   ```bash
   git checkout -b feature/your-feature
   ```

2. **Make changes**
   - Follow existing code conventions
   - Update documentation if needed
   - Add tests for new functionality

3. **Run smoke tests**
   ```bash
   # Navigate to /Admin/SmokeTests in browser
   # Or via API
   curl http://localhost:5000/api/smoke/run
   ```

4. **Commit changes**
   ```bash
   git add .
   git commit -m "Description of changes"
   ```

### Database Changes

For schema changes:

1. Modify entity classes in `Models/`
2. Update `AppDbContext` if needed
3. Create migration:
   ```bash
   dotnet ef migrations add MigrationName
   ```
4. Review migration SQL
5. Apply migration:
   ```bash
   dotnet ef database update
   ```

See [DatabaseMigrations.md](DatabaseMigrations.md) for details.

## Key Files

### Configuration

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration |
| `appsettings.Development.json` | Dev overrides |
| `Properties/launchSettings.json` | Launch profiles |

### Shared UI

| File | Purpose |
|------|---------|
| `Pages/Shared/_ModernLayout.cshtml` | Main layout with sidebar |
| `Pages/Shared/_Layout.cshtml` | Legacy layout |
| `Pages/Shared/_BackLink.cshtml` | Return navigation partial |
| `wwwroot/css/premium-components.css` | UI component styles |
| `wwwroot/js/enhanced-grid.js` | DataGrid controls |

### Core Services

| File | Purpose |
|------|---------|
| `Services/DepreciationService.cs` | Depreciation calculations |
| `Services/MaintenanceService.cs` | Work order logic |
| `Services/Navigation/ReturnUrlHelper.cs` | URL security |
| `Services/Testing/SmokeTestRunner.cs` | Smoke tests |

## Debugging

### Logging

Logs output to console in development:

```csharp
_logger.LogInformation("Processing asset {AssetId}", assetId);
_logger.LogError(ex, "Failed to calculate depreciation");
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Database connection failed | Check DATABASE_URL env var |
| Migrations pending | Run `dotnet ef database update` |
| Port 5000 in use | Kill existing process or change port |
| Static files not updating | Clear browser cache |

## Testing

### Smoke Tests

The smoke test suite validates core functionality:

```bash
# Run all tests
curl http://localhost:5000/api/smoke/run

# View results in browser
# Navigate to /Admin/SmokeTests
```

See [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) for details.

### Manual Testing

Key pages to verify:

| Page | What to Check |
|------|---------------|
| `/` | Dashboard loads |
| `/Assets` | Asset list displays |
| `/Maintenance` | Work orders display |
| `/Admin/SmokeTests` | All tests pass |

## Code Conventions

### Naming

| Type | Convention | Example |
|------|------------|---------|
| Classes | PascalCase | `AssetService` |
| Methods | PascalCase | `GetAssetById` |
| Properties | PascalCase | `AssetNumber` |
| Variables | camelCase | `assetCount` |
| Constants | UPPER_CASE | `MAX_RETRY_COUNT` |

### File Organization

- One class per file (usually)
- Group related files in folders
- Keep services focused and small

### Documentation Updates

When making changes, update:

1. `replit.md` - For architectural changes
2. `docs/RouteRegistry.md` - For new routes
3. Relevant ADR - For design decisions
4. Code comments - For complex logic

## Pre-Push Verification

Before pushing code changes, run the local verification script:

```bash
# Local mode (warnings only)
./tools/pre-push-verify.sh

# Strict CI mode (fails on issues)
CI=true ./tools/pre-push-verify.sh
```

This script runs:
1. **Build verification** - Ensures the project compiles
2. **Documentation freshness** - Checks if docs need updates when code changes
3. **Smoke test validation** - Verifies test infrastructure is healthy

### Optional Git Hook Installation

To automatically run verification before each push:

```bash
# Install as pre-push hook
cp tools/pre-push-verify.sh .git/hooks/pre-push
chmod +x .git/hooks/pre-push
```

The hook runs in local mode (warnings only) by default. Set `CI=true` in your shell for strict enforcement.

### CI Integration

In CI/CD pipelines, use:

```bash
CI=true ./tools/ci-verify.sh
```

See [Deployment.md](Deployment.md#cicd-pipeline) for pipeline configuration.

## Related Documents

- [TestingAndSmokeSuite.md](TestingAndSmokeSuite.md) - Testing guide
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration workflow
- [UXStandards.md](UXStandards.md) - UI conventions
