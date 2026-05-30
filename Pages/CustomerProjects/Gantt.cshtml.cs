// Theme B9 Wave 3 PR-9 (CLOSES B9 Wave 3) — Project Gantt + critical path.
// Read-only: injects IProjectScheduleService (ADR-025) and hands its Gantt DTO
// to the server-rendered SVG partial.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Authorize]
public sealed class GanttModel : PageModel
{
    private readonly IProjectScheduleService _schedule;

    public GanttModel(IProjectScheduleService schedule) => _schedule = schedule;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public ProjectGanttView? Gantt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id <= 0) { ErrorMessage = "Invalid project id."; return Page(); }

        var result = await _schedule.GetGanttAsync(Id, ct);
        if (result.IsFailure) { ErrorMessage = result.Error; return Page(); }

        Gantt = result.Value;
        return Page();
    }
}
