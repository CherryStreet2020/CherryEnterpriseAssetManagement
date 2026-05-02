# CherryAI EAM - Third-Party Dependencies
Last updated: 2026-01-24


## Table of Contents

1. [NuGet Packages](#nuget-packages)
2. [Frontend Libraries](#frontend-libraries)
3. [Development Tools](#development-tools)
4. [External Services](#external-services)
5. [License Summary](#license-summary)
6. [Vendored Assets](#vendored-assets)
7. [Update Policy](#update-policy)

---

## NuGet Packages

### Runtime Dependencies

| Package | Version | License | Purpose |
|---------|---------|---------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.0.4 | PostgreSQL License | PostgreSQL database provider for EF Core |
| `Microsoft.EntityFrameworkCore` | 9.0.0 | MIT | Object-relational mapper (ORM) |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.0 | MIT | EF Core design-time tools |
| `Microsoft.EntityFrameworkCore.Tools` | 9.0.0 | MIT | EF Core CLI tools for migrations |
| `ClosedXML` | 0.104.2 | MIT | Excel file generation for exports |
| `QuestPDF` | 2024.10.0 | MIT (Community) | PDF report generation |
| `ZXing.Net.Bindings.SkiaSharp` | 0.16.14 | Apache 2.0 | Barcode generation (Code128, QR, etc.) |

### ASP.NET Core (Included with .NET 9.0)

| Component | License | Purpose |
|-----------|---------|---------|
| ASP.NET Core Razor Pages | MIT | Web framework |
| ASP.NET Core Identity | MIT | Authentication and authorization |
| Kestrel Web Server | MIT | HTTP server |

See [Architecture.md](Architecture.md) for how these integrate.

---

## Frontend Libraries

### CSS/Styling

| Library | Version | License | Purpose |
|---------|---------|---------|---------|
| Custom CSS | - | Proprietary | Premium design system (modern.css, premium-components.css) |

### JavaScript

| Library | Version | License | Purpose |
|---------|---------|---------|---------|
| Vanilla JS | ES2022 | - | DataGrid premium controls, navigation |

The application uses **no external JavaScript frameworks** (no React, Vue, Angular). All interactivity is implemented with vanilla JavaScript in:
- `wwwroot/js/enhanced-grid.js` - DataGrid premium controls
- `wwwroot/js/site.js` - Global utilities

See [DataGridPremium.md](DataGridPremium.md) for implementation details.

---

## Development Tools

### Build Tools

| Tool | Version | License | Purpose |
|------|---------|---------|---------|
| .NET SDK | 9.0 | MIT | Build and runtime |
| Entity Framework Core CLI | 9.0 | MIT | Database migrations |

### Recommended IDE

| Tool | License | Notes |
|------|---------|-------|
| Visual Studio Code | MIT | With C# Dev Kit extension |
| Rider | Commercial | JetBrains IDE |
| Visual Studio | Commercial/Community | Full-featured IDE |

See [DeveloperGettingStarted.md](DeveloperGettingStarted.md) for setup.

---

## External Services

### Required Services

| Service | Purpose | Fallback |
|---------|---------|----------|
| PostgreSQL Database | Primary data store | None (required) |

### Optional Services

| Service | Purpose | Configuration |
|---------|---------|---------------|
| OpenAI API | AI Assistant feature | `AI_INTEGRATIONS_OPENAI_API_KEY` |

### Integration Endpoints

The application can integrate with external systems via webhooks:

| Integration Type | Protocol | Notes |
|------------------|----------|-------|
| Outbound Webhooks | HTTPS + HMAC-SHA256 | Configurable endpoints |
| Inbound Webhooks | HTTPS + Signature Verify | Timestamp tolerance enforced |

See [Integrations.md](Integrations.md) for webhook configuration.

---

## License Summary

### License Compatibility

All dependencies use licenses compatible with commercial use:

| License | Packages | Commercial OK |
|---------|----------|---------------|
| MIT | ClosedXML, EF Core, ASP.NET Core, QuestPDF | Yes |
| PostgreSQL License | Npgsql | Yes |
| Apache 2.0 | ZXing.Net | Yes |

### License Obligations

1. **MIT License:** Include copyright notice in distributions
2. **Apache 2.0:** Include license and notice files
3. **PostgreSQL License:** No specific obligations

### Full License Texts

License texts should be included in the deployment package at:
- `/LICENSES/MIT.txt`
- `/LICENSES/Apache-2.0.txt`
- `/LICENSES/PostgreSQL.txt`

---

## Vendored Assets

### Icons and Images

| Asset | Location | License | Source |
|-------|----------|---------|--------|
| Application icons | `wwwroot/images/` | Proprietary | Custom designed |
| Placeholder images | `wwwroot/images/placeholders/` | Proprietary | Custom designed |

### Fonts

| Font | Location | License | Source |
|------|----------|---------|--------|
| System fonts | Browser default | - | OS provided |

The application uses system font stacks for maximum compatibility:

```css
font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
```

See [UXStandards.md](UXStandards.md) for typography guidelines.

---

## Update Policy

### Security Updates

- **Critical vulnerabilities:** Update within 24 hours
- **High severity:** Update within 1 week
- **Medium/Low:** Update in next release cycle

### Dependency Review

1. Check for updates monthly:
   ```bash
   dotnet list package --outdated
   ```

2. Review changelogs for breaking changes

3. Test in staging before production

4. Update `ThirdPartyDependencies.md` after updates

### Breaking Change Protocol

1. Document breaking changes in release notes
2. Update affected documentation
3. Create migration guide if needed
4. Notify stakeholders

---

## Related Documentation

- [Architecture.md](Architecture.md) - System architecture overview
- [DeveloperGettingStarted.md](DeveloperGettingStarted.md) - Development setup
- [SecurityResponse.md](SecurityResponse.md) - Security procedures
- [Deployment.md](Deployment.md) - Deployment guide
