# CherryAI EAM - Deployment Guide
Last updated: 2026-01-24


## Overview

This document describes how to build, configure, and deploy CherryAI EAM to production environments.

## Deployment Targets

| Target | Description | Use Case |
|--------|-------------|----------|
| Replit | Primary hosting platform | SaaS deployment |
| Docker | Containerized deployment | On-premise |
| VM/IaaS | Traditional server | Enterprise |

## Build Process

### Development Build

```bash
dotnet build
```

### Production Build

```bash
dotnet publish -c Release -o ./publish
```

### Build Output

```
publish/
├── Abs.FixedAssets.dll
├── Abs.FixedAssets.runtimeconfig.json
├── appsettings.json
├── wwwroot/
│   ├── css/
│   └── js/
└── [dependencies...]
```

## Environment Configuration

### Required Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `DATABASE_URL` | PostgreSQL connection string | Yes |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | Yes |
| `ASPNETCORE_URLS` | Binding URLs | Yes |

### Optional Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `AI_INTEGRATIONS_OPENAI_API_KEY` | OpenAI API key | (none) |
| `LOG_LEVEL` | Logging verbosity | Information |

### Environment Values

| Environment | `ASPNETCORE_ENVIRONMENT` |
|-------------|--------------------------|
| Development | `Development` |
| Staging | `Staging` |
| Production | `Production` |

## Replit Deployment

### Automatic Deployment

On Replit, the application runs via configured workflow:

```
Command: dotnet run --project Abs.FixedAssets.csproj
Port: 5000
```

### Publishing

1. Ensure all changes committed
2. Click "Deploy" in Replit
3. Verify deployment succeeded
4. Test production URL

### Environment Setup

1. Navigate to Secrets panel
2. Add required environment variables
3. Add PostgreSQL database
4. Configure custom domain (optional)

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Abs.FixedAssets.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Abs.FixedAssets.dll"]
```

### Docker Compose

```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DATABASE_URL=${DATABASE_URL}
    depends_on:
      - db
  
  db:
    image: postgres:15
    environment:
      - POSTGRES_USER=cherryai
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=cherryai_prod
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

## Database Setup

### PostgreSQL Requirements

| Requirement | Value |
|-------------|-------|
| Version | 14+ |
| Extensions | None required |
| Character Set | UTF-8 |

### Connection String Format

```
postgresql://user:password@host:port/database?sslmode=require
```

### Initial Setup

```bash
# Apply migrations
dotnet ef database update

# Verify connection
dotnet run -- --verify-db
```

## SSL/TLS Configuration

### Replit

SSL automatically provided for custom domains.

### Self-Hosted

Configure reverse proxy (nginx/caddy):

```nginx
server {
    listen 443 ssl;
    server_name cherryai.example.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
    }
}
```

## Health Checks

### Health Endpoint

```bash
curl http://localhost:5000/health
```

Response:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "memory": "Healthy"
  }
}
```

### Readiness Check

```bash
curl http://localhost:5000/ready
```

## Monitoring

### Logging

Application logs to stdout in JSON format:

```json
{
  "timestamp": "2026-01-24T10:30:00Z",
  "level": "Information",
  "message": "Request completed",
  "requestId": "abc123",
  "duration": 45
}
```

### Metrics (Optional)

If enabled, Prometheus metrics at `/metrics`:

- `http_requests_total`
- `http_request_duration_seconds`
- `db_query_duration_seconds`

## Deployment Checklist

### Pre-Deployment

- [ ] All tests passing
- [ ] Smoke suite green
- [ ] Database migrations tested
- [ ] Environment variables configured
- [ ] Backup taken

### Deployment Steps

1. **Backup database**
2. **Deploy new version**
3. **Apply migrations** (if any)
4. **Verify health endpoint**
5. **Run smoke tests**
6. **Monitor logs**

### Post-Deployment

- [ ] Verify key pages load
- [ ] Check error rates
- [ ] Verify integrations work
- [ ] Update documentation

## Rollback

### Quick Rollback

1. Stop new deployment
2. Restore previous version
3. Rollback migrations if needed
4. Verify functionality

### Database Rollback

```bash
# Restore from backup
pg_restore -d cherryai_prod backup.dump

# Or rollback migration
dotnet ef database update PreviousMigration
```

## Scaling

### Vertical Scaling

Increase resources:
- CPU cores
- Memory
- Database size

### Horizontal Scaling (Future)

For horizontal scaling:
- Add load balancer
- Configure session affinity
- Use distributed cache

## CI/CD Pipeline

### CI Verification

Run the comprehensive CI verification script before deployment:

```bash
# Full CI verification (build + docs freshness + smoke tests)
CI=true ./tools/ci-verify.sh
```

### Documentation Freshness Enforcement

The CI pipeline enforces documentation updates when code changes:

```bash
# Run docs freshness check
CI=true ./tools/validate-docs-change.sh
```

**Watched directories** (changes require docs updates):
- `Services/Seeding/` - Seed pipeline changes
- `Services/Testing/` - Smoke test changes
- `Services/Webhooks/` - Webhook system changes
- `wwwroot/css/` and `wwwroot/js/` - Frontend changes
- `Pages/Shared/` and `Pages/Admin/` - UI changes
- `Models/` - Domain model changes

**Exit codes:**
- `0` - Pass (docs updated or no watched changes)
- `1` - Fail (CI mode: code changed without docs update)

### Pipeline Integration

For GitHub Actions or similar CI:

```yaml
jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0'
      - name: CI Verification
        run: CI=true ./tools/ci-verify.sh
        env:
          CI: true
```

See [OperationsRunbook.md](OperationsRunbook.md) for operational procedures.

## Related Documents

- [DeveloperGettingStarted.md](DeveloperGettingStarted.md) - Development setup
- [DatabaseMigrations.md](DatabaseMigrations.md) - Migration guide
- [RollbackPlaybook.md](RollbackPlaybook.md) - Rollback procedures
- [ReleaseChecklist.md](ReleaseChecklist.md) - Release checklist
- [OperationsRunbook.md](OperationsRunbook.md) - Operations guide
- [SecurityResponse.md](SecurityResponse.md) - Security procedures
