using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Locks in the behavior of <see cref="AuthService"/> after the Argon2id
/// migration in PR #11. Covers the static hashing surface (HashPassword,
/// VerifyPassword, NeedsRehash) and the rolling-rehash flow inside
/// <see cref="AuthService.ValidateUserAsync"/>.
///
/// The Argon2id paths are CPU-heavy on purpose; each test creates short
/// hashes (the parameters are baked in via the production constants).
/// Tests use the EF Core InMemory provider for the rolling-rehash flow.
/// </summary>
public class AuthServiceTests
{
    // ── Test DB helpers (mirrors the pattern in AssetConcurrencyTests) ──

    // The InMemory provider doesn't support JsonDocument or PG xmin
    // row versions; ignore both for the test model. Same pattern as
    // AssetConcurrencyTests.
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static User MakeUser(string username, string passwordHash, bool isActive = true)
    {
        return new User
        {
            Username = username,
            PasswordHash = passwordHash,
            Role = "Admin",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── HashPassword: produces a valid Argon2id PHC string ──────────────

    [Fact]
    public void HashPassword_ReturnsArgon2idPhcString()
    {
        var hash = AuthService.HashPassword("hello");

        Assert.StartsWith("$argon2id$v=19$", hash);

        // PHC: $argon2id$v=19$<params>$<salt-base64>$<hash-base64>
        var parts = hash.Split('$');
        Assert.Equal(6, parts.Length);
        Assert.Equal("", parts[0]);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Contains("m=", parts[3]);
        Assert.Contains("t=", parts[3]);
        Assert.Contains("p=", parts[3]);
        // Salt + hash must be valid Base64
        Assert.NotNull(Convert.FromBase64String(parts[4]));
        Assert.NotNull(Convert.FromBase64String(parts[5]));
    }

    [Fact]
    public void HashPassword_TwoCallsSamePassword_ProduceDifferentHashes()
    {
        // Salt is per-call random; identical passwords MUST hash differently.
        var h1 = AuthService.HashPassword("same-password");
        var h2 = AuthService.HashPassword("same-password");
        Assert.NotEqual(h1, h2);
    }

    // ── VerifyPassword: Argon2id round-trip ─────────────────────────────

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = AuthService.HashPassword("correct-horse-battery-staple");
        Assert.True(AuthService.VerifyPassword("correct-horse-battery-staple", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = AuthService.HashPassword("real-pw");
        Assert.False(AuthService.VerifyPassword("wrong-pw", hash));
    }

    [Fact]
    public void VerifyPassword_NullOrEmptyStoredHash_ReturnsFalse()
    {
        Assert.False(AuthService.VerifyPassword("anything", null!));
        Assert.False(AuthService.VerifyPassword("anything", ""));
    }

    [Fact]
    public void VerifyPassword_MalformedArgon2idHash_ReturnsFalse()
    {
        // Looks like an Argon2id hash but the body is junk.
        var malformed = "$argon2id$v=19$m=65536,t=3,p=1$bm9uc2Vuc2U=$YWxzbm9uc2Vuc2U=";
        Assert.False(AuthService.VerifyPassword("anything", malformed));
    }

    // ── VerifyPassword: legacy SHA-256 backward compat ─────────────────
    //
    // These cases are why VerifyPassword exists: real users in the DB
    // today have SHA-256 hashes from the pre-PR-#11 code. They MUST keep
    // logging in. Plus the latent unsalted variant created by the broken
    // Pages/Admin/Users.cshtml.cs HashPassword (closed in #11).

    [Fact]
    public void VerifyPassword_LegacySaltedSha256_ReturnsTrue()
    {
        const string password = "legacy-1";
        var legacyHash = LegacySaltedSha256(password);
        Assert.True(AuthService.VerifyPassword(password, legacyHash));
    }

    [Fact]
    public void VerifyPassword_LegacyUnsaltedSha256_ReturnsTrue()
    {
        const string password = "admin-created-user-pw";
        var legacyHash = LegacyUnsaltedSha256(password);
        Assert.True(AuthService.VerifyPassword(password, legacyHash));
    }

    [Fact]
    public void VerifyPassword_LegacySaltedSha256_WrongPassword_ReturnsFalse()
    {
        var legacyHash = LegacySaltedSha256("real");
        Assert.False(AuthService.VerifyPassword("wrong", legacyHash));
    }

    [Fact]
    public void VerifyPassword_LegacyUnsaltedSha256_WrongPassword_ReturnsFalse()
    {
        var legacyHash = LegacyUnsaltedSha256("real");
        Assert.False(AuthService.VerifyPassword("wrong", legacyHash));
    }

    // ── NeedsRehash semantics ──────────────────────────────────────────

    [Fact]
    public void NeedsRehash_FreshArgon2idCurrentParams_ReturnsFalse()
    {
        var hash = AuthService.HashPassword("any");
        Assert.False(AuthService.NeedsRehash(hash));
    }

    [Fact]
    public void NeedsRehash_LegacySalted_ReturnsTrue()
    {
        Assert.True(AuthService.NeedsRehash(LegacySaltedSha256("x")));
    }

    [Fact]
    public void NeedsRehash_LegacyUnsalted_ReturnsTrue()
    {
        Assert.True(AuthService.NeedsRehash(LegacyUnsaltedSha256("x")));
    }

    [Fact]
    public void NeedsRehash_Argon2idStaleParams_ReturnsTrue()
    {
        // Synthesize an Argon2id hash with parameters that don't match
        // the production constants. Hash bytes don't matter — NeedsRehash
        // only inspects the parameters.
        var stale = "$argon2id$v=19$m=4096,t=2,p=1$YWJjZGVmZ2hpams=$ZHVtbXloYXNoYnl0ZXM=";
        Assert.True(AuthService.NeedsRehash(stale));
    }

    [Fact]
    public void NeedsRehash_NullOrEmpty_ReturnsTrue()
    {
        Assert.True(AuthService.NeedsRehash(null!));
        Assert.True(AuthService.NeedsRehash(""));
    }

    // ── ValidateUserAsync: rolling re-hash on legacy login ─────────────

    [Fact]
    public async Task ValidateUserAsync_LegacySalted_AcceptsAndPersistsArgon2idRehash()
    {
        await using var db = NewDb();
        const string username = "ROLLOVER1";
        const string password = "secret-1";
        db.Users.Add(MakeUser(username, LegacySaltedSha256(password)));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, password);

        Assert.NotNull(user);
        Assert.StartsWith("$argon2id$", user!.PasswordHash);

        // Verify the rehash was actually persisted, not just on the in-memory entity.
        var fromDb = await db.Users.AsNoTracking()
            .FirstAsync(u => u.Username == username);
        Assert.StartsWith("$argon2id$", fromDb.PasswordHash);
        Assert.NotNull(fromDb.PasswordChangedAt);
    }

    [Fact]
    public async Task ValidateUserAsync_LegacyUnsalted_AcceptsAndPersistsArgon2idRehash()
    {
        await using var db = NewDb();
        const string username = "ROLLOVER2";
        const string password = "admin-page-pw";
        db.Users.Add(MakeUser(username, LegacyUnsaltedSha256(password)));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, password);

        Assert.NotNull(user);
        Assert.StartsWith("$argon2id$", user!.PasswordHash);
    }

    [Fact]
    public async Task ValidateUserAsync_Argon2idHashOnDisk_AcceptsWithoutRehash()
    {
        await using var db = NewDb();
        const string username = "STAYPUT";
        const string password = "already-modern";
        var hashOnDisk = AuthService.HashPassword(password);
        db.Users.Add(MakeUser(username, hashOnDisk));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, password);

        Assert.NotNull(user);
        // Same hash bytes — no rehash event triggered.
        Assert.Equal(hashOnDisk, user!.PasswordHash);
    }

