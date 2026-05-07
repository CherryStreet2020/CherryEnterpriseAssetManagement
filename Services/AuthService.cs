// TENANT SCOPING EXCEPTION: AuthService must operate cross-tenant for login/user management.
// User lookup during authentication cannot be scoped to a single company because the
// tenant context has not been established yet at that point in the request pipeline.
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
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

    // ── Argon2id parameters (OWASP first-choice recommendation) ──────────
    // Memory: 64 MiB, Iterations: 3, Parallelism: 1. These settings target
    // ~250-500 ms on commodity x64 hardware in 2026 — slow enough to make
    // brute-force expensive, fast enough not to feel sluggish on login.
    // Adjust if benchmarks change; existing hashes embed their parameters
    // in the PHC string so older settings still verify.
    private const int Argon2idMemoryKb     = 64 * 1024;   // 64 MiB
    private const int Argon2idIterations   = 3;
    private const int Argon2idParallelism  = 1;
    private const int Argon2idSaltBytes    = 16;
    private const int Argon2idHashBytes    = 32;
    private const string PhcPrefix         = "$argon2id$";

    // Legacy SHA-256 salt used by the original AuthService.HashPassword.
    // Retained ONLY so VerifyPassword can recognize legacy hashes during
    // the rolling migration. New writes never use this.
    private const string LegacySha256Salt  = "ABS_SALT_2026";

    public async Task<User?> ValidateUserAsync(string username, string password)
    {
        var upperUsername = username.ToUpperInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == upperUsername && u.IsActive);

        if (user == null)
            return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        // Rolling migration: any successful login against a legacy SHA-256
        // hash (or against an Argon2id hash with stale parameters) gets
        // re-hashed with the current target and saved. After every active
        // user has logged in once, no SHA-256 hashes remain in the DB.
        if (NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = HashPassword(password);
            user.PasswordChangedAt = DateTime.UtcNow;
        }

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
        user.PasswordChangedAt = DateTime.UtcNow;
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

    // ─────────────────────────────────────────────────────────────────────
    // Password hashing
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hashes a password using Argon2id and returns a self-contained PHC
    /// string of the form
    ///   <c>$argon2id$v=19$m=&lt;mem&gt;,t=&lt;iter&gt;,p=&lt;par&gt;$&lt;salt-base64&gt;$&lt;hash-base64&gt;</c>.
    /// The salt is freshly generated per call from a CSPRNG; algorithm
    /// parameters are encoded in the string so future tuning doesn't break
    /// stored hashes.
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(Argon2idSaltBytes);
        var hash = Argon2idDerive(password, salt, Argon2idMemoryKb, Argon2idIterations, Argon2idParallelism, Argon2idHashBytes);
        return $"{PhcPrefix}v=19$m={Argon2idMemoryKb},t={Argon2idIterations},p={Argon2idParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a candidate password against a stored hash. Recognizes
    /// Argon2id PHC strings and the two legacy SHA-256 formats present in
    /// the DB (one salted with <c>ABS_SALT_2026</c> from AuthService, one
    /// unsalted from the shadow implementation that lived in
    /// <c>Pages/Admin/Users.cshtml.cs</c> until this PR consolidated it).
    /// Constant-time comparison throughout.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        if (storedHash.StartsWith(PhcPrefix, StringComparison.Ordinal))
            return VerifyArgon2id(password, storedHash);

        // Legacy paths. Both produce a 44-character Base64 string of 32
        // raw bytes, so length alone doesn't disambiguate; try each in
        // constant time.
        var saltedSha256 = LegacySha256Salted(password);
        if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(saltedSha256),
                Encoding.UTF8.GetBytes(storedHash)))
            return true;

        var plainSha256 = LegacySha256Unsalted(password);
        if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(plainSha256),
                Encoding.UTF8.GetBytes(storedHash)))
            return true;

        return false;
    }

    /// <summary>
    /// Returns true if the stored hash uses an algorithm or parameter set
    /// that is not the current target — meaning the next successful login
    /// should re-hash with the current parameters.
    /// </summary>
    public static bool NeedsRehash(string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return true;
        if (!storedHash.StartsWith(PhcPrefix, StringComparison.Ordinal))
            return true; // any legacy SHA-256 hash

        try
        {
            var (m, t, p) = ParseArgon2idParams(storedHash);
            return m != Argon2idMemoryKb || t != Argon2idIterations || p != Argon2idParallelism;
        }
        catch
        {
            return true;
        }
    }

    private static bool VerifyArgon2id(string password, string phcString)
    {
        try
        {
            // PHC: $argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>
            var parts = phcString.Split('$');
            if (parts.Length != 6) return false;
            if (parts[1] != "argon2id") return false;

            var (m, t, p) = ParseArgon2idParams(phcString);
            var salt = Convert.FromBase64String(parts[4]);
            var expectedHash = Convert.FromBase64String(parts[5]);

            var actualHash = Argon2idDerive(password, salt, m, t, p, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }

    private static (int memoryKb, int iterations, int parallelism) ParseArgon2idParams(string phcString)
    {
        var parts = phcString.Split('$');
        var paramsStr = parts[3]; // "m=...,t=...,p=..."
        int m = 0, t = 0, p = 0;
        foreach (var kv in paramsStr.Split(','))
        {
            var pair = kv.Split('=');
            if (pair.Length != 2) continue;
            switch (pair[0])
            {
                case "m": m = int.Parse(pair[1]); break;
                case "t": t = int.Parse(pair[1]); break;
                case "p": p = int.Parse(pair[1]); break;
            }
        }
        return (m, t, p);
    }

    private static byte[] Argon2idDerive(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int hashLen)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = memoryKb;
        argon2.Iterations = iterations;
        argon2.DegreeOfParallelism = parallelism;
        return argon2.GetBytes(hashLen);
    }

    private static string LegacySha256Salted(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + LegacySha256Salt));
        return Convert.ToBase64String(bytes);
    }

    private static string LegacySha256Unsalted(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
