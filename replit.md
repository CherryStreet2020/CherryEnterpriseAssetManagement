# CherryAI Enterprise Asset Management

## Overview
CherryAI Enterprise Asset Management is an ASP.NET Core Razor Pages application designed for holding companies with multiple manufacturing subsidiaries. It offers comprehensive asset lifecycle management, including GAAP & tax book depreciation (US and Canadian compliance), maintenance tracking, and capital improvement project management. The project aims to be a leading solution in the fixed asset management market by integrating with ERP systems and providing advanced features for enterprise asset management.

## User Preferences
I prefer clear and concise information. When making changes, please explain the reasoning and potential impact. I value iterative development with frequent check-ins for major architectural decisions. Ensure the codebase remains clean, well-documented, and follows established ASP.NET Core conventions.

## System Architecture
The application is built on ASP.NET Core using Razor Pages, with PostgreSQL as the database and Entity Framework Core for ORM.

**UI/UX Decisions:**
- A modern, professional SAGE-like UI with a dark sidebar, breadcrumbs, and zero-modal inline UX, following a "CherryAI Brand Theme" (Brand Red, Navy, Inter + JetBrains Mono fonts).
- Supports both Dark and Light modes, persisted via local storage, respecting user preferences, and utilizing CSS custom properties for theme-aware styling.
- Features a "Luxury Surface System" with multi-layer shadows, 16px card radius, and premium KPI stat cards.
- Custom design system utilizing CSS variables and Design Tokens Architecture.
- Incorporates Premium DataGrid Controls for enhanced table functionality (search, sort, filter, export, client-side pagination with 25/50/100/250/500/All page sizes and persistent state).
- Implements a "Premium Asset Page Design System" with spec-card containers, color-coded accent dots, field grids, and boolean chips.
- Uses a UI Conformance System with shared partials and deterministic smoke tests.
- Features a "Modern Navigation Overhaul" with grouped sidebar, command palette (Ctrl+K), global search, and responsive design, with all transactional entities using a unified header+detail workspace pattern with inline editing.
- Zero popup modals across all pages, emphasizing inline editing and creation workflows.

