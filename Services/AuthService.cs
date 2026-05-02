// TENANT SCOPING EXCEPTION: AuthService must operate cross-tenant for login/user management.
// User lookup during authentication cannot be scoped to a single company because the
// tenant context has not been established yet at that point in the request pipeline.
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var upperUsername = username.ToUpperInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == upperUsername && u.IsActive);

        if (user == null)
            return null;

        var passwordHash = HashPassword(password);
        if (user.PasswordHash != passwordHash)
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return user;
    }
    
    public static string NormalizeRole(string role)
    {
        return role.ToUpperInvariant() switch
        {
            "ADMIN" => "Admin",
            "ACCOUNTANT" => "Accountant",
            "VIEWER" => "Viewer",
            _ => role
        };
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _db.Users.FindAsync(id);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<User> CreateUserAsync(string username, string password, string role, string? fullName = null, string? email = null, int? assignedCompanyId = null)
    {
        var user = new User
        {
            Username = username.ToUpperInvariant(),
            PasswordHash = HashPassword(password),
            Role = role,
            FullName = fullName,
            Email = email,
            AssignedCompanyId = assignedCompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SeedDefaultUserAsync()
    {
        if (!await _db.Users.AnyAsync())
        {
            var pwhUsa = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyCode == "PWH-USA");
            var pwhCan = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyCode == "PWH-CAN");

            await CreateUserAsync("admin", "admin123", UserRoles.Admin, "System Administrator", "admin@absmachining.com");
            await CreateUserAsync("accountant", "acc123", UserRoles.Accountant, "Accountant User", "accounting@absmachining.com", pwhUsa?.Id);
            await CreateUserAsync("viewer", "view123", UserRoles.Viewer, "Read-Only User", "viewer@absmachining.com", pwhCan?.Id);
        }
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "ABS_SALT_2026"));
        return Convert.ToBase64String(bytes);
    }
}
