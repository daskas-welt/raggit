using System;

namespace RAGGit.Core.Models;

/// <summary>
/// Roles supported by the per-person account directory.
/// </summary>
public enum UserRole
{
    Admin,
    Employee,
}

/// <summary>
/// Per-person account on the AI Workstation. Single-tenant; no company/tenant dimension.
/// </summary>
public sealed class User
{
    /// <summary>
    /// Stable person id — used for attribution in Documents.CreatedBy and Queries.UserId.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique login name (case-insensitive match at the DB layer via COLLATE NOCASE).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name returned by the identity envelope.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Admin or Employee.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Salted, non-recoverable password hash: PBKDF2-SHA256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// False ⇒ sign-in refused and live tokens rejected on next request.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Consecutive failed sign-in attempts.
    /// </summary>
    public int FailedAccessCount { get; set; }

    /// <summary>
    /// UTC lockout expiry; null when not locked.
    /// </summary>
    public DateTime? LockoutUntil { get; set; }

    /// <summary>
    /// Set by an admin password reset to force a change at next sign-in.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Last successful sign-in (UTC).
    /// </summary>
    public DateTime? LastSignInAt { get; set; }

    /// <summary>
    /// When the password was last changed (UTC).
    /// </summary>
    public DateTime LastPasswordChangedAt { get; set; }

    /// <summary>
    /// Account creation time (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Derived: account is currently locked out.
    /// </summary>
    public bool IsLockedOut => LockoutUntil.HasValue && LockoutUntil.Value > DateTime.UtcNow;
}
