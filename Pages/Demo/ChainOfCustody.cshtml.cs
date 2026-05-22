// Sprint 12D PR #6 / ADR-022 §D4 — "Machine Event → General Ledger" demo
// walkthrough page. The narrative spine of the June 3 EVS pitch.
//
// What this page does:
//   - Picks a Receipt (?id=N or auto-select the most-recently-posted receipt
//     that has chain edges in the current tenant scope).
//   - Renders BOTH upstream (where this material came from) and downstream
//     (where it went next — work orders, capitalization, invoices, GL) chains
//     via the reusable _ChainOfCustodyGraph.cshtml partial.
//   - Narrates every edge in plain English in a numbered list — the same
//     narration the ExplainChainOfCustody voice tool produces, but rendered
//     as text on the page so a CFO can read the story while listening.
//   - CTA to the live receipt page + a voice-tool deep link.
//
// The point of this page: in the June 3 demo, the headline is "we go from
// goods receipt to general ledger in one query — zero Tier-1 ERPs ship this."
// This page is what we click into AFTER the voice query, so the Joe-audience
// has something concrete + visual to anchor on.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.ChainOfCustody;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Demo
{
    // Sprint 12D PR #6 — read-only demo walkthrough surface. AppDbContext is
    // used only for picker projections (most-recent receipts with chain edges)
    // and to resolve the anchor receipt's display fields. All chain traversal
    // flows through IChainOfCustodyService. No SaveChanges anywhere. Per
    // ADR-025 D1 (Service Layer Standard), [ControlPlaneExempt] documents
    // this surface is read-only and intentionally bypasses the typed-service
    // gate.
    [ControlPlaneExempt("Sprint 12D PR #6 — read-only demo walkthrough. AppDbContext queries are projections only; chain traversal goes through IChainOfCustodyService. No mutations.")]
    public class ChainOfCustodyModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IChainOfCustodyService _chainOfCustody;

        public ChainOfCustodyModel(
            AppDbContext context,
            ITenantContext tenantContext,
            IChainOfCustodyService chainOfCustody)
        {
            _context = context;
            _tenantContext = tenantContext;
            _chainOfCustody = chainOfCustody;
        }

        public int? ReceiptId { get; private set; }
        public string? ReceiptNumber { get; private set; }
        public string? VendorName { get; private set; }
        public string? PurchaseOrderNumber { get; private set; }

        // Two graphs side-by-side. Both default to empty so the partial
        // renders the friendly empty-state when there's no data yet.
        public ChainOfCustodyGraph UpstreamChain { get; set; } =
            new ChainOfCustodyGraph(0, new List<ChainHop>());

        public ChainOfCustodyGraph DownstreamChain { get; set; } =
            new ChainOfCustodyGraph(0, new List<ChainHop>());

        // Narration items — one per hop (excluding the anchor). Order matches
        // the upstream + downstream traversal order.
        public List<NarrationStep> UpstreamNarration { get; set; } = new();
        public List<NarrationStep> DownstreamNarration { get; set; } = new();

        // Receipts the user can switch between (eligibility: has at least
        // one ChainEdge originating from or pointing to it in the tenant).
        public List<ReceiptOption> CandidateReceipts { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id = null)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;

            // Build the picker — every Receipt that has at least one chain
            // edge OR is the anchor of one. Ordered most-recent-first.
            var receiptNodes = await _context.ChainNodes.AsNoTracking()
                .Where(n => n.NodeType == ChainNodeTypes.Receipt)
                .Select(n => n.EntityId)
                .ToListAsync(HttpContext.RequestAborted);

            if (receiptNodes.Count > 0)
            {
                var receipts = await _context.GoodsReceipts.AsNoTracking()
                    .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                    .Where(g => receiptNodes.Contains(g.Id)
                        && visibleIds.Contains(g.CompanyId ?? 0))
                    .OrderByDescending(g => g.ReceiptDate)
                    .Take(20)
                    .ToListAsync(HttpContext.RequestAborted);

                CandidateReceipts = receipts.Select(r => new ReceiptOption
                {
                    Id = r.Id,
                    ReceiptNumber = r.ReceiptNumber ?? $"RCPT-{r.Id}",
                    VendorName = r.PurchaseOrder?.Vendor?.Name,
                    PurchaseOrderNumber = r.PurchaseOrder?.PONumber,
                    ReceiptDate = r.ReceiptDate,
                }).ToList();
            }

            // Resolve the anchor receipt — explicit id query string, or fall
            // back to the most-recent eligible candidate.
            GoodsReceipt? anchor = null;
            if (id.HasValue)
            {
                anchor = await _context.GoodsReceipts.AsNoTracking()
                    .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                    .FirstOrDefaultAsync(g => g.Id == id.Value
                        && visibleIds.Contains(g.CompanyId ?? 0),
                        HttpContext.RequestAborted);
            }
            else if (CandidateReceipts.Count > 0)
            {
                var first = CandidateReceipts[0];
                anchor = await _context.GoodsReceipts.AsNoTracking()
                    .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                    .FirstOrDefaultAsync(g => g.Id == first.Id,
                        HttpContext.RequestAborted);
            }

            if (anchor is not null)
            {
                ReceiptId = anchor.Id;
                ReceiptNumber = anchor.ReceiptNumber;
                VendorName = anchor.PurchaseOrder?.Vendor?.Name;
                PurchaseOrderNumber = anchor.PurchaseOrder?.PONumber;

                // Upstream chain — where did this material come from?
                var up = await _chainOfCustody.GetUpstreamChainAsync(
                    ChainNodeTypes.Receipt, anchor.Id, maxDepth: 6,
                    HttpContext.RequestAborted);
                if (up.IsSuccess && up.Value is not null)
                {
                    UpstreamChain = up.Value;
                    UpstreamNarration = BuildNarration(up.Value);
                }

                // Downstream chain — where did it go next? (Receipt is the
                // start of the financial-spine chain in most cases.)
                var down = await _chainOfCustody.GetDownstreamChainAsync(
                    ChainNodeTypes.Receipt, anchor.Id, maxDepth: 6,
                    HttpContext.RequestAborted);
                if (down.IsSuccess && down.Value is not null)
                {
                    DownstreamChain = down.Value;
                    DownstreamNarration = BuildNarration(down.Value);
                }
            }

            return Page();
        }

        // Mirrors VoiceInvokeEndpoint.NarrateEdge (Sprint 12D PR #5). Kept
        // local-and-static so the demo page stays self-contained; the
        // narration template is duplicated by design — the voice handler
        // and this page each serve different surfaces (audio vs. visual)
        // and may diverge stylistically in future PRs.
        private static List<NarrationStep> BuildNarration(ChainOfCustodyGraph chain)
        {
            var steps = new List<NarrationStep>();
            var byId = chain.Hops.ToDictionary(h => h.NodeId, h => h);

            foreach (var hop in chain.Hops.Where(h => h.IncomingFromNodeId.HasValue))
            {
                var parentLabel = byId.TryGetValue(hop.IncomingFromNodeId!.Value, out var parent)
                    ? parent.Label
                    : "(unknown)";

                steps.Add(new NarrationStep
                {
                    StepNumber = steps.Count + 1,
                    EdgeType = hop.IncomingEdgeType ?? "LINKED_TO",
                    FromLabel = parentLabel,
                    ToLabel = hop.Label,
                    ToType = hop.NodeType,
                    Sentence = Narrate(hop.IncomingEdgeType, hop.NodeType, hop.Label),
                    Depth = hop.Depth,
                });
            }

            return steps;
        }

        private static string Narrate(string? edgeType, string toType, string toLabel)
        {
            return edgeType switch
            {
                ChainEdgeTypes.ReceivedAt    => $"Received under {toType.ToLower()} {toLabel}.",
                ChainEdgeTypes.SuppliedBy    => $"Supplied by {toType.ToLower()} {toLabel}.",
                ChainEdgeTypes.ContainsItem  => $"Contains item {toLabel}.",
                ChainEdgeTypes.InspectedBy   => $"Inspected by IQC {toLabel}.",
                ChainEdgeTypes.CertifiedBy   => $"Certified by {toLabel}.",
                ChainEdgeTypes.MeltedFrom    => $"Melted from heat {toLabel}.",
                ChainEdgeTypes.OfMaterial    => $"Of material master {toLabel}.",
                ChainEdgeTypes.CarriedBy     => $"Carried by {toLabel}.",
                ChainEdgeTypes.ProducedBy    => $"Produced by {toType.ToLower()} {toLabel}.",
                ChainEdgeTypes.CapitalizedTo => $"Capitalized to asset {toLabel}.",
                ChainEdgeTypes.ApprovedBy    => $"Approved by {toLabel}.",
                ChainEdgeTypes.PostedTo      => $"Posted to {toType.ToLower()} {toLabel}.",
                ChainEdgeTypes.InvoicesFor   => $"Invoices for {toType.ToLower()} {toLabel}.",
                ChainEdgeTypes.RevisionOf    => $"Revision of {toLabel}.",
                _                            => $"Linked to {toType.ToLower()} {toLabel}.",
            };
        }

        public sealed class ReceiptOption
        {
            public int Id { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public string? VendorName { get; set; }
            public string? PurchaseOrderNumber { get; set; }
            public System.DateTime ReceiptDate { get; set; }
        }

        public sealed class NarrationStep
        {
            public int StepNumber { get; set; }
            public string EdgeType { get; set; } = "";
            public string FromLabel { get; set; } = "";
            public string ToLabel { get; set; } = "";
            public string ToType { get; set; } = "";
            public string Sentence { get; set; } = "";
            public int Depth { get; set; }
        }
    }
}
