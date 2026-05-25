using System.Security.Claims;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.AssetImport;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.AssetImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin.AssetImport
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly IAssetImportService _service;
        private readonly ITenantContext _tenant;

        public IndexModel(IAssetImportService service, ITenantContext tenant)
        {
            _service = service;
            _tenant = tenant;
        }

        public IReadOnlyList<AssetImportBatch> RecentBatches { get; private set; } = System.Array.Empty<AssetImportBatch>();
        public AssetImportKpis Kpis { get; private set; } = new AssetImportKpis(0, 0, 0, 0);
        public int CompanyId { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            CompanyId = _tenant.CompanyId ?? 1;
            RecentBatches = await _service.ListRecentAsync(CompanyId, 50, ct);
            Kpis = await _service.GetKpisAsync(CompanyId, ct);
            return Page();
        }

        public IActionResult OnGetTemplate()
        {
            var bytes = _service.GenerateTemplate();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "AssetImport-Template.xlsx");
        }
    }
}
