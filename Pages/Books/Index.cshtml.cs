using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Books
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext db,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<Row> Items { get; set; } = new();

        public class Row
        {
            public int Id { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public DepreciationMethod Method { get; set; }
            public DepreciationConvention Convention { get; set; }
            public int? UsefulLifeOverrideMonths { get; set; }
            public BookType BookType { get; set; }
            public bool IsActive { get; set; }
            public bool IsPrimaryBook { get; set; }
            public bool HasGl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });


            var visibleIds = _tenantContext.VisibleCompanyIds;

            Items = await _db.Books
                .Where(b => visibleIds.Contains(b.CompanyId ?? 0))
                .Select(b => new Row
                {
                    Id = b.Id,
                    Code = b.Code,
                    Name = b.Name,
                    Method = b.Method,
                    Convention = b.Convention,
                    UsefulLifeOverrideMonths = b.UsefulLifeOverrideMonths,
                    BookType = b.BookType,
                    IsActive = b.IsActive,
                    IsPrimaryBook = b.IsPrimaryBook,
                    HasGl = _db.BookGlAccounts.Any(g => g.BookId == b.Id)
                })
                .OrderBy(r => r.Code)
                .ToListAsync();
        
            return Page();
        }
    }
}
