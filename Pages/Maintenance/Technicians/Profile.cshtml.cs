using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Maintenance.Technicians;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public ProfileModel(AppDbContext db, ITenantContext tenantContext, IModuleGuardService moduleGuard)
    {
        _db = db;
        _tenantContext = tenantContext;
        _moduleGuard = moduleGuard;
    }

    public Technician Tech { get; set; } = null!;
    public List<TechnicianCertification> Certifications { get; set; } = new();
    public List<TechnicianSkill> Skills { get; set; } = new();
    public List<MaintenanceEvent> RecentWorkOrders { get; set; } = new();

    public int CompletedWoCount { get; set; }
    public int OpenWoCount { get; set; }
    public decimal TotalLaborHours { get; set; }
    public int ActiveCertCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "skills";

    private static readonly HashSet<string> ValidTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "skills", "history", "availability", "contact"
    };

    [BindProperty] public string EditName { get; set; } = "";
    [BindProperty] public string? EditTitle { get; set; }
    [BindProperty] public string? EditEmail { get; set; }
    [BindProperty] public string? EditPhone { get; set; }
    [BindProperty] public string? EditPrimaryCraft { get; set; }
    [BindProperty] public string? EditSecondaryCraft { get; set; }
    [BindProperty] public string? EditShiftPattern { get; set; }
    [BindProperty] public string? EditEmergencyContactName { get; set; }
    [BindProperty] public string? EditEmergencyContactPhone { get; set; }

    [BindProperty] public string CertName { get; set; } = "";
    [BindProperty] public string? CertNumber { get; set; }
    [BindProperty] public string? CertAuthority { get; set; }
    [BindProperty] public DateTime? CertIssueDate { get; set; }
    [BindProperty] public DateTime? CertExpirationDate { get; set; }
    [BindProperty] public bool CertIsRequired { get; set; }

    [BindProperty] public string SkillName { get; set; } = "";
    [BindProperty] public string? SkillCategory { get; set; }
    [BindProperty] public int SkillProficiency { get; set; }
    [BindProperty] public bool SkillIsCertified { get; set; }

    private async Task<Technician?> GetScopedTechAsync(int id)
    {
        return await _db.Technicians
            .Include(t => t.Company)
            .Include(t => t.Site)
            .Include(t => t.Certifications)
            .Include(t => t.Skills)
            .Where(t => t.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0))
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
            return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });

        var tech = await GetScopedTechAsync(id);
        if (tech == null)
            return NotFound();

        if (!ValidTabs.Contains(Tab))
            Tab = "skills";

        Tech = tech;
        Certifications = tech.Certifications?.OrderBy(c => c.Name).ToList() ?? new();
        Skills = tech.Skills?.OrderBy(s => s.SkillName).ToList() ?? new();

        ActiveCertCount = Certifications.Count(c => c.ExpirationDate == null || c.ExpirationDate > DateTime.UtcNow);

        RecentWorkOrders = await _db.MaintenanceEvents
            .Include(e => e.Asset)
            .Where(e => e.TechnicianId == id)
            .OrderByDescending(e => e.ScheduledDate)
            .Take(20)
            .ToListAsync();

        CompletedWoCount = RecentWorkOrders.Count(w => w.Status == MaintenanceStatus.Completed);
        OpenWoCount = RecentWorkOrders.Count(w => w.Status != MaintenanceStatus.Completed && w.Status != MaintenanceStatus.Cancelled);
        TotalLaborHours = RecentWorkOrders.Where(w => w.LaborHours.HasValue).Sum(w => w.LaborHours!.Value);

        return Page();
    }

    public async Task<IActionResult> OnPostEditProfileAsync(int id)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        tech.Name = EditName;
        tech.Title = EditTitle;
        tech.Email = EditEmail;
        tech.Phone = EditPhone;
        tech.PrimaryCraft = EditPrimaryCraft;
        tech.SecondaryCraft = EditSecondaryCraft;
        tech.ShiftPattern = EditShiftPattern;
        tech.EmergencyContactName = EditEmergencyContactName;
        tech.EmergencyContactPhone = EditEmergencyContactPhone;

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, tab = "contact" });
    }

    public async Task<IActionResult> OnPostAddCertificationAsync(int id)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        var cert = new TechnicianCertification
        {
            TechnicianId = id,
            Name = CertName,
            CertificateNumber = CertNumber,
            IssuingAuthority = CertAuthority,
            IssueDate = CertIssueDate,
            ExpirationDate = CertExpirationDate,
            IsRequired = CertIsRequired,
            TenantId = tech.TenantId
        };

        _db.TechnicianCertifications.Add(cert);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, tab = "skills" });
    }

    public async Task<IActionResult> OnPostRemoveCertificationAsync(int id, int certId)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        var cert = await _db.TechnicianCertifications.FirstOrDefaultAsync(c => c.Id == certId && c.TechnicianId == id);
        if (cert != null)
        {
            _db.TechnicianCertifications.Remove(cert);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id, tab = "skills" });
    }

    public async Task<IActionResult> OnPostAddSkillAsync(int id)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        var skill = new TechnicianSkill
        {
            TechnicianId = id,
            SkillName = SkillName,
            Category = SkillCategory,
            ProficiencyLevel = SkillProficiency,
            IsCertified = SkillIsCertified,
            LastAssessedDate = DateTime.UtcNow,
            TenantId = tech.TenantId
        };

        _db.TechnicianSkills.Add(skill);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, tab = "skills" });
    }

    public async Task<IActionResult> OnPostRemoveSkillAsync(int id, int skillId)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        var skill = await _db.TechnicianSkills.FirstOrDefaultAsync(s => s.Id == skillId && s.TechnicianId == id);
        if (skill != null)
        {
            _db.TechnicianSkills.Remove(skill);
            await _db.SaveChangesAsync();
        }
        return RedirectToPage(new { id, tab = "skills" });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        tech.Active = !tech.Active;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUploadPhotoAsync(int id, IFormFile? photo)
    {
        if (photo == null || photo.Length == 0)
            return RedirectToPage(new { id });

        var tech = await GetScopedTechAsync(id);
        if (tech == null) return NotFound();

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(photo.ContentType) || photo.Length > 5 * 1024 * 1024)
            return RedirectToPage(new { id });

        var uploadsDir = Path.Combine("wwwroot", "uploads", "technicians");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(photo.FileName);
        var fileName = $"tech_{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        tech.PhotoPath = $"/uploads/technicians/{fileName}";
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
}
