using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RAGGit.Workstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Placeholder login endpoint. Returns 401 for any credentials when no accounts
    /// are provisioned; real authentication is implemented in T018.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        return Unauthorized(new { error = "unauthorized" });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        var role =
            User.FindFirstValue(ClaimTypes.Role)
            ?? (
                User.IsInRole("Admin") ? "Admin"
                : User.IsInRole("Employee") ? "Employee"
                : "Unknown"
            );
        // Ensure role is Admin or Employee
        if (role != "Admin" && role != "Employee")
        {
            // Fallback: try to read from claims directly
            var claimRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            if (claimRole == "Admin" || claimRole == "Employee")
                role = claimRole;
        }

        return Ok(new { identityType = "ApiKey", role });
    }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
