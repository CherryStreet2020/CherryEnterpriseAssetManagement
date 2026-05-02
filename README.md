# CherryAI EAM

**CherryAI Enterprise Asset Management** — A comprehensive fixed asset and maintenance management platform for manufacturing organizations.

## Product Tiers

| Tier | Name | Target Audience |
|------|------|-----------------|
| 1 | **CherryAI EAM Launchpad** | Small businesses with basic asset tracking needs |
| 2 | **CherryAI EAM Autopilot** | Mid-market companies requiring automation and compliance |
| 3 | **CherryAI EAM Command Center** | Enterprise organizations with complex multi-site operations |

## Capabilities

CherryAI EAM operates as a **standalone financial system** while also integrating seamlessly with:
- **ERP systems** (SAP, Oracle, Microsoft Dynamics)
- **MES platforms** (Manufacturing Execution Systems)
- **IoT/SCADA** for real-time asset monitoring

## Key Features

- GAAP & Tax book depreciation (US & Canadian compliance)
- Preventive maintenance scheduling and work order management
- Capital improvement project (CIP) tracking
- Inventory and spare parts management
- Multi-company, multi-site support
- AI-powered assistant for natural language queries

## Environment Safety

### LAB vs DEMO Environments

| Environment | Purpose | Seeding Allowed |
|-------------|---------|-----------------|
| **LAB** | Development sandbox for experiments and testing | Yes (with safeguards) |
| **DEMO** | Protected demonstration environment | Read-only, no seeding |
| **PROD** | Production environment | Fully locked |

**Rules:**
- DEMO is read-only — no data seeding or destructive operations
- LAB is where development and experiments happen
- Set `APP_ENVIRONMENT=LAB` or `APP_ENVIRONMENT=DEMO` to control environment mode
- Visual banner displays current environment to prevent confusion

## Technology Stack

- ASP.NET Core 9.0 (Razor Pages)
- PostgreSQL with Entity Framework Core
- Premium Hero Design System (custom CSS)

## Getting Started

1. Clone the repository
2. Set `DATABASE_URL` environment variable
3. Run `dotnet run`
4. Access the application at `http://localhost:5000`

## License

Copyright 2026 CherryAI, a division of Cherry Street Consulting. All Rights Reserved.
