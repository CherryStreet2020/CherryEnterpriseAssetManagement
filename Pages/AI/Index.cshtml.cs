using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.AI
{
    public class IndexModel : PageModel
    {
        private readonly AiAssistantService _aiService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public IndexModel(AiAssistantService aiService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _aiService = aiService;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("ai"))
                return RedirectToPage("/ModuleDisabled", new { module = "AI" });


            return Page();
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] AskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
            {
                return new JsonResult(new { answer = "Please ask a question." });
            }

            var answer = await _aiService.AskQuestionAsync(request.Question, request.History);
            return new JsonResult(new { answer });
        }
    }

    public class AskRequest
    {
        public string? Question { get; set; }
        public string? History { get; set; }
    }
}