    [Fact]
    public async Task ValidateUserAsync_WrongPassword_ReturnsNullAndDoesNotMutateHash()
    {
        await using var db = NewDb();
        const string username = "WRONG";
        var hashOnDisk = AuthService.HashPassword("real");
        db.Users.Add(MakeUser(username, hashOnDisk));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, "wrong-password");

        Assert.Null(user);
        var fromDb = await db.Users.AsNoTracking()
            .FirstAsync(u => u.Username == username);
        Assert.Equal(hashOnDisk, fromDb.PasswordHash);
    }

    [Fact]
    public async Task ValidateUserAsync_NonexistentUser_ReturnsNull()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync("DOESNOTEXIST", "anything");
        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateUserAsync_InactiveUser_ReturnsNull()
    {
        await using var db = NewDb();
        const string username = "INACTIVE";
        const string password = "real";
        db.Users.Add(MakeUser(username, AuthService.HashPassword(password), isActive: false));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, password);

        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateUserAsync_UsernameLookupIsCaseInsensitive()
    {
        await using var db = NewDb();
        const string password = "case-test";
        // Stored upper-cased, the way CreateUserAsync does it.
        db.Users.Add(MakeUser("ADMIN", AuthService.HashPassword(password)));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);

        Assert.NotNull(await svc.ValidateUserAsync("admin", password));
        Assert.NotNull(await svc.ValidateUserAsync("Admin", password));
        Assert.NotNull(await svc.ValidateUserAsync("ADMIN", password));
    }

    [Fact]
    public async Task ValidateUserAsync_LegacySalted_WrongPassword_DoesNotRehash()
    {
        // Negative case for the rolling-rehash path: if the password is wrong,
        // no rehash should happen — even though the hash is in legacy form.
        await using var db = NewDb();
        const string username = "LEGACY_WRONG";
        var legacyHash = LegacySaltedSha256("the-real-password");
        db.Users.Add(MakeUser(username, legacyHash));
        await db.SaveChangesAsync();

        var svc = new AuthService(db);
        var user = await svc.ValidateUserAsync(username, "definitely-wrong");

        Assert.Null(user);
        var fromDb = await db.Users.AsNoTracking()
            .FirstAsync(u => u.Username == username);
        Assert.Equal(legacyHash, fromDb.PasswordHash); // not rolled
    }

    // ── CreateUserAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_StoresArgon2idHash()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);

        var user = await svc.CreateUserAsync("newperson", "test-password", "Admin",
            fullName: "New Person", email: "new@example.com");

        Assert.NotNull(user);
        Assert.StartsWith("$argon2id$", user.PasswordHash);
        // Username must be uppercased per CreateUserAsync's contract.
        Assert.Equal("NEWPERSON", user.Username);
        Assert.True(user.IsActive);
        Assert.Equal("Admin", user.Role);
    }

    [Fact]
    public async Task CreateUserAsync_TwoUsersSamePassword_DifferentHashes()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);

        var u1 = await svc.CreateUserAsync("alice", "shared", "Viewer");
        var u2 = await svc.CreateUserAsync("bob",   "shared", "Viewer");

        // Same password but per-user random salt → different hashes.
        Assert.NotEqual(u1.PasswordHash, u2.PasswordHash);
        Assert.True(AuthService.VerifyPassword("shared", u1.PasswordHash));
        Assert.True(AuthService.VerifyPassword("shared", u2.PasswordHash));
    }

    [Fact]
    public async Task CreateUserAsync_PersistsAndCanLoginImmediately()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);

        await svc.CreateUserAsync("loginnow", "first-login", "Admin");
        var loggedIn = await svc.ValidateUserAsync("loginnow", "first-login");

        Assert.NotNull(loggedIn);
        // No rehash event — already current Argon2id format.
        Assert.StartsWith("$argon2id$", loggedIn!.PasswordHash);
    }

    // ── ChangePasswordAsync ────────────────────────────────────────────

    [Fact]
    public async Task ChangePasswordAsync_RewritesHashAsArgon2id()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);

        var user = await svc.CreateUserAsync("changeme", "old-pw", "Admin");
        var oldHash = user.PasswordHash;

        var result = await svc.ChangePasswordAsync(user.Id, "new-pw");

        Assert.True(result);
        var fromDb = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.StartsWith("$argon2id$", fromDb.PasswordHash);
        Assert.NotEqual(oldHash, fromDb.PasswordHash);
        Assert.NotNull(fromDb.PasswordChangedAt);
        Assert.True(AuthService.VerifyPassword("new-pw", fromDb.PasswordHash));
        Assert.False(AuthService.VerifyPassword("old-pw", fromDb.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_NonexistentUser_ReturnsFalse()
    {
        await using var db = NewDb();
        var svc = new AuthService(db);

        var result = await svc.ChangePasswordAsync(userId: 999_999, "anything");
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_OnLegacyHash_RewritesToArgon2id()
    {
        // Real-world scenario: user with a legacy SHA-256 hash gets their
        // password reset by an admin. The reset must produce Argon2id, not
        // re-create the legacy hash.
        await using var db = NewDb();
        const string username = "LEGACYRESET";
        db.Users.Add(MakeUser(username, LegacySaltedSha256("old-legacy-pw")));
        await db.SaveChangesAsync();
        var userId = (await db.Users.FirstAsync(u => u.Username == username)).Id;

        var svc = new AuthService(db);
        var result = await svc.ChangePasswordAsync(userId, "fresh-new-pw");

        Assert.True(result);
        var fromDb = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.StartsWith("$argon2id$", fromDb.PasswordHash);
        Assert.True(AuthService.VerifyPassword("fresh-new-pw", fromDb.PasswordHash));
    }

    // ── NormalizeRole ──────────────────────────────────────────────────

    [Theory]
    [InlineData("admin",       "Admin")]
    [InlineData("ADMIN",       "Admin")]
    [InlineData("Admin",       "Admin")]
    [InlineData("aCcOuNtAnT",  "Accountant")]
    [InlineData("VIEWER",      "Viewer")]
    public void NormalizeRole_StandardRoles_NormalizesToTitleCase(string input, string expected)
    {
        Assert.Equal(expected, AuthService.NormalizeRole(input));
    }

    [Fact]
    public void NormalizeRole_UnknownRole_PassesThroughUnchanged()
    {
        // Non-standard role strings are preserved as-is so a future role
        // addition doesn't silently get coerced to something else.
        Assert.Equal("CustomRole", AuthService.NormalizeRole("CustomRole"));
        Assert.Equal("",           AuthService.NormalizeRole(""));
    }

    // ── SeedDefaultUserAsync ───────────────────────────────────────────

    [Fact]
    public async Task SeedDefaultUserAsync_WhenUsersExist_DoesNothing()
    {
        await using var db = NewDb();
        // Pre-populate one user so Users.AnyAsync() returns true.
        db.Users.Add(MakeUser("EXISTING", AuthService.HashPassword("pw")));
        await db.SaveChangesAsync();
        var initialCount = await db.Users.CountAsync();

        var svc = new AuthService(db);
        await svc.SeedDefaultUserAsync();

        Assert.Equal(initialCount, await db.Users.CountAsync());
    }

    // ── Helpers replicating the legacy hash formats ────────────────────

    private static string LegacySaltedSha256(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "ABS_SALT_2026"));
        return Convert.ToBase64String(bytes);
    }

    private static string LegacyUnsaltedSha256(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
