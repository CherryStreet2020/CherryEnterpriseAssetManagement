using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Quality;
using Abs.FixedAssets.Services.Quality;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Quality.Fai
{
    [Authorize]
    public class DetailModel : PageModel
    {
        private readonly IFaiService _fai;

        public DetailModel(IFaiService fai)
        {
            _fai = fai;
        }

        public FaiReport? Report { get; private set; }
        public IReadOnlyList<FaiCharacteristic> Characteristics { get; private set; } = System.Array.Empty<FaiCharacteristic>();
        public IReadOnlyList<FaiProductAccountability> ProductAccountability { get; private set; } = System.Array.Empty<FaiProductAccountability>();

        [BindProperty(SupportsGet = true)]
        public long Id { get; set; }

        // Add-characteristic form fields
        [BindProperty] public string? CharBalloonNumber { get; set; }
        [BindProperty] public string? CharDescription { get; set; }
        [BindProperty] public decimal? CharNominalValue { get; set; }
        [BindProperty] public decimal? CharUpperTolerance { get; set; }
        [BindProperty] public decimal? CharLowerTolerance { get; set; }
        [BindProperty] public string? CharUnitOfMeasure { get; set; }
        [BindProperty] public decimal? CharActualResult { get; set; }
        [BindProperty] public string? CharActualText { get; set; }
        [BindProperty] public string? CharRequirementText { get; set; }
        [BindProperty] public FaiConformance CharConformance { get; set; } = FaiConformance.Conforms;
        [BindProperty] public string? CharInstrumentUsed { get; set; }
        [BindProperty] public string? CharNonConformanceNotes { get; set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            Report = await _fai.GetByIdAsync(Id, ct);
            if (Report is null) return NotFound();
            Characteristics = await _fai.GetCharacteristicsAsync(Id, ct);
            ProductAccountability = await _fai.GetProductAccountabilityAsync(Id, ct);
            return Page();
        }

        public async Task<IActionResult> OnPostAddCharacteristicAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(CharBalloonNumber) || string.IsNullOrWhiteSpace(CharDescription))
            {
                TempData["Error"] = "Balloon Number + Description are required.";
                return Redirect($"/Quality/Fai/Detail/{Id}");
            }

            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                await _fai.RecordCharacteristicAsync(Id, new FaiCharacteristic
                {
                    BalloonNumber = CharBalloonNumber!.Trim(),
                    CharacteristicDescription = CharDescription!.Trim(),
                    NominalValue = CharNominalValue,
                    UpperToleranceValue = CharUpperTolerance,
                    LowerToleranceValue = CharLowerTolerance,
                    UnitOfMeasure = CharUnitOfMeasure?.Trim(),
                    ActualResult = CharActualResult,
                    ActualText = CharActualText?.Trim(),
                    RequirementText = CharRequirementText?.Trim(),
                    Conformance = CharConformance,
                    InstrumentUsed = CharInstrumentUsed?.Trim(),
                    NonConformanceNotes = CharNonConformanceNotes?.Trim()
                }, userId, username, ct);
                TempData["Success"] = $"Added characteristic {CharBalloonNumber}.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Add failed: {ex.Message}";
            }
            return Redirect($"/Quality/Fai/Detail/{Id}");
        }

        public async Task<IActionResult> OnPostSubmitAsync(CancellationToken ct)
        {
            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                await _fai.SubmitAsync(Id, userId, username, ct);
                TempData["Success"] = "Submitted for approval.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Submit failed: {ex.Message}";
            }
            return Redirect($"/Quality/Fai/Detail/{Id}");
        }

        public async Task<IActionResult> OnPostApproveAsync(CancellationToken ct)
        {
            var userId = ResolveUserId();
            var username = User.Identity?.Name;
            try
            {
                await _fai.SignOffAsync(Id, userId, username, ct);
                TempData["Success"] = "Approved.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = $"Approve failed: {ex.Message}";
            }
            return Redirect($"/Quality/Fai/Detail/{Id}");
        }

        private int ResolveUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : 0;
        }
    }
}
