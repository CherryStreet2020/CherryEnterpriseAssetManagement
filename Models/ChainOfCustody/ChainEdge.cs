// Sprint 12D PR #2 / ADR-022 — chain-of-custody graph edge.
//
// Typed, directional edge between two ChainNodes. EdgeType examples:
//   RECEIVED_AT, INSPECTED_BY, CERTIFIED_BY, MELTED_FROM, SUPPLIED_BY,
//   CARRIED_BY, CONSUMED_BY, APPROVED_BY, POSTED_TO.
// Adding a new edge type is just a new constant string — no schema migration.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Abs.FixedAssets.Models.ChainOfCustody;

[Table("ChainEdges")]
public sealed class ChainEdge
{
    public long Id { get; set; }

    public long FromNodeId { get; set; }
    public ChainNode? FromNode { get; set; }

    public long ToNodeId { get; set; }
    public ChainNode? ToNode { get; set; }

    [Required, StringLength(40)]
    public string EdgeType { get; set; } = string.Empty;

    public int TenantId { get; set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Canonical edge-type constants — used by every IChainOfCustodyService
/// caller (ReceivingPostingService, IWorkOrderService, ApPostingService,
/// IPurchasingService, IItemMasterService).
/// </summary>
public static class ChainEdgeTypes
{
    // Receiving chain (PR #3 wires these into ReceivingPostingService)
    public const string ReceivedAt    = "RECEIVED_AT";    // Receipt → PO
    public const string ContainsItem  = "CONTAINS_ITEM";  // ReceiptLine → Receipt
    public const string OfPoLine      = "OF_PO_LINE";     // ReceiptLine → POLine
    public const string InspectedBy   = "INSPECTED_BY";   // Receipt → IQC
    public const string CertifiedBy   = "CERTIFIED_BY";   // Receipt → Cert
    public const string MeltedFrom    = "MELTED_FROM";    // Heat → Cert (cert traces this heat)
    public const string OfMaterial    = "OF_MATERIAL";    // Heat → MaterialMaster
    public const string SuppliedBy    = "SUPPLIED_BY";    // PO → Vendor
    public const string CarriedBy     = "CARRIED_BY";     // Receipt → Carrier

    // Production chain
    public const string ConsumedBy    = "CONSUMED_BY";    // ReceiptLine → WorkOrder
    public const string ProducedBy    = "PRODUCED_BY";    // WorkOrder → Asset (CapitalImprovement)
    public const string CapitalizedTo = "CAPITALIZED_TO"; // CapitalImprovement → Asset

    // Approval chain
    public const string ApprovedBy    = "APPROVED_BY";    // PO → User
    public const string SubmittedBy   = "SUBMITTED_BY";   // PO → User

    // Financial chain
    public const string InvoicesFor   = "INVOICES_FOR";   // Invoice → Receipt
    public const string PostedTo      = "POSTED_TO";      // Invoice → GlEntry

    // Item lineage
    public const string RevisionOf    = "REVISION_OF";    // Item → Item (predecessor revision)

    // Sprint 13.5 PR #2 — CustomerProject chain edges. Only mutating ops on
    // ICustomerProjectService emit these — read paths still query the FK
    // relationships directly. Amendment / Phase edges are deliberately
    // omitted in PR #2 (amendments live in the append-only ProjectAmendments
    // table backed by a status-regression trigger; phases are internal WBS).
    public const string MemberOf            = "MEMBER_OF";            // Customer → CustomerProject (ProjectMember row)
    public const string ContainsProductionOrder = "CONTAINS_PRODUCTION_ORDER"; // CustomerProject → ProductionOrder (link emitted by LinkProductionOrderAsync)

    // Sprint 13.5 PR #5c.1 — Routing / WorkCenter / ProductionOperation chain edges.
    // Wired by WorkCenterService.LinkAssetAsync (WC→Asset), RoutingService.AddOperationAsync
    // (Routing→RoutingOperation + RoutingOperation→WorkCenter), and
    // ProductionOperationService.ReleaseFromRoutingAsync (Order→ProductionOperation per row +
    // ProductionOperation→WorkCenter per row).
    public const string HasRouting          = "HAS_ROUTING";              // ProductionOrder → Routing (set on release)
    public const string RoutingHasOperation = "ROUTING_HAS_OPERATION";    // Routing → RoutingOperation
    public const string OperationAtWorkCenter = "OPERATION_AT_WORKCENTER"; // RoutingOperation → WorkCenter OR ProductionOperation → WorkCenter
    public const string OrderHasOperation   = "ORDER_HAS_OPERATION";      // ProductionOrder → ProductionOperation (per snapshot row)
    public const string WorkCenterUsesAsset = "WORKCENTER_USES_ASSET";    // WorkCenter → Asset (linked via WorkCenterAssetLink)
}
