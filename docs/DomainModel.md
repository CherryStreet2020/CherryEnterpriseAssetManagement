# CherryAI EAM - Domain Model
Last updated: 2026-01-24


## Overview

This document describes the core domain entities and their relationships in CherryAI EAM.

## Organizational Hierarchy

```mermaid
graph TD
    Org[Organization] --> Company
    Company --> Site
    Site --> Location
    Location --> Asset
    
    style Org fill:#e1f5fe
    style Company fill:#b3e5fc
    style Site fill:#81d4fa
    style Location fill:#4fc3f7
    style Asset fill:#29b6f6
```

| Entity | Description | Key Fields |
|--------|-------------|------------|
| Organization | Top-level tenant | Name, TenantId |
| Company | Legal entity within org | Code, Name, Currency, FiscalYearEnd |
| Site | Physical facility | Code, Name, Address |
| Location | Area within site | Code, Name, ParentLocationId |
| Asset | Individual asset | AssetNumber, Description, Status |

## Asset Domain

### Core Asset Entity

```mermaid
erDiagram
    Asset ||--o{ DepreciationBook : "has"
    Asset ||--o{ MaintenanceEvent : "has"
    Asset ||--o{ Attachment : "has"
    Asset }o--|| Location : "at"
    Asset }o--|| Company : "owned by"
    Asset }o--o| Asset : "parent"
    
    Asset {
        int Id PK
        string AssetNumber UK
        string Description
        string Status
        decimal AcquisitionCost
        date AcquisitionDate
        int CompanyId FK
        int LocationId FK
    }
```

### Asset Status Values
- `Active` - In service
- `Inactive` - Temporarily out of service
- `Disposed` - Sold, scrapped, or transferred out
- `UnderConstruction` - CIP not yet capitalized

## Depreciation Domain

### Multi-Book Architecture

```mermaid
erDiagram
    Asset ||--o{ DepreciationBook : "has"
    DepreciationBook ||--o{ DepreciationSchedule : "has"
    DepreciationBook }o--|| DepreciationMethod : "uses"
    DepreciationBook }o--|| DepreciationConvention : "uses"
    
    DepreciationBook {
        int Id PK
        int AssetId FK
        string BookType
        decimal OriginalCost
        decimal SalvageValue
        int UsefulLife
        string MethodCode FK
        string ConventionCode FK
    }
    
    DepreciationSchedule {
        int Id PK
        int BookId FK
        int Year
        int Period
        decimal Depreciation
        decimal AccumulatedDepreciation
    }
```

### Book Types
- `GAAP` - Financial reporting
- `Tax` - Tax compliance (US Federal, State, Canadian)
- `AMT` - Alternative Minimum Tax
- `ACE` - Adjusted Current Earnings

### Depreciation Methods (22 supported)
- Straight Line, Declining Balance, Sum of Years Digits
- MACRS (3-39 year), Canadian CCA Classes

## Maintenance Domain

### Work Execution Flow

```mermaid
stateDiagram-v2
    [*] --> WorkRequest: User submits
    WorkRequest --> WorkOrder: Approved/Auto-generated
    WorkOrder --> InProgress: Assigned
    InProgress --> OnHold: Waiting for parts
    OnHold --> InProgress: Parts received
    InProgress --> Completed: Work done
    Completed --> Closed: Closeout approved
    Closed --> [*]
    
    WorkRequest --> Rejected: Denied
    Rejected --> [*]
```

### PM Template → Schedule → Occurrence

```mermaid
erDiagram
    PMTemplate ||--o{ PMScheduleTemplate : "versions"
    PMScheduleTemplate ||--o{ PMSchedule : "instantiated as"
    PMSchedule ||--o{ PMOccurrence : "generates"
    PMOccurrence ||--o| WorkOrder : "creates"
    
    PMTemplate {
        int Id PK
        string TemplateName
        int CurrentRevisionId FK
    }
    
    PMScheduleTemplate {
        int Id PK
        int TemplateId FK
        string RevisionCode
        string Status
    }
    
    PMSchedule {
        int Id PK
        int TemplateRevisionId FK
        int AssetId FK
        string Frequency
        date NextDue
    }
```

## Materials Domain

### Item Master with Revisions

```mermaid
erDiagram
    Item ||--o{ ItemRevision : "has revisions"
    Item ||--o{ VendorPartNumber : "has VPNs"
    Item ||--o{ ManufacturerPartNumber : "has MPNs"
    Item }o--|| ItemCategory : "categorized"
    
    ItemRevision {
        int Id PK
        int ItemId FK
        string RevisionCode
        string Status
        date EffectiveFromUtc
    }
    
    VendorPartNumber {
        int Id PK
        int ItemId FK
        int VendorId FK
        string PartNumber
        decimal UnitCost
    }
```

### Item Status Values
- `Active` - Available for use
- `Inactive` - Not for new orders
- `Obsolete` - Being phased out
- `Superseded` - Replaced by another item

## Integration Domain

### Webhook & Event Processing

```mermaid
erDiagram
    IntegrationEndpoint ||--o{ WebhookSubscription : "has"
    WebhookSubscription ||--o{ WebhookDelivery : "delivers"
    IntegrationEndpoint ||--o{ InboundEvent : "receives"
    IntegrationEndpoint ||--o{ IntegrationMapping : "maps"
    
    InboundEvent {
        int Id PK
        string EventType
        string Payload
        string Status
        string IdempotencyKey UK
    }
    
    WebhookDelivery {
        int Id PK
        string Payload
        string Status
        int RetryCount
    }
```

## Financial Domain

### Chart of Accounts Structure

```mermaid
graph TD
    COA[Chart of Accounts] --> Assets[Assets 1xxx]
    COA --> Liabilities[Liabilities 2xxx]
    COA --> Equity[Equity 3xxx]
    COA --> Revenue[Revenue 4xxx]
    COA --> Expenses[Expenses 5xxx]
    
    Assets --> FixedAssets[Fixed Assets 1500-1599]
    Assets --> AccumDepr[Accum Depr 1600-1699]
    Expenses --> DeprExpense[Depr Expense 5200-5299]
```

## Multi-Tenant Isolation

All operational entities include tenant/company scoping:

| Scope Level | Filter Pattern |
|-------------|----------------|
| Tenant | `TenantId == currentTenant` |
| Company | `CompanyId == selectedCompany` |
| Site | `SiteId == selectedSite` |

See [TenancyAndSecurity.md](TenancyAndSecurity.md) for enforcement details.

## Key Constraints

### Unique Constraints
- `Asset.AssetNumber` per Company
- `Item.PartNumber` globally
- `InboundEvent.IdempotencyKey` globally

### Referential Integrity
- Cascade delete for child entities (schedules, attachments)
- Restrict delete for referenced entities (locations with assets)

## Related Documents

- [Architecture.md](Architecture.md) - System architecture
- [DatabaseSchema.md](DatabaseSchema.md) - Physical schema
- [PreventiveMaintenance.md](PreventiveMaintenance.md) - PM details
