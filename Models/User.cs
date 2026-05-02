using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

[Table("Users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(200)]
    public string? FullName { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = "Viewer";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    [StringLength(5)]
    public string Language { get; set; } = "en";

    [StringLength(50)]
    public string? TimeZone { get; set; }

    public int? CompanyId { get; set; }

    public int? AssignedCompanyId { get; set; }
    public Company? AssignedCompany { get; set; }

    public int? AssignedSiteId { get; set; }
    public Site? AssignedSite { get; set; }

    public bool MustChangePassword { get; set; } = false;

    public DateTime? PasswordChangedAt { get; set; }
}

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Accountant = "Accountant";
    public const string Viewer = "Viewer";

    public static readonly string[] All = { Admin, Accountant, Viewer };
}
