namespace RAGGit.Workstation.Api.Auth;

/// <summary>
/// Options for account lockout policy.
/// </summary>
public sealed class LockoutPolicyOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Consecutive failed sign-in attempts before the account is locked.
    /// </summary>
    public int Threshold { get; set; } = 5;

    /// <summary>
    /// Lockout duration in minutes.
    /// </summary>
    public int Minutes { get; set; } = 15;
}
