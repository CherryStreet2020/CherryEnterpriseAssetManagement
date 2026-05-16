using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Abs.FixedAssets.Services;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AuthService _authService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public LoginModel(AuthService authService, IWebHostEnvironment env, IConfiguration config)
    {
        _authService = authService;
        _env = env;
        _config = config;
    }

    // PR #100 (B-04): The "Demo Accounts" click-to-fill buttons render the
    // admin/accountant/viewer credentials directly in the page HTML so anyone
    // who can reach /Account/Login (i.e. the whole internet on a public
    // deployment) sees three valid logins. Gate the card to Development
    // environment OR an explicit `Login:ShowDemoAccounts=true` config flag,
    // so the demo experience still works in staging but production never
    // ships the buttons. Boolean exposed on the page model so the cshtml
    // stays simple.
    public bool ShowDemoAccounts =>
        _env.IsDevelopment()
        || _config.GetValue<bool>("Login:ShowDemoAccounts", false);

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please enter username and password.";
            return Page();
        }

        var user = await _authService.ValidateUserAsync(Username, Password);

        if (user == null)
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var normalizedRole = AuthService.NormalizeRole(user.Role);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, normalizedRole),
            new Claim("FullName", user.FullName ?? user.Username)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return LocalRedirect(ReturnUrl);
    }
}