**Technical Implementations & Feature Specifications:**
- **Organizational Hierarchy:** Supports Organization → Company → Site → Location → Asset structure.
- **Asset Management:** Comprehensive tracking of asset lifecycle.
- **Depreciation:** Multi-book architecture with 22 methods, 12 conventions, and US/Canadian tax engines.
- **Enterprise Master Files:** Includes Chart of Accounts and extensive system configuration.
- **User Authentication & RBAC:** Cookie-based authentication with `Admin`, `Accountant`, and `Viewer` roles, enforcing `RequireAuthorization()`.
- **Multi-Tenant Isolation:** Enforces company-scoped data access, ensuring all data operations are scoped to the user's assigned or visible companies. **Full Hierarchy Scoping (COMPLETED):** All 66+ non-admin page models use `VisibleCompanyIds.Contains()` for reads. Creates stamp `CompanyId` from `_tenantContext.CompanyId`. Site-aware entities (Asset, CIP, WorkRequest, PMSchedule) get optional `SiteId` filter. ItemEdit has `LoadItemInScopeAsync()` helper for all 21 POST handlers. ReportBuilderService scoped. PO number generation intentionally global for uniqueness. CCA/CcaClassBalance is shared reference data (no CompanyId).
- **Audit Trail & Period Locking:** Tracks changes and manages accounting periods.
- **Flexible Deployment Mode:** Configurable Financial Mode (Standalone or ERP Integration) with module activation.
- **Vendor & Purchase Management:** Complete vendor master file, POs, goods receiving, and AP with 3-way PO matching. **Vendor Page Redesign (COMPLETED):** Single inline-form `/Admin/Vendors` page split into three dedicated pages: List (`/Materials/Vendors` — clean DataGrid, no inline forms, row click navigation), Create (`/Materials/Vendors/Create` — full-page form with auto-generated code), and Edit (`/Materials/Vendors/Edit/{id}` — workspace with hero card, KPIs, tabbed sections: Vendor Info, Contact & Address, Purchase History; view/edit toggle). Tab-scoped updates prevent data loss. Server-side role enforcement on all POST handlers (Admin/Accountant only). Purchase history query tenant-scoped. List page accessible to all roles.
- **Work Order System:** Enhanced maintenance events with labor tracking and approval, including safety badging based on asset properties.
- **Smart Assist Work Requests:** Self-service work requests with rule-based auto-generation of work orders and manual conversion options.
- **Adaptive Navigation:** Dynamic sidebar based on Financial Mode.
- **Capital Improvement Projects (CIP):** End-to-end costing, traceability, and capitalization workflow.
- **Bulk Operations:** Supports bulk asset transfers, status changes, and partial disposals.
- **AI Assistant:** Enterprise-grade natural language assistant.
- **Universal Attachment System:** Allows attachments to assets and related entities.
- **Multi-Company Support:** Manages multiple companies with currency selection and a robust company hierarchy service for access control. **Single Scope Selector (COMPLETED):** Sidebar org selector is the single source of truth for company scope. ALL page-level company dropdowns removed from: Asset Register, Asset Create/Edit, Books Edit, Admin/PMScheduleEdit, Admin/StockLevels, Admin/Inventory, Admin/Items. Entity creates auto-set CompanyId from `_tenantContext.CompanyId`. Entity edits preserve existing CompanyId (read-only display). Inventory POST handlers use tenant context. Report filters (Form4562, T2Schedule8) and admin pages (Companies, Users, Sites) intentionally kept. `CompanyHierarchyService.GetVisibleCompanyIdsAsync()` fail-closed. `OrgScopeMiddleware` rejects scoped users with empty visibility (403). **Site Scoping (COMPLETED):** Sidebar site selector below company selector is single source of truth for site scope. Cookie-based `cherryai_site_id` communicates selected site to server. `TenantContextMiddleware` reads cookie and validates against `VisibleSiteIds`. Site filtering enforced on: Assets (Index, Asset detail, Delete, Dispose, Improve, Transfer), Maintenance (Index, Details, Create via MaintenanceService), WorkRequests (Index, Details), BulkOperations, Dashboard, Reports/Export. `MaintenanceService.GetScopedEventsQuery()` centrally applies site filter. `OrgController.GetSites` intersects with `VisibleSiteIds` for assigned-site users. Site selector resets when company changes. `User.AssignedSiteId` FK added to Users table. Admin Users page supports AssignedSiteId assignment.
- **Fiscal Calendar System:** Full `FiscalYear` and `FiscalPeriod` tracking.
- **Inventory & Parts Management:** Enhanced Item Master with PM templates, meter readings, kits, work order parts tracking, purchase requisitions, revision control, and cross-referencing.
- **Meter Reading History:** Full meter reading history system on Asset detail page with inline recording and history tracking.
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
- **Item Master Cross-Reference:** Three-way part number resolution system.
- **Procurement-Grade Parts:** Extended Item Master with Approved Vendor List and alternate parts.
- **Smart Mode Architecture:** System adapts behavior based on Single/Multi-Company mode.
- **Lookup Table Infrastructure:** Database-driven reference data system with admin UI, caching, and audit logging.
- **Enterprise Detail API:** Unified API for fetching detailed entity information across various types.
- **Machine Specifications:** `MachineSpecification` entity (one-to-one with Asset) for CNC machine technical data, integrated into Asset detail page with view/edit modes and unit conversions.
- **Data Management (COMPLETED):** Unified `/Admin/DataManagement` page consolidating Import Wizard + Export into tabbed UI. Import tab: 15 entity types in reordered FK-dependency phases (Phase 1: Fixed Assets — Company → Site → Location → Department → GL Account → Manufacturer → Asset Category → Asset → Dep. Books; Phase 2: Maintenance — Vendor → Item → AVL → Technician → PM Template → CIP Project). Features: premium branded static .xlsx templates in `wwwroot/templates/` (served by `TemplateService`), master workbook download (18-sheet `ABS_Machining_EAM_Import_Workbook.xlsx`), individual template downloads, ZIP of all templates, file upload with drag-and-drop, server-side validation, import with duplicate detection. Export tab preserved. Services: `TemplateService` (static file serving + ZIP + master workbook), `MasterDataImportService` (validation + import). Admin-only.
- **White-Label Branding (COMPLETED):** App white-labeled for ABS Machining. Sidebar: ABS swoosh icon (`abs-logo-icon.png`, 32px) + "EAM" text + "© Powered by CherryAI" subtitle. Login page: `abs-logo-full.png` (55px) + "ABS Machining Enterprise Asset Management". Browser tab title: "ABS Machining EAM". Footer: "© 2026 ABS Machining EAM, powered by CherryAI — a division of Cherry Street Consulting, New York, NY". Logo files in `wwwroot/images/abs-logo-*.png` (high-res 666x72, browser-scaled). Internal CSS class names/filenames still reference "cherryai" (not user-visible).
- **Technician Enrichment (COMPLETED):** Enriched `Technician` model with 15+ new fields (EmployeeId, Title, PrimaryCraft, SecondaryCraft, ProficiencyLevel, ShiftPattern, ShiftStart/End, OvertimeRate, DoubleTimeRate, HireDate, EmergencyContact, CompanyId, SiteId, SupervisorTechnicianId, PhotoPath, TenantId). New supporting models: `TechnicianCertification` (Name, CertificateNumber, IssuingAuthority, IssueDate, ExpirationDate, IsRequired) and `TechnicianSkill` (SkillName, Category, ProficiencyLevel, IsCertified, LastAssessedDate). New pages: `/Maintenance/Technicians` (Index with premium DataGrid, KPIs, craft/site/shift filters) and `/Maintenance/Technicians/Profile/{id}` (hero card + 4 tabbed sections: Skills & Certifications, Work History, Availability, Contact & HR with inline add/remove). 8 realistic demo technicians seeded with certifications and skills. Old Admin/Technicians page still accessible. Company-scoped via VisibleCompanyIds.

## External Dependencies
- **PostgreSQL:** Primary database.
- **Entity Framework Core:** ORM for database interaction.
- **OpenAI Integration:** Used for the AI Assistant feature.
- **ClosedXML:** For generating Excel report exports.
- **QuestPDF:** For generating PDF report exports.
- **Tom Select (CDN v2.3.1):** Searchable typeahead for `<select>` elements.