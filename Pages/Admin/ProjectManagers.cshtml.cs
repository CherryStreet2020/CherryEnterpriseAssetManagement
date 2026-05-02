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
    public class ProjectManagersModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public ProjectManagersModel(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<ProjectManager> ProjectManagers { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string name,
            string? email,
            string? phone,
            string? department,
            string? title,
            string? notes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Name is required.";
                ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
                return Page();
            }

            var projectManager = new ProjectManager
            {
                Name = name,
                Email = email,
                Phone = phone,
                Department = department,
                Title = title,
                Notes = notes,
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectManagers.Add(projectManager);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Project Manager '{name}' created successfully.";
            ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int projectManagerId,
            string name,
            string? email,
            string? phone,
            string? department,
            string? title,
            string? notes,
            bool active)
        {
            var projectManager = await _context.ProjectManagers
                .Where(pm => pm.Id == projectManagerId)
                .FirstOrDefaultAsync();
            if (projectManager == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Name is required.";
                ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
                return Page();
            }

            projectManager.Name = name;
            projectManager.Email = email;
            projectManager.Phone = phone;
            projectManager.Department = department;
            projectManager.Title = title;
            projectManager.Notes = notes;
            projectManager.Active = active;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Project Manager '{name}' updated successfully.";
            ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int projectManagerId)
        {
            var projectManager = await _context.ProjectManagers
                .Where(pm => pm.Id == projectManagerId)
                .FirstOrDefaultAsync();
            if (projectManager == null)
                return NotFound();

            projectManager.Active = !projectManager.Active;
            await _context.SaveChangesAsync();

            ProjectManagers = await _context.ProjectManagers.OrderBy(pm => pm.Name).ToListAsync();
            return Page();
        }
    }
}
