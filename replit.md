# CherryAI Enterprise Asset Management

## Overview
CherryAI Enterprise Asset Management is an ASP.NET Core Razor Pages application designed for holding companies with multiple manufacturing subsidiaries. It provides comprehensive asset lifecycle management, including GAAP & tax book depreciation (US and Canadian compliance), maintenance tracking, and capital improvement project management. The project aims to be a leading solution in the fixed asset management market by integrating with ERP systems and offering advanced features for enterprise asset management.

## User Preferences
I prefer clear and concise information. When making changes, please explain the reasoning and potential impact. I value iterative development with frequent check-ins for major architectural decisions. Ensure the codebase remains clean, well-documented, and follows established ASP.NET Core conventions.

## System Architecture
The application is built on ASP.NET Core using Razor Pages, with PostgreSQL as the database and Entity Framework Core for ORM.

**UI/UX Decisions:**
- Modern, professional SAGE-like UI with a dark sidebar, breadcrumbs, and zero-modal inline UX, adhering to a "CherryAI Brand Theme" (Brand Red, Navy, Inter + JetBrains Mono fonts).
- Supports Dark and Light modes, persisted via local storage, respecting user preferences, and utilizing CSS custom properties.
- Features a "Luxury Surface System" with multi-layer shadows, 16px card radius, and premium KPI stat cards.
- Custom design system utilizing CSS variables and Design Tokens Architecture.
- Incorporates Premium DataGrid Controls for enhanced table functionality.
- Implements a "Premium Asset Page Design System" with spec-card containers, color-coded accent dots, field grids, and boolean chips.
- Uses a UI Conformance System with shared partials and deterministic smoke tests.
- Features a "Modern Navigation Overhaul" with grouped sidebar, command palette (Ctrl+K), global search, and responsive design.
- All transactional entities use a unified header+detail workspace pattern with inline editing, eliminating popup modals.
- The application supports white-label branding, with specific branding implemented for "ABS Machining EAM."

**Technical Implementations & Feature Specifications:**
- **Organizational Hierarchy:** Supports Organization → Company → Site → Location → Asset structure.
- **Asset Management:** Comprehensive tracking of asset lifecycle.
- **Depreciation:** Multi-book architecture with 22 methods, 12 conventions, and US/Canadian tax engines, including enhanced handling for MidQuarter, ModifiedHalfYear, and MACRS.
- **Enterprise Master Files:** Includes Chart of Accounts and extensive system configuration.
- **User Authentication & RBAC:** Cookie-based authentication with `Admin`, `Accountant`, and `Viewer` roles.
- **Multi-Tenant Isolation:** Enforces company and site-scoped data access.
- **Audit Trail & Period Locking:** Tracks changes and manages accounting periods.
- **Flexible Deployment Mode:** Configurable Financial Mode (Standalone or ERP Integration) with module activation.
- **Vendor & Purchase Management:** Complete vendor master file, POs, goods receiving, and AP with 3-way PO matching.
- **Work Order System:** Enhanced maintenance events with labor tracking and approval, including safety badging.
- **Smart Assist Work Requests:** Self-service work requests with rule-based auto-generation of work orders.
- **Adaptive Navigation:** Dynamic sidebar based on Financial Mode.
- **Capital Improvement Projects (CIP):** End-to-end costing, traceability, and capitalization workflow.
- **Bulk Operations:** Supports bulk asset transfers, status changes, and partial disposals.
- **AI Assistant:** Enterprise-grade natural language assistant.
- **Universal Attachment System:** Allows attachments to assets and related entities.
- **Multi-Company Support:** Manages multiple companies with currency selection and a robust company hierarchy service for access control.
- **Fiscal Calendar System:** Full `FiscalYear` and `FiscalPeriod` tracking.
- **Inventory & Parts Management:** Enhanced Item Master with PM templates, meter readings, kits, work order parts tracking, purchase requisitions, revision control, cross-referencing, and procurement-grade features.
- **Meter Reading History:** Full meter reading history system on Asset detail page.
- **Module Guard Coverage:** `IModuleGuardService` enforced on all module pages to control access based on module activation.
- **Barcode System:** Generation and scanning API.
- **Mobile API Preparation:** REST endpoints for mobile app integration.
- **MES Integration:** Supports Work Center assignment, Production Line/Cell tracking, and OEE.
- **IoT Integration:** Full IoT device connectivity and SCADA integration.
- **Calibration Tracking:** Complete calibration management.
- **Safety & Compliance:** Safety classifications and regulatory requirements (LOTO, Confined Space, OSHA).
- **Predictive Maintenance:** Full PdM support with sensor readings, thresholds, and ML-driven predictions.
- **Webhooks Integration Hub:** Outbox-based webhook delivery with configurable subscriptions and retry logic.
- **Inbound Webhooks Integration:** Receiver for inbound webhooks with signature verification and background processing.
- **Integration Endpoints Management:** Admin UI for managing integration endpoints and health metrics.
- **Integration ID Mapping:** Admin UI for mapping external system IDs to internal entities.
- **PM Execution Loop:** Preventive maintenance scheduler for auto-generating work orders.
- **PM Template Revision Control:** In-app revision control for PM Templates.
- **Item Revision Management:** Comprehensive system for managing item revisions.
- **Smart Mode Architecture:** System adapts behavior based on Single/Multi-Company mode.
- **Lookup Table Infrastructure:** Database-driven reference data system with admin UI, caching, and audit logging.
- **Enterprise Detail API:** Unified API for fetching detailed entity information across various types.
- **Machine Specifications:** `MachineSpecification` entity integrated into Asset detail page with view/edit modes and unit conversions.
- **Data Management:** Unified `/Admin/DataManagement` page consolidating Import Wizard + Export into a tabbed UI, supporting 15 entity types in FK-dependency phases with premium branded static .xlsx templates and server-side validation.
- **Technician Enrichment:** Enriched `Technician` model with detailed employee, skill, and certification information.
- **Fixed Assets Production Hardening:** Implemented production readiness features including robust report scoping, advanced depreciation engine logic, period locking, flexible GL account lookups, strict validation, unique indexing, and an idempotent `DepreciationBackfillService`.
- **Test Coverage:** Extensive Playwright test suite covering authentication, navigation, smoke tests, UI, core flows, fixed assets, and reports, ensuring end-to-end validation. Includes production observability checks for health and readiness endpoints and `Server-Timing` headers for performance monitoring.
- **Production Observability:** Health and readiness endpoints expose process state to autoscale router and operators. `RequestIdMiddleware` enhances logging with request identifiers.
- **Production Performance & Security:** Includes `DbCommandInterceptor` for slow query logging, `ServerTimingMiddleware` for `Server-Timing` headers, response compression (Brotli/Gzip), and `PartitionedRateLimiter` for login endpoint protection against brute-force attacks.
- **Concurrency Hardening:** Global `QuerySplittingBehavior.SplitQuery` for EF Core, optimized Npgsql connection string, and memoization of tenant resolution to improve stability under load.

## External Dependencies
- **PostgreSQL:** Primary database.
- **Entity Framework Core:** ORM for database interaction.
- **OpenAI Integration:** Used for the AI Assistant feature.
- **ClosedXML:** For generating Excel report exports.
- **QuestPDF:** For generating PDF report exports.
- **Tom Select (CDN v2.3.1):** Searchable typeahead for `<select>` elements.
- **SkiaSharp + ZXing.Net.Bindings.SkiaSharp:** Barcode and label PNG rendering, with native library provided by `SkiaSharp.NativeAssets.Linux.NoDependencies`.