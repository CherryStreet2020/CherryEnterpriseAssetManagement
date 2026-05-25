using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Quality;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Quality;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Quality.Fai
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IFaiService _fai;
        private readonly ITenantContext _tenant;

        public IndexModel(IFaiService fai, ITenantContext tenant)
        {
            _fai = fai;
            _tenant = tenant;
        }

        public IReadOnlyList<FaiReport> Reports { get; private set; } = System.Array.Empty<FaiReport>();
        public int CompanyId { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? CustomerProjectId { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            CompanyId = _tenant.CompanyId ?? 1;
            Reports = await _fai.ListAsync(CompanyId, CustomerProjectId, ct);
            return Page();
        }
    }
}
