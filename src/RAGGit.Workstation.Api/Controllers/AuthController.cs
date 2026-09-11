using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RAGGit.Workstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController : ControllerBase
{
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
