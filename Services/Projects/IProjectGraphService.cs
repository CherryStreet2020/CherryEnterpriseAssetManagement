// Theme B9 Wave 1 PR-3 (2026-05-30, CLOSES B9 Wave 1) — IProjectGraphService.
//
// THE project lifecycle graph — the EVS money-shot that puts the whole
// Quote → Project → WBS/Phase → Job → PO → Receipt → Cost → Billing → Acceptance
// spine on ONE clickable canvas. No incumbent (SAP PS / Primavera / MSFT Project)
// shows commercial + execution + financial lineage as a single connected graph.
//
// v1 renders the nodes the current substrate can populate truthfully —
// Project (root), the WBS/ProjectPhase tree, the linked ProductionOrder jobs
// (nested under their phase when ProjectPhaseId is set), and an EVM/Cost node
// when a rollup exists — and shows the future-wave stages (Quote, Purchasing,
// Receipt, Billing, Acceptance) as DIMMED placeholders tagged with the wave
// that lights them up. Read-only; tenant-scoped on every read (ADR-025: a
// service, never AppDbContext-in-the-PageModel).
//
// Powers the /CustomerProjects/Graph/{id} page (reuses the cytoscape.js + dagre
// render pattern from _ChainOfCustodyGraph) and the Cherry Bar `ShowProjectGraph`
// voice intent (narrates the lifecycle shape + deep-links to the page).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Projects;

/// <summary>The nine lifecycle stages, left-to-right. Ordinal == pipeline order.</summary>
public enum ProjectGraphStage
{
    Quote = 0,       // commercial quote (Wave 2)
    Project = 1,     // the CustomerProject root (today)
    Wbs = 2,         // WBS / ProjectPhase tree (today)
    Job = 3,         // linked ProductionOrder execution (today)
    Purchasing = 4,  // project procurement / PO commitments (Wave 4)
    Receipt = 5,     // material receipts against the project (Wave 4)
    Cost = 6,        // EVM / cost rollup (today, when a rollup exists)
    Billing = 7,     // billing schedule / invoices (Wave 5)
    Acceptance = 8,  // customer acceptance / closeout (Wave 6)
}

/// <summary>Whether a node is live data, a built-but-empty stage, or a future wave.</summary>
public enum ProjectGraphNodeState
{
    Present = 0,  // real row(s) exist — render lit
    Empty = 1,    // stage is built but this project has no rows — render lit but muted
    Future = 2,   // the wave that fills this stage hasn't shipped — render dimmed
}

/// <summary>Visual tone for a Present node (drives node color in the render).</summary>
public enum ProjectGraphTone
{
    Neutral = 0,
    Good = 1,     // on-track / healthy (released, in-progress, positive margin)
    Warning = 2,  // at-risk (not released, pending, behind)
    Bad = 3,      // blocked / on-hold / over-budget
    Done = 4,     // completed / closed
}

/// <summary>
/// One node in the lifecycle graph. Edges are derived from <see cref="ParentId"/>
/// (a node with a null parent is a graph root), exactly like the chain-of-custody
/// renderer derives edges from each hop's IncomingFromNodeId.
/// </summary>
public sealed record ProjectGraphNode(
    string Id,
    ProjectGraphStage Stage,
    ProjectGraphNodeState State,
    ProjectGraphTone Tone,
    string Label,
    string? Sublabel,
    string? DeepLinkHref,
    string? WaveLabel,   // e.g. "Wave 4" — only set when State == Future
    string? ParentId);

public sealed record ProjectGraph(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    string Headline,                       // one-line spoken/printed lifecycle summary
    int PhaseCount,
    int JobCount,
    IReadOnlyList<ProjectGraphNode> Nodes);

public interface IProjectGraphService
{
    /// <summary>Assemble the lifecycle graph for a project (tenant-scoped, read-only).</summary>
    Task<Result<ProjectGraph>> GetGraphAsync(int customerProjectId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a project by free-text ref (numeric Id OR project Code, exact→prefix,
    /// tenant-scoped) then assemble the graph. For the `ShowProjectGraph` voice intent.
    /// </summary>
    Task<Result<ProjectGraph>> GetGraphByRefAsync(string projectRef, CancellationToken ct = default);
}
