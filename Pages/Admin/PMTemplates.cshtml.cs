using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Authorization;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class PMTemplatesModel : PageModel
    {
        private readonly AppDbContext _db;

        public PMTemplatesModel(AppDbContext db)
        {
            _db = db;
        }

        public List<PMTemplate> Templates { get; set; } = new();

        public string? SuccessMessage => TempData["Success"]?.ToString();
        public string? ErrorMessage => TempData["Error"]?.ToString();

        public async Task OnGetAsync()
        {
            Templates = await _db.PMTemplates
                .Include(t => t.CurrentReleasedRevision)
                .OrderBy(t => t.Code)
                .ToListAsync();
        }
    }
}
