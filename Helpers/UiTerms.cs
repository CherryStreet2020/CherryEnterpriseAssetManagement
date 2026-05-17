namespace Abs.FixedAssets.Helpers;

public static class UiTerms
{
    public const string ModuleName = "Work Orders";
    public const string WorkOrderSingular = "Work Order";
    public const string WorkOrderPlural = "Work Orders";
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
