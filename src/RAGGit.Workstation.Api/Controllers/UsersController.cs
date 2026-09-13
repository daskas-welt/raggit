using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Admin-only people management endpoints per FR-005.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly UserStore _userStore;

    public UsersController(UserStore userStore)
    {
        _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    /// <summary>
    /// GET /api/users — list person accounts (PasswordHash never serialized).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userStore.ListAsync(cancellationToken);
            return Ok(users.Select(ToDto));
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
    /// POST /api/users — create a person account with PBKDF2-hashed password.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] UserCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (
            string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.Password)
        )
        {
            return BadRequest(new { error = "username, displayName, and password are required." });
        }

        if (request.Username.Trim().Length < 3 || request.Username.Trim().Length > 64)
        {
            return BadRequest(new { error = "username must be 3-64 characters." });
        }

        if (request.Password.Length < 10)
        {
            return BadRequest(new { error = "password must be at least 10 characters." });
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return BadRequest(new { error = "role must be Admin or Employee." });
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(request.Password),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };

        try
        {
            await _userStore.CreateAsync(user, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = $"Username '{request.Username}' already exists." });
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "identity store unavailable" }
            );
        }

        return Created($"/api/users/{user.Id}", ToDto(user));
    }

    /// <summary>
    /// GET /api/users/{id} — read a single person account.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userStore.GetByIdAsync(id, cancellationToken);
            if (user is null)
            {
                return NotFound(new { error = "User not found." });
            }

            return Ok(ToDto(user));
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
    /// PATCH /api/users/{id} — change role, displayName, or active status.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UserPatchRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (
            !request.Role.HasValue
            && !request.IsActive.HasValue
            && string.IsNullOrWhiteSpace(request.DisplayName)
        )
        {
            return BadRequest(new { error = "No fields provided to update." });
        }

        if (
            !string.IsNullOrWhiteSpace(request.DisplayName)
            && (request.DisplayName.Trim().Length < 1 || request.DisplayName.Trim().Length > 100)
        )
        {
            return BadRequest(new { error = "displayName must be 1-100 characters." });
        }

        try
        {
            var user = await _userStore.GetByIdAsync(id, cancellationToken);
            if (user is null)
            {
                return NotFound(new { error = "User not found." });
            }

            if (request.Role.HasValue)
            {
                user.Role = request.Role.Value;
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
                if (!user.IsActive)
                {
                    // Clear any active lockout so reactivation is clean.
                    user.LockoutUntil = null;
                    user.FailedAccessCount = 0;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                user.DisplayName = request.DisplayName.Trim();
            }

            await _userStore.UpdateAsync(user, cancellationToken);
            return Ok(ToDto(user));
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
    /// POST /api/users/{id}/reset-password — set a new password and clear lockout.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "password is required." });
        }

        if (request.Password.Length < 10)
        {
            return BadRequest(new { error = "password must be at least 10 characters." });
        }

        try
        {
            var user = await _userStore.GetByIdAsync(id, cancellationToken);
            if (user is null)
            {
                return NotFound(new { error = "User not found." });
            }

            user.PasswordHash = PasswordHasher.HashPassword(request.Password);
            user.MustChangePassword = request.MustChangePassword;
            user.FailedAccessCount = 0;
            user.LockoutUntil = null;
            user.LastPasswordChangedAt = DateTime.UtcNow;

            await _userStore.UpdateAsync(user, cancellationToken);
            return NoContent();
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "identity store unavailable" }
            );
        }
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            LockedOut = user.IsLockedOut,
            LastSignInAt = user.LastSignInAt,
            CreatedAt = user.CreatedAt,
        };
    }
}

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public bool LockedOut { get; set; }
    public DateTime? LastSignInAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserCreateRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class UserPatchRequest
{
    public string? DisplayName { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string Password { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
