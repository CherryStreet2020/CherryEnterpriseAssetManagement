using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Naming;

/// <summary>
/// Customer-aligned display labels for the unified <see cref="WorkOrder"/>
/// header. Every major ERP draws the maintenance/production/capital
/// distinction explicitly — SAP calls maintenance work "PM Work Order"
/// and production work "Process Order"; Oracle uses "Maintenance Work
/// Order" vs "Work Order" (overloaded); Microsoft D365 uses "Maintenance
/// Work Order" vs "Production Order"; IFS uses "Work Order" (maintenance)
/// vs "Manufacturing Order". When CherryAI renders a single label
/// "Work Order" across all classifications, every user is confused
/// based on which ERP they came from.
///
/// This helper is the display-layer mapping: keep <see cref="WorkOrder"/>
/// as the internal C# class + <c>WorkOrders</c> as the DB table (renaming
/// either would be a multi-week refactor with zero customer-facing
/// benefit), but render user-visible labels keyed off the
/// <see cref="WorkOrderClassification"/> discriminator.
///
/// Production has its own dedicated <c>ProductionOrder</c> entity per
/// ADR-012 v0.2 — the enum's Production value (1) is intentionally
/// gapped. Callers rendering Production work go through a separate
/// labelling path (the dedicated production UI).
/// </summary>
public static class OrderTypeLabels
{
    /// <summary>
    /// Singular display label (e.g. "Maintenance Order", "CIP Order").
    /// Use in page titles, detail-page headers, single-record CTAs.
    /// </summary>
    public static string Singular(WorkOrderClassification c) => c switch
    {
        WorkOrderClassification.Maintenance => "Maintenance Order",
        WorkOrderClassification.Quality     => "Quality Order",
        WorkOrderClassification.Engineering => "Engineering Order",
        WorkOrderClassification.HSE         => "HSE Order",
        WorkOrderClassification.CIP         => "CIP Order",
        _                                   => "Order"
    };

    /// <summary>
    /// Plural display label (e.g. "Maintenance Orders", "CIP Orders").
    /// Use in list page titles, list column headers, KPI labels.
    /// </summary>
    public static string Plural(WorkOrderClassification c) => c switch
    {
        WorkOrderClassification.Maintenance => "Maintenance Orders",
        WorkOrderClassification.Quality     => "Quality Orders",
        WorkOrderClassification.Engineering => "Engineering Orders",
        WorkOrderClassification.HSE         => "HSE Orders",
        WorkOrderClassification.CIP         => "CIP Orders",
        _                                   => "Orders"
    };

    /// <summary>
    /// Short code prefix matching the number-sequence convention
    /// (e.g. "MO", "CIP", "QO"). Use in compact list columns or
    /// breadcrumbs where horizontal space is tight. NOTE: the existing
    /// <see cref="WorkOrder.WorkOrderNumber"/> values like "WO-12345"
    /// are NOT migrated — the prefix here is for display chrome only,
    /// not for record identity.
    /// </summary>
    public static string ShortCode(WorkOrderClassification c) => c switch
    {
        WorkOrderClassification.Maintenance => "MO",
        WorkOrderClassification.Quality     => "QO",
        WorkOrderClassification.Engineering => "EO",
        WorkOrderClassification.HSE         => "HSE",
        WorkOrderClassification.CIP         => "CIP",
        _                                   => "WO"
    };
}
