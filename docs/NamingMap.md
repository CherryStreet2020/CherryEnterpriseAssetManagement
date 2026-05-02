# EAM Naming Map

## Overview

This document defines the canonical naming for master data concepts in CherryAI EAM. Legacy "Customer Master" terminology has been replaced with "EAM Core Masters" to better reflect the enterprise asset management domain.

---

## Naming Map

| Old Term / Identifier | New Term / Identifier | Notes |
|-----------------------|-----------------------|-------|
| Customer Master Load | EAM Core Masters Load | Primary display name |
| Customer Master | EAM Core Masters | General reference |
| CustomerMasterStatus | CoreMastersStatus | Property name (UI display) |
| RunCustomerMasterLoadAsync | RunCustomerMasterLoadAsync | **KEPT for compatibility** |
| asp-page-handler="CustomerMasterLoad" | asp-page-handler="CustomerMasterLoad" | POST handler **KEPT** |
| ?handler=RunCustomerMasterLoad | ?handler=RunCustomerMasterLoad | GET handler **KEPT** |
| ?handler=RunEamCoreMastersLoad | ?handler=RunEamCoreMastersLoad | GET handler **NEW alias** |

---

## Location Reference

### UI (Pages/Admin/DataImport.cshtml)
- Card title (line 153): `EAM Core Masters Load`
- Button label (line 170): `Run EAM Core Masters Load`
- KPI label (line 30): `EAM Core Masters`
- Success message (code-behind line 255): `EAM Core Masters load completed...`

### Documentation
- `docs/SeedPackages.md`: Updated endpoint descriptions
- `docs/MasterDataBootstrap.md`: Updated method references
- `docs/SeedEndpointSecurityProof.md`: Updated endpoint table

### Endpoints/Handlers

**POST (Form Submit):**
- `asp-page-handler="CustomerMasterLoad"` → `OnPostCustomerMasterLoadAsync()` (line 249)
- No POST alias exists (not needed - form is internal)

**GET (JSON API):**
- `?handler=RunCustomerMasterLoad` → `OnGetRunCustomerMasterLoadAsync()` (line 390) - **legacy**
- `?handler=RunEamCoreMastersLoad` → `OnGetRunEamCoreMastersLoadAsync()` (line 396) - **preferred**
- Both GET handlers call `RunEamCoreMastersLoadInternalAsync()` (line 401)

### Code (Internal - Unchanged for Compatibility)
- `IMasterDataBootstrapService.RunCustomerMasterLoadAsync()` - Service method name kept
- `OnPostCustomerMasterLoadAsync()` - POST handler name kept (form compatibility)
- `OnGetRunCustomerMasterLoadAsync()` - GET handler name kept (URL compatibility)
- `OnGetRunEamCoreMastersLoadAsync()` - NEW GET alias (preferred)

---

## Compatibility Notes

### Endpoint Aliases

**GET Handlers (JSON API):** Both call `RunEamCoreMastersLoadInternalAsync()`
```
/Admin/DataImport?handler=RunCustomerMasterLoad   (legacy, still works)
/Admin/DataImport?handler=RunEamCoreMastersLoad   (new, preferred)
```

**POST Handler (Form):** No alias needed
```
<form method="post" asp-page-handler="CustomerMasterLoad">
```

### Why Keep Internal Names?

1. **Database compatibility**: If BulkOperations or AuditLogs store handler names as text
2. **Route stability**: Existing integrations/scripts may reference old URLs
3. **Non-breaking principle**: UI labels can change without breaking functionality

---

*Last Updated: January 21, 2026*
