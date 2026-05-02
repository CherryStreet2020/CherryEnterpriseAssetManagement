using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Revisions;
using Microsoft.AspNetCore.Authorization;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class PMTemplateEditModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPMTemplateRevisionService _revisionService;
        private readonly ITenantContext _tenantContext;

        public PMTemplateEditModel(AppDbContext db, IPMTemplateRevisionService revisionService, ITenantContext tenantContext)
        {
            _db = db;
            _revisionService = revisionService;
            _tenantContext = tenantContext;
        }

        [BindProperty]
        public PMTemplate Template { get; set; } = new();

        public PMTemplateRevision? CurrentRevision { get; set; }
        public IList<PMTemplateRevision> Revisions { get; set; } = new List<PMTemplateRevision>();

        public bool IsCreateMode => Template.Id == 0;
        public string PageTitle => IsCreateMode ? "New PM Template" : "Edit PM Template";

        public string? SuccessMessage => TempData["Success"]?.ToString();
        public string? ErrorMessage => TempData["Error"]?.ToString();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id.HasValue)
            {
                var template = await _db.PMTemplates
                    .Include(t => t.CurrentReleasedRevision)
                    .Where(t => (t.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0)) && t.Id == id.Value)
                    .FirstOrDefaultAsync();
                if (template == null)
                {
                    TempData["Error"] = "Template not found.";
                    return RedirectToPage("PMTemplates");
                }
                Template = template;
                CurrentRevision = template.CurrentReleasedRevision;
                Revisions = await _revisionService.GetRevisionHistoryAsync(template.Id);
            }
            else
            {
                Template = new PMTemplate
                {
                    Priority = PMPriority.Medium,
                    TriggerType = PMTriggerType.Calendar,
                    CalendarInterval = RecurrenceType.Monthly,
                    CalendarIntervalValue = 1,
                    EstimatedHours = 1,
                    IsActive = true
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadRevisionsAsync();
                return Page();
            }

            if (IsCreateMode)
            {
                if (await _db.PMTemplates.AnyAsync(t => t.Code == Template.Code))
                {
                    ModelState.AddModelError("Template.Code", $"A template with code {Template.Code} already exists.");
                    return Page();
                }

                Template.CreatedAt = DateTime.UtcNow;
                Template.CreatedBy = User.Identity?.Name ?? "system";
                Template.CompanyId = _tenantContext.CompanyId;
                _db.PMTemplates.Add(Template);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"Template {Template.Code} created successfully.";
            }
            else
            {
                var existing = await _db.PMTemplates
                    .Where(t => (t.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0)) && t.Id == Template.Id)
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    TempData["Error"] = "Template not found.";
                    return RedirectToPage("PMTemplates");
                }

                if (await _db.PMTemplates.AnyAsync(t => t.Code == Template.Code && t.Id != Template.Id))
                {
                    ModelState.AddModelError("Template.Code", $"A template with code {Template.Code} already exists.");
                    await LoadRevisionsAsync();
                    return Page();
                }

                existing.Code = Template.Code;
                existing.Name = Template.Name;
                existing.Description = Template.Description;
                existing.Type = Template.Type;
                existing.Priority = Template.Priority;
                existing.TriggerType = Template.TriggerType;
                existing.CalendarInterval = Template.CalendarInterval;
                existing.CalendarIntervalValue = Template.CalendarIntervalValue;
                existing.MeterType = Template.MeterType;
                existing.MeterInterval = Template.MeterInterval;
                existing.EstimatedHours = Template.EstimatedHours;
                existing.EstimatedLaborCost = Template.EstimatedLaborCost;
                existing.EstimatedPartsCost = Template.EstimatedPartsCost;
                existing.RequiresShutdown = Template.RequiresShutdown;
                existing.RequiresLOTO = Template.RequiresLOTO;
                existing.SafetyInstructions = Template.SafetyInstructions;
                existing.IsActive = Template.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = User.Identity?.Name ?? "system";

                await _db.SaveChangesAsync();
                TempData["Success"] = $"Template {Template.Code} updated successfully.";
            }

            return RedirectToPage("PMTemplates");
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var template = await _db.PMTemplates
                .Where(t => (t.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0)) && t.Id == Template.Id)
                .FirstOrDefaultAsync();
            if (template == null)
            {
                TempData["Error"] = "Template not found.";
                return RedirectToPage("PMTemplates");
            }

            var code = template.Code;
            _db.PMTemplates.Remove(template);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Template {code} deleted successfully.";
            return RedirectToPage("PMTemplates");
        }

        public async Task<IActionResult> OnPostCreateDraftAsync(int templateId)
        {
            try
            {
                var draft = await _revisionService.CreateDraftFromTemplateAsync(templateId, "New draft revision", User.Identity?.Name);
                TempData["Success"] = $"Draft revision {draft.RevisionCode} created.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToPage(new { id = templateId });
        }

        public async Task<IActionResult> OnPostReleaseDraftAsync(int revisionId)
        {
            try
            {
                var revision = await _revisionService.GetRevisionByIdAsync(revisionId);
                if (revision == null)
                {
                    TempData["Error"] = "Revision not found.";
                    return RedirectToPage();
                }

                var released = await _revisionService.ReleaseRevisionAsync(revisionId, User.Identity?.Name);
                TempData["Success"] = $"Revision {released.RevisionCode} released successfully.";
                return RedirectToPage(new { id = revision.PMTemplateId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteDraftAsync(int revisionId)
        {
            try
            {
                var revision = await _revisionService.GetRevisionByIdAsync(revisionId);
                if (revision == null)
                {
                    TempData["Error"] = "Revision not found.";
                    return RedirectToPage();
                }

                var templateId = revision.PMTemplateId;
                await _revisionService.DeleteDraftRevisionAsync(revisionId);
                TempData["Success"] = "Draft revision deleted.";
                return RedirectToPage(new { id = templateId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToPage();
            }
        }

        private async Task LoadRevisionsAsync()
        {
            if (Template.Id > 0)
            {
                var template = await _db.PMTemplates
                    .Include(t => t.CurrentReleasedRevision)
                    .FirstOrDefaultAsync(t => t.Id == Template.Id);
                if (template != null)
                {
                    CurrentRevision = template.CurrentReleasedRevision;
                    Revisions = await _revisionService.GetRevisionHistoryAsync(template.Id);
                }
            }
        }
    }
}
