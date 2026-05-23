// Sprint 12D PR #2 / ADR-022 — chain-of-custody graph node.
//
// Polymorphic graph node — (NodeType, EntityId) is the business key.
// NodeType is one of the constants in ChainNodeTypes (PurchaseOrder,
// Receipt, IQC, Cert, Heat, MaterialMaster, Vendor, Carrier, WorkOrder,
// Invoice, GLEntry). Adding a new node type is just a new constant
// string — no schema migration needed.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Abs.FixedAssets.Models.ChainOfCustody;

[Table("ChainNodes")]
public sealed class ChainNode
{
    public long Id { get; set; }

    [Required, StringLength(40)]
    public string NodeType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public int TenantId { get; set; }

    [Required]
    public string Label { get; set; } = string.Empty;

    // Free-form JSON for node-type-specific attributes (status, dates,
    // amounts, etc.). Renders directly into the cytoscape.js node tooltip.
    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Canonical node-type constants — one source of truth across services
/// that emit chain edges. New node types are just new string constants here.
/// </summary>
public static class ChainNodeTypes
{
    public const string PurchaseOrder    = "PurchaseOrder";
    public const string PurchaseOrderLine = "PurchaseOrderLine";
    public const string Receipt          = "Receipt";
    public const string ReceiptLine      = "ReceiptLine";
    public const string Iqc              = "Iqc";
    public const string Cert             = "Cert";
    public const string Heat             = "Heat";
    public const string MaterialMaster   = "MaterialMaster";
    public const string Item             = "Item";
    public const string Vendor           = "Vendor";
    public const string Carrier          = "Carrier";
    public const string WorkOrder        = "WorkOrder";
    public const string Asset            = "Asset";
    public const string Invoice          = "Invoice";
    public const string GlEntry          = "GlEntry";
    public const string CapitalImprovement = "CapitalImprovement";
    public const string User             = "User";

    // Sprint 13.5 PR #2 — CustomerProject hierarchy node types. ProductionOrder
    // is introduced here (rather than PR #3) because LinkProductionOrderAsync
    // on ICustomerProjectService emits the first CustomerProject→ProductionOrder
    // edge. PR #3 IProductionOrderService extends what ProductionOrder nodes
    // participate in (CONSUMED_BY / PRODUCED_BY etc.). Customer becomes a node
    // so ProjectMember MEMBER_OF edges have a real source.
    public const string CustomerProject  = "CustomerProject";
    public const string ProductionOrder  = "ProductionOrder";
    public const string Customer         = "Customer";
}
