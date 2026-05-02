using Abs.FixedAssets.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ApprovalsModel : PageModel
    {
        private readonly AppDbContext _context;

        public ApprovalsModel(AppDbContext context)
        {
            _context = context;
        }

        public decimal DisposalThreshold { get; set; } = 10000;
        public decimal TransferThreshold { get; set; } = 25000;
        public decimal ImprovementThreshold { get; set; } = 5000;
        public bool RequireManagerApproval { get; set; } = true;
        public bool RequireFinanceApproval { get; set; } = true;

        public void OnGet()
        {
        }
    }
}
