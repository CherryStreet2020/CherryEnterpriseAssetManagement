using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TechniciansModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public TechniciansModel(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<Technician> Technicians { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        private List<int> VisibleCompanyIds => _tenantContext.VisibleCompanyIds;

        private IQueryable<Technician> ScopedTechnicians =>
            _context.Technicians.Where(t => t.CompanyId == null || VisibleCompanyIds.Contains(t.CompanyId ?? 0));

        public async Task OnGetAsync()
        {
            Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string name,
            string? email,
            string? phone,
            string? specialty,
            string? department,
            decimal? hourlyRate,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Name is required.";
                Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
                return Page();
            }

            var technician = new Technician
            {
                Name = name,
                Email = email,
                Phone = phone,
                Specialty = specialty,
                Department = department,
                HourlyRate = hourlyRate,
                Notes = notes,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                CompanyId = _tenantContext.CompanyId,
                SiteId = _tenantContext.SiteId,
                TenantId = _tenantContext.TenantId
            };

            _context.Technicians.Add(technician);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Technician '{name}' created successfully.";
            Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int technicianId,
            string name,
            string? email,
            string? phone,
            string? specialty,
            string? department,
            decimal? hourlyRate,
            string? notes,
            bool active)
        {
            var technician = await ScopedTechnicians
                .Where(t => t.Id == technicianId)
                .FirstOrDefaultAsync();
            if (technician == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Name is required.";
                Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
                return Page();
            }

            technician.Name = name;
            technician.Email = email;
            technician.Phone = phone;
            technician.Specialty = specialty;
            technician.Department = department;
            technician.HourlyRate = hourlyRate;
            technician.Notes = notes;
            technician.Active = active;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Technician '{technician.Name}' updated successfully.";
            Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int technicianId)
        {
            var technician = await ScopedTechnicians
                .Where(t => t.Id == technicianId)
                .FirstOrDefaultAsync();
            if (technician == null)
                return NotFound();

            technician.Active = !technician.Active;
            await _context.SaveChangesAsync();

            Technicians = await ScopedTechnicians.OrderBy(t => t.Name).ToListAsync();
            return Page();
        }
    }
}
