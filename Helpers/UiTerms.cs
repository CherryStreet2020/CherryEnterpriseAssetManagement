namespace Abs.FixedAssets.Helpers;

public static class UiTerms
{
    // ADR-025 sibling rename (2026-05-20, Dean's call): customer-aligned
    // labels. Every major ERP draws maintenance/production/CIP distinctly —
    // SAP "PM Work Order" vs "Process Order"; Oracle "Maintenance Work
    // Order" vs "Work Order"; D365 "Maintenance Work Order" vs "Production
    // Order"; IFS "Work Order" vs "Manufacturing Order". CherryAI rendering
    // a single label "Work Order" across classifications confused users
    // based on whichever ERP they came from. Symbol names stay (10 callsites
    // unchanged); values rename. For pages that render multiple
    // WorkOrderClassification values dynamically (e.g. /WorkOrders/Details),
    // use Services.Naming.OrderTypeLabels instead — it's keyed off the
    // Classification enum and returns the right label per row.
    public const string ModuleName = "Maintenance Orders";
    public const string WorkOrderSingular = "Maintenance Order";
    public const string WorkOrderPlural = "Maintenance Orders";
    public const string PMTemplates = "PM Templates";
    public const string Schedules = "Maintenance Schedules";
    public const string CapitalProjects = "Capital Projects";
    public const string CIPSingular = "Project";
    public const string CIPPlural = "CIP Projects";

    // PR #119.6-prep — canonical long-form name for the project-management
    // header. Per Dean's call (2026-05-17): operators see this, accountants
    // see "Construction in Progress" on the GL/balance sheet (the GAAP
    // ASC 360-10 chart-of-accounts term stays unchanged on the books).
    // CIP acronym still expands cleanly to Capital Improvement Project.
    public const string CapitalImprovementProjectFull = "Capital Improvement Project";
    public const string CapitalImprovementProjectPluralFull = "Capital Improvement Projects";
    public const string AssetManagement = "Asset Register";
    public const string AssetSingular = "Asset";
    public const string AssetPlural = "Assets";
    public const string Procurement = "Purchase Orders";
    public const string PurchaseOrderSingular = "Purchase Order";
    public const string PurchaseOrderPlural = "Purchase Orders";
    public const string Administration = "Admin Hub";
    
    public static string GetWorkOrderTabTitle(string? woNumber) =>
        !string.IsNullOrEmpty(woNumber) ? $"{woNumber} — {WorkOrderSingular}" : WorkOrderSingular;
    
    public static string GetAssetTabTitle(string? assetNumber) =>
        !string.IsNullOrEmpty(assetNumber) ? $"{assetNumber} — {AssetSingular}" : AssetSingular;
    
    public static string GetProjectTabTitle(string? projectNumber) =>
        !string.IsNullOrEmpty(projectNumber) ? $"{projectNumber} — {CIPSingular}" : CIPSingular;
    
    public static string GetPurchaseOrderTabTitle(string? poNumber) =>
        !string.IsNullOrEmpty(poNumber) ? $"{poNumber} — {PurchaseOrderSingular}" : PurchaseOrderSingular;
}
