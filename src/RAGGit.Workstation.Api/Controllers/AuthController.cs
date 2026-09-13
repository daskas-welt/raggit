using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;

namespace RAGGit.Workstation.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController : ControllerBase
{
    private readonly UserStore _userStore;
    private readonly JwtTokenService _tokenService;
    private readonly LockoutPolicyOptions _lockoutOptions;
    private readonly IHostEnvironment _environment;

    public AuthController(
        UserStore userStore,
        JwtTokenService tokenService,
        IOptions<LockoutPolicyOptions> lockoutOptions,
        IHostEnvironment environment
    )
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _lockoutOptions =
            lockoutOptions?.Value ?? throw new ArgumentNullException(nameof(lockoutOptions));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// POST /api/auth/login — HTTPS only (outside Development). Issues an 8h JWT.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!Request.IsHttps && !_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "HTTPS required." });
        }

        if (
            request is null
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
        )
        {
            return BadRequest(new { error = "Username and password are required." });
        }

        try
        {
            var user = await _userStore.GetByUsernameAsync(
                request.Username,
                HttpContext.RequestAborted
            );
            if (user is null)
            {
                return Unauthorized(new { error = "unauthorized" });
            }

            if (user.IsLockedOut)
            {
                var retryAfter = (int)(user.LockoutUntil!.Value - DateTime.UtcNow).TotalSeconds + 1;
                Response.Headers.RetryAfter = retryAfter.ToString();
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new { error = "account locked" }
                );
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { error = "unauthorized" });
            }

            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedAccessCount++;
                if (user.FailedAccessCount >= _lockoutOptions.Threshold)
                {
                    user.LockoutUntil = DateTime.UtcNow.AddMinutes(_lockoutOptions.Minutes);
                }

                await _userStore.UpdateAsync(user, HttpContext.RequestAborted);
                return Unauthorized(new { error = "unauthorized" });
            }

            user.FailedAccessCount = 0;
            user.LockoutUntil = null;
            user.LastSignInAt = DateTime.UtcNow;
            await _userStore.UpdateAsync(user, HttpContext.RequestAborted);

            var token = _tokenService.IssueToken(user);
            return Ok(
                new TokenResponse
                {
                    AccessToken = token,
                    TokenType = "Bearer",
                    ExpiresIn = _tokenService.TokenLifetimeHours * 3600,
                }
            );
        }
        catch (Exception)
        {
            // Identity store unavailable — fail fast per FR-011.
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "identity store unavailable" }
            );
        }
    }

    /// <summary>
    /// GET /api/auth/me — additive identity envelope.
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sub))
        {
            // Local per-person JWT session.
            return Ok(
                new
                {
                    identityType = "Local",
                    role,
                    displayName = User.FindFirstValue("displayName"),
                    username = User.FindFirstValue("username"),
                    sub,
                }
            );
        }

        // ApiKey (machine/bootstrap) session keeps the 1.2.0 shape.
        return Ok(new { identityType = "ApiKey", role });
    }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
