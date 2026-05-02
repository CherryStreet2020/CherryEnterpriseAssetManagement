using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages
{
    public class ModuleDisabledModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? Module { get; set; }

        public string ModuleName => Module ?? "This";

        public void OnGet()
        {
        }
    }
}
