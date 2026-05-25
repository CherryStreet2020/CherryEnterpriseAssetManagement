using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Quality;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Quality;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Quality.Fai
{
    [Authorize]
    [Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Reads Items + CustomerProjects + Customers for dropdown population only. All mutations route through IFaiService.")]
    public class CreateModel : PageModel
    {
        private readonly IFaiService _fai;
        private readonly ITenantContext _tenant;
        private readonly AppDbContext _db;

        public CreateModel(IFaiService fai, ITenantContext tenant, AppDbContext db)
        {
            _fai = fai;
            _tenant = tenant;
            _db = db;
        }

        public int CompanyId { get; private set; }

        public List<SelectOption> ItemOptions { get; private set; } = new();
        public List<SelectOption> CustomerOptions { get; private set; } = new();
        public List<SelectOption> CustomerProjectOptions { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? CustomerProjectId { get; set; }

        [BindProperty] public int? SelectedItemId { get; set; }
        [BindProperty] public int? SelectedCustomerId { get; set; }
        [BindProperty] public string PartNumberSnapshot { get; set; } = string.Empty;
        [BindProperty] public string? PartNameSnapshot { get; set; }
        [BindProperty] public string? DrawingNumberSnapshot { get; set; }
        [BindProperty] public string? DrawingRevSnapshot { get; set; }
        [BindProperty] public FaiType Type { get; set; } = FaiType.Full;
        [BindProperty] public FaiPartType PartType { get; set; } = FaiPartType.Detail;
        [BindProperty] public FaiReason Reason { get; set; } = FaiReason.NewPart;
        [BindProperty] public string? ReasonText { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            CompanyId = _tenant.CompanyId ?? 1;
            await LoadDropdownsAsync(ct);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            CompanyId = _tenant.CompanyId ?? 1;
            await LoadDropdownsAsync(ct);

            if (!SelectedItemId.HasValue)
            {
                TempData["Error"] = "Select an Item.";
                return Page();
            }
            if (string.IsNullOrWhiteSpace(PartNumberSnapshot))
            {
                TempData["Error"] = "Part Number Snapshot is required.";
                return Page();
            }

            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                var fai = await _fai.CreateAsync(
                    new FaiCreateRequest(
                        CompanyId, _tenant.TenantId,
                        SelectedItemId.Value, CustomerProjectId, SelectedCustomerId,
                        PartNumberSnapshot, PartNameSnapshot, DrawingNumberSnapshot, DrawingRevSnapshot,
                        Type, PartType, Reason, ReasonText),
                    userId, username, ct);
                TempData["Success"] = $"Created {fai.FaiNumber}.";
                return Redirect($"/Quality/Fai/Detail/{fai.Id}");
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Create failed: {ex.Message}";
                return Page();
            }
        }

        private async Task LoadDropdownsAsync(CancellationToken ct)
        {
            ItemOptions = await _db.Items
                .Where(i => i.CompanyId == CompanyId)
                .OrderBy(i => i.PartNumber)
                .Take(500)
                .Select(i => new SelectOption(i.Id, $"{i.PartNumber} — {i.Description}"))
                .ToListAsync(ct);
            CustomerOptions = await _db.Customers
                .Where(c => c.CompanyId == CompanyId)
                .OrderBy(c => c.Name)
                .Take(500)
                .Select(c => new SelectOption(c.Id, c.Name))
                .ToListAsync(ct);
            CustomerProjectOptions = await _db.CustomerProjects
                .Where(p => p.CompanyId == CompanyId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new SelectOption((int)p.Id, $"{p.Code} — {p.Name}"))
                .ToListAsync(ct);
        }

        private int ResolveUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }

        public sealed record SelectOption(int Id, string Label);
    }
}
