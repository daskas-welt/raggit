using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;

namespace RAGGit.Workstation.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _userStore;
    private readonly JwtTokenService _tokenService;
    private readonly LockoutPolicy _lockoutPolicy;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IUserRepository userStore,
        JwtTokenService tokenService,
        LockoutPolicy lockoutPolicy,
        IHostEnvironment environment
    )
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _lockoutPolicy = lockoutPolicy ?? throw new ArgumentNullException(nameof(lockoutPolicy));
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
                Response.Headers.RetryAfter = _lockoutPolicy.RetryAfterSeconds(user).ToString();
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
                await _lockoutPolicy.RecordFailureAsync(user, HttpContext.RequestAborted);
                return Unauthorized(new { error = "unauthorized" });
            }

            await _lockoutPolicy.RecordSuccessAsync(user, HttpContext.RequestAborted);
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
    /// POST /api/auth/refresh — trivial re-issue of an 8h token from a still-valid token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        try
        {
            var user = await _userStore.GetByIdAsync(userId, cancellationToken);
            if (user is null || !user.IsActive || user.IsLockedOut)
            {
                return Unauthorized(new { error = "unauthorized" });
            }

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
