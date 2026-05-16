using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Approvals
{
    // Sprint 2 PR #115 — User-facing pending-approval queue.
    // Hides rows the user has already approved or where their role
    // doesn't match the workflow's ApproverRoles.
    [Authorize(Roles = "Admin,Manager,Director,Accountant,Finance")]
    public class PendingModel : PageModel
    {
        private readonly IApprovalService _approvals;

        public PendingModel(IApprovalService approvals)
        {
            _approvals = approvals;
        }

        public IReadOnlyList<PendingApprovalRow> Rows { get; private set; } = new List<PendingApprovalRow>();
        public string CurrentUserRoles { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var username = User.Identity?.Name ?? "unknown";
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            CurrentUserRoles = string.Join(", ", roles);
            Rows = await _approvals.GetPendingForUserAsync(username, roles, companyIdFilter: null);
            return Page();
        }
    }
}
