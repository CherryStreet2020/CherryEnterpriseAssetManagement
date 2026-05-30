// Theme B9 Wave 1 PR-1 (2026-05-30) — Project Command Center page.
// THE BIC money-shot surface. Read-only: injects IProjectCommandCenterService
// (a service, not AppDbContext — ADR-025) and maps its DTO to the view.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.CustomerProjects;

[Authorize]
public sealed class CommandCenterModel : PageModel
{
    private readonly IProjectCommandCenterService _commandCenter;
    private readonly IProjectPromiseService _promise;

    public CommandCenterModel(IProjectCommandCenterService commandCenter, IProjectPromiseService promise)
    {
        _commandCenter = commandCenter;
        _promise = promise;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public ProjectCommandCenterData? Data { get; private set; }
    public ProjectPromiseAssessment? Promise { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id <= 0) { ErrorMessage = "Invalid project id."; return Page(); }

        var result = await _commandCenter.GetCommandCenterAsync(Id, ct);
        if (result.IsFailure) { ErrorMessage = result.Error; return Page(); }

        Data = result.Value;

        // B9 PR-2 — the "can we still hit the promise?" verdict (best-effort).
        var promise = await _promise.EvaluateAsync(Id, ct);
        if (promise.IsSuccess) Promise = promise.Value;

        return Page();
    }
}
