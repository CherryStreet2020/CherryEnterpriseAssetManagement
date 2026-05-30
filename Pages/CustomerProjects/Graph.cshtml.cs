// Theme B9 Wave 1 PR-3 (CLOSES B9 Wave 1) — Project Lifecycle Graph page.
// Read-only: injects IProjectGraphService (a service, not AppDbContext — ADR-025)
// and hands its DTO to the reusable cytoscape graph partial.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Authorize]
public sealed class GraphModel : PageModel
{
    private readonly IProjectGraphService _graph;

    public GraphModel(IProjectGraphService graph) => _graph = graph;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public ProjectGraph? Graph { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id <= 0) { ErrorMessage = "Invalid project id."; return Page(); }

        var result = await _graph.GetGraphAsync(Id, ct);
        if (result.IsFailure) { ErrorMessage = result.Error; return Page(); }

        Graph = result.Value;
        return Page();
    }
}
