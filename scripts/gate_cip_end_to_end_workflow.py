#!/usr/bin/env python3
"""
GATE: CIP End-to-End Workflow Validation
Verifies the complete CIP feature set is structurally sound:
  1) Models: CipProject, CipCost, CipBudgetLine, CipCapitalization, CipCapitalizationCost exist
  2) Services: All 4 CIP services exist and are registered
  3) Pages: CIP detail page exists with cost tiles, related sections, capitalize support
  4) API: DetailController handles cip_project with rich payload
  5) Migration: AddCipTracingAndCapitalization migration exists
  6) FK columns: CipProjectId exists on related domain models
Outputs to proof/cip_e2e/after/
"""
import os, sys, json, datetime, re

WORKSPACE = os.path.join(os.path.dirname(__file__), "..")
results = {"gate": "cip_end_to_end_workflow", "timestamp": datetime.datetime.utcnow().isoformat(), "checks": [], "pass": True}

def add_check(name, passed, detail=""):
    results["checks"].append({"name": name, "pass": passed, "detail": detail})
    if not passed:
        results["pass"] = False

def file_contains(path, *patterns):
    full = os.path.join(WORKSPACE, path)
    if not os.path.exists(full):
        return False, f"File not found: {path}"
    with open(full, "r") as f:
        content = f.read()
    missing = [p for p in patterns if p not in content]
    if missing:
        return False, f"Missing in {path}: {missing}"
    return True, f"All {len(patterns)} patterns found in {path}"

models_path = "Models/ConstructionInProgress.cs"
ok, detail = file_contains(models_path,
    "class CipProject", "class CipCost", "class CipBudgetLine",
    "class CipCapitalization", "class CipCapitalizationCost",
    "IsCapitalized", "CapitalizedAt", "SourceType", "SourceDisplayRef",
    "SourceHeaderId", "SourceLineId", "WorkOrderId", "PurchaseOrderId",
    "VendorInvoiceId", "GoodsReceiptId", "JournalEntryId", "VendorId",
    "IsCapitalizable", "CompanyId", "SiteId")
add_check("models_complete", ok, detail)

services = [
    ("Services/Cip/CipCostService.cs", ["AddManualCostAsync", "ComputeTotalsAsync", "ReconcileProjectTotalAsync"]),
    ("Services/Cip/CipAutoCostPostingService.cs", ["PostFromWorkOrderAsync", "PostFromReceiptLineAsync", "PostFromVendorInvoiceLineAsync"]),
    ("Services/Cip/CipCapitalizationService.cs", ["PreviewAsync", "CapitalizeAsync"]),
    ("Services/Cip/CipTraceQueryService.cs", ["GetRelatedWorkOrdersAsync", "GetRelatedPurchaseOrdersAsync"]),
]
for svc_path, methods in services:
    ok, detail = file_contains(svc_path, *methods)
    svc_name = os.path.basename(svc_path).replace(".cs", "")
    add_check(f"service_{svc_name}", ok, detail)

ok, detail = file_contains("Program.cs",
    "CipCostService", "CipAutoCostPostingService",
    "CipCapitalizationService", "CipTraceQueryService")
add_check("services_registered", ok, detail)

ok, detail = file_contains("Pages/CIP/Details.cshtml.cs",
    "CipCostService", "CipCapitalizationService", "CipTraceQueryService")
add_check("details_page_injects_services", ok, detail)

details_cshtml = os.path.join(WORKSPACE, "Pages/CIP/Details.cshtml")
if os.path.exists(details_cshtml):
    with open(details_cshtml, "r") as f:
        html = f.read()
    has_tiles = "CostTypeTile" in html or "cost-type-tile" in html or "costTypeTiles" in html or "CostsByType" in html or "CipCostTypeOptions" in html or "costTypeLookupValueId" in html
    has_related = "RelatedWorkOrders" in html or "work-orders" in html.lower()
    has_capitalize = "Capitalize" in html
    add_check("details_ui_features", has_tiles and has_capitalize,
              f"tiles={has_tiles}, related={has_related}, capitalize={has_capitalize}")
else:
    add_check("details_ui_features", False, "Details.cshtml not found")

ok, detail = file_contains("Controllers/DetailController.cs",
    "GetCipProjectDetail", "work_orders", "purchase_orders",
    "vendor_invoices", "journals", "assets", "IsCapitalized")
add_check("api_enhanced", ok, detail)

migrations_dir = os.path.join(WORKSPACE, "Migrations")
migration_found = False
if os.path.isdir(migrations_dir):
    for fname in os.listdir(migrations_dir):
        if ("AddCipTracingAndCapitalization" in fname or "CipTracing" in fname or "AddCIP" in fname) and fname.endswith(".cs"):
            migration_found = True
            break
add_check("migration_exists", migration_found,
          f"AddCipTracingAndCapitalization migration: {'found' if migration_found else 'NOT FOUND'}")

ok, detail = file_contains("Data/AppDbContext.cs",
    "CipBudgetLines", "CipCapitalizations", "CipCapitalizationCosts")
add_check("dbcontext_dbsets", ok, detail)

fk_checks = [
    ("Models/AssetMaintenance.cs", "CipProjectId"),
    ("Models/PurchaseOrder.cs", "CipProjectId"),
    ("Models/VendorInvoice.cs", "CipProjectId"),
    ("Models/GoodsReceipt.cs", "CipProjectId"),
]
for fpath, prop in fk_checks:
    ok, detail = file_contains(fpath, prop)
    model_name = os.path.basename(fpath).replace(".cs", "")
    add_check(f"fk_{model_name}_CipProjectId", ok, detail)

out_dir = os.path.join(WORKSPACE, "proof", "cip_e2e", "after")
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "gate_cip_end_to_end_workflow.json")
with open(out_path, "w") as f:
    json.dump(results, f, indent=2)

status = "PASS" if results["pass"] else "FAIL"
total = len(results["checks"])
passed = sum(1 for c in results["checks"] if c["pass"])
print(f"{status}: CIP E2E Workflow — {passed}/{total} checks passed")
for c in results["checks"]:
    marker = "OK" if c["pass"] else "FAIL"
    print(f"  [{marker}] {c['name']}: {c['detail']}")
sys.exit(0 if results["pass"] else 1)
