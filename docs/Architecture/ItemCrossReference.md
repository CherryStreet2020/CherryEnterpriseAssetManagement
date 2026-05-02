# Item Master Cross-Reference Architecture (Sprint 11)

## Overview

The Item Master Cross-Reference system enables three-way part number resolution, allowing items to be found by:
1. **Internal Part Number** - The primary identifier in CherryAI
2. **Manufacturer Part Number (MPN)** - The manufacturer's catalog number
3. **Vendor Part Number (VPN)** - The vendor/supplier's catalog number

## Data Model

### Core Entities

#### Item (Enhanced)
- `CurrentReleasedRevisionId` - Pointer to the current released ItemRevision
- `ManufacturerParts` - Collection of ItemManufacturerPart records

#### ItemRevision
Follows the same pattern as PMTemplateRevision:
- `RevisionCode` - Auto-generated (A, B, C... AA, AB...)
- `Status` - Draft | Released | Obsolete
- `SupersedesItemRevisionId` - Chain to previous revision
- Approval tracking (ApprovedByUserId, ApprovedAtUtc)
- Effective date range (EffectiveFromUtc, EffectiveToUtc)

#### Manufacturer (Enhanced)
- `Code` - Unique manufacturer code (required)
- `TenantId` - Multi-tenant support (optional)

#### ItemManufacturerPart
Links an Item to a Manufacturer with MPN:
- `ItemId` - FK to Item
- `ManufacturerId` - FK to Manufacturer
- `MfrPartNumber` - The manufacturer's part number
- `LifecycleStatus` - Active/Obsolete/Discontinued
- `DatasheetUrl` - Link to product datasheet
- Unique constraint: (ItemId, ManufacturerId, MfrPartNumber)

#### VendorItemPart
Links an Item to a Vendor with VPN:
- `ItemId` - FK to Item
- `VendorId` - FK to Vendor
- `VendorPartNumber` - The vendor's catalog number
- `ItemManufacturerPartId` - Optional FK to link VPN to MPN
- Pricing and ordering info (UnitPrice, LeadTimeDays, MinOrderQty, PackQty)
- `Preferred` - Flag for preferred vendor
- Unique constraint: (VendorId, VendorPartNumber)

## Resolution Logic

### Priority Order
1. Internal Part Number (exact match, case-insensitive)
2. Manufacturer Part Number (exact match, case-insensitive)
3. Vendor Part Number (exact match, case-insensitive, optional vendor filter)

### Resolution Result
Returns `ItemResolutionResult` containing:
- ItemId and CurrentRevisionId
- PartNumber and Name
- `OriginMatched` enum (Internal, MfrPartNumber, VendorPartNumber)
- `MatchedValue` - The actual value that matched
- Related vendor/manufacturer info when applicable

## Services

### IItemRevisionService
- `CreateDraftFromItemAsync()` - Create new draft revision from item
- `CreateDraftFromRevisionAsync()` - Create draft that supersedes existing revision
- `ReleaseRevisionAsync()` - Release draft, auto-obsolete previous released
- `ObsoleteRevisionAsync()` - Manually obsolete a revision

### IItemCrossReferenceService
- `AddMpnAsync()` / `UpdateMpnAsync()` - Manage manufacturer parts
- `AddVpnAsync()` / `UpdateVpnAsync()` - Manage vendor parts
- `ResolveItemAsync()` - Exact match resolution with priority
- `SearchItemsAsync()` - Fuzzy search across all part number types

## UI Pages

### /Materials/Items
- List view with search across all part number types
- Optional vendor filter for VPN resolution
- Shows MPN count and VPN count per item
- Current revision badge

### /Materials/ItemEdit/{id?}
- Create/Edit item basics
- Revision management with Draft/Release workflow
- Manufacturer Parts tab (add/list MPNs)
- Vendor Parts tab (add/list VPNs with optional MPN link)

## Smoke Tests

- #21: Resolution by Internal PN / MPN / VPN with 3 vendors
- #22: VPN uniqueness per vendor enforcement
- #23: Item revision immutability and current pointer update
