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
    public class DetailModel : PageModel
    {
        private readonly IAssetImportService _service;

        public DetailModel(IAssetImportService service)
        {
            _service = service;
        }

        public AssetImportBatch? Batch { get; private set; }
        public IReadOnlyList<AssetImportRow> Rows { get; private set; } = System.Array.Empty<AssetImportRow>();

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            Batch = await _service.GetBatchAsync(Id, ct);
            if (Batch is null) return NotFound();
            Rows = await _service.GetRowsAsync(Id, ct);
            return Page();
        }
    }
}
