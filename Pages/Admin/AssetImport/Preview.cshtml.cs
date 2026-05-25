using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.AssetImport;
using Abs.FixedAssets.Services.AssetImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin.AssetImport
{
    [Authorize(Roles = "Admin")]
    public class PreviewModel : PageModel
    {
        private readonly IAssetImportService _service;

        public PreviewModel(IAssetImportService service)
        {
            _service = service;
        }

        public AssetImportBatch? Batch { get; private set; }
        public IReadOnlyList<AssetImportRow> Rows { get; private set; } = System.Array.Empty<AssetImportRow>();

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Tab { get; set; } = "all";

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            Batch = await _service.GetBatchAsync(Id, ct);
            if (Batch is null) return NotFound();
            Rows = await _service.GetRowsAsync(Id, ct);
            return Page();
        }

        public async Task<IActionResult> OnPostRevalidateAsync(CancellationToken ct)
        {
            await _service.ValidateRowsAsync(Id, ct);
            TempData["Success"] = "Re-validated. Counts refreshed.";
            return Redirect($"/Admin/AssetImport/Preview/{Id}");
        }

        public async Task<IActionResult> OnPostCommitAsync(CancellationToken ct)
        {
            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                var batch = await _service.CommitBatchAsync(Id, userId, username, ct);
                TempData["Success"] = $"Committed {batch.ValidRowCount} rows as new Assets. Batch is now read-only.";
                return Redirect($"/Admin/AssetImport/Detail/{Id}");
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Commit failed: {ex.Message}";
                return Redirect($"/Admin/AssetImport/Preview/{Id}");
            }
        }

        public async Task<IActionResult> OnPostDiscardAsync(CancellationToken ct)
        {
            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                await _service.DiscardBatchAsync(Id, userId, username, ct);
                TempData["Success"] = "Batch discarded.";
                return Redirect("/Admin/AssetImport");
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Discard failed: {ex.Message}";
                return Redirect($"/Admin/AssetImport/Preview/{Id}");
            }
        }

        private int ResolveUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }
    }
}
