using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abs.FixedAssets.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var name = User.Identity?.Name ?? "(anonymous)";
        
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList();
        
        var allClaims = User.Claims
            .Select(c => new { type = c.Type, value = c.Value })
            .ToList();
        
        return Ok(new
        {
            isAuthenticated,
            name,
            roles,
            claims = allClaims
        });
    }
}
