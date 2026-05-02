using System.Security.Cryptography;
using System.Text;
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
    public class UsersModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public UsersModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<User> Users { get; set; } = new();
        public List<SelectListItem> RoleOptions { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public List<Site> Sites { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            RoleOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "UserRole", null, "");
            Users = await _context.Users.Include(u => u.AssignedCompany).Include(u => u.AssignedSite).OrderBy(u => u.Username).ToListAsync();
            Companies = await _context.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            Sites = await _context.Sites.Where(s => s.Status == SiteStatus.Active).OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string username,
            string email,
            string fullName,
            string password,
            string role,
            int? assignedCompanyId,
            int? assignedSiteId)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Username and password are required.";
                Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
                return Page();
            }

            var existingUser = await _context.Users.Where(u => u.Username == username).OrderBy(u => u.Id).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                ErrorMessage = "A user with this username already exists.";
                Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
                return Page();
            }

            var user = new User
            {
                Username = username,
                Email = email,
                FullName = fullName,
                PasswordHash = HashPassword(password),
                Role = role,
                AssignedCompanyId = assignedCompanyId,
                AssignedSiteId = assignedSiteId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                MustChangePassword = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            SuccessMessage = $"User '{username}' created successfully.";
            Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int userId,
            string email,
            string fullName,
            string role,
            bool isActive,
            int? assignedCompanyId,
            int? assignedSiteId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            if (user == null)
                return NotFound();

            user.Email = email;
            user.FullName = fullName;
            user.Role = role;
            user.IsActive = isActive;
            user.AssignedCompanyId = assignedCompanyId;
            user.AssignedSiteId = assignedSiteId;

            await _context.SaveChangesAsync();

            SuccessMessage = $"User '{user.Username}' updated successfully.";
            Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(int userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ErrorMessage = "Password cannot be empty.";
                Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
                return Page();
            }

            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            if (user == null)
                return NotFound();

            user.PasswordHash = HashPassword(newPassword);
            user.MustChangePassword = true;
            user.PasswordChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Password reset for '{user.Username}'.";
            Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync();
            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();
            return Page();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
