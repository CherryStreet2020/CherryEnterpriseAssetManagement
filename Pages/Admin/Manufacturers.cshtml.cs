using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManufacturersModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public ManufacturersModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Manufacturer> Manufacturers { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", null, "");
            Manufacturers = await _context.Manufacturers
                .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string name,
            string? website,
            string? country,
            string? contactName,
            string? contactEmail,
            string? contactPhone,
            string? address,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Manufacturer name is required.";
                Manufacturers = await _context.Manufacturers
                    .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                    .OrderBy(m => m.Name)
                    .ToListAsync();
                return Page();
            }

            var existingManufacturer = await _context.Manufacturers
                .Where(m => m.Name == name && (m.TenantId == _tenantContext.TenantId || m.TenantId == null))
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync();
            if (existingManufacturer != null)
            {
                ErrorMessage = "A manufacturer with this name already exists.";
                Manufacturers = await _context.Manufacturers
                    .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                    .OrderBy(m => m.Name)
                    .ToListAsync();
                return Page();
            }

            var manufacturer = new Manufacturer
            {
                Name = name,
                Website = website,
                Country = country,
                ContactName = contactName,
                ContactEmail = contactEmail,
                ContactPhone = contactPhone,
                Address = address,
                Notes = notes,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                TenantId = _tenantContext.TenantId
            };

            _context.Manufacturers.Add(manufacturer);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Manufacturer '{name}' created successfully.";
            Manufacturers = await _context.Manufacturers
                .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int manufacturerId,
            string name,
            string? website,
            string? country,
            string? contactName,
            string? contactEmail,
            string? contactPhone,
            string? address,
            string? notes,
            bool active)
        {
            var manufacturer = await _context.Manufacturers
                .Where(m => (m.TenantId == _tenantContext.TenantId || m.TenantId == null) && m.Id == manufacturerId)
                .FirstOrDefaultAsync();
            if (manufacturer == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Manufacturer name is required.";
                Manufacturers = await _context.Manufacturers
                    .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                    .OrderBy(m => m.Name)
                    .ToListAsync();
                return Page();
            }

            manufacturer.Name = name;
            manufacturer.Website = website;
            manufacturer.Country = country;
            manufacturer.ContactName = contactName;
            manufacturer.ContactEmail = contactEmail;
            manufacturer.ContactPhone = contactPhone;
            manufacturer.Address = address;
            manufacturer.Notes = notes;
            manufacturer.Active = active;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Manufacturer '{manufacturer.Name}' updated successfully.";
            Manufacturers = await _context.Manufacturers
                .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int manufacturerId)
        {
            var manufacturer = await _context.Manufacturers
                .Where(m => (m.TenantId == _tenantContext.TenantId || m.TenantId == null) && m.Id == manufacturerId)
                .FirstOrDefaultAsync();
            if (manufacturer == null)
                return NotFound();

            manufacturer.Active = !manufacturer.Active;
            await _context.SaveChangesAsync();

            Manufacturers = await _context.Manufacturers
                .Where(m => m.TenantId == _tenantContext.TenantId || m.TenantId == null)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return Page();
        }
    }
}
