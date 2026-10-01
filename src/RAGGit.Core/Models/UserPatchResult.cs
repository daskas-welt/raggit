namespace RAGGit.Core.Models;

/// <summary>
/// Result of applying an Admin user patch while preserving the last-active-admin invariant.
/// </summary>
public sealed record UserPatchResult(UserPatchStatus Status, User? User)
{
    public static UserPatchResult Updated(User user) => new(UserPatchStatus.Updated, user);

    public static UserPatchResult NotFound() => new(UserPatchStatus.NotFound, null);

    public static UserPatchResult LastActiveAdmin() => new(UserPatchStatus.LastActiveAdmin, null);
}

public enum UserPatchStatus
{
    Updated,
    NotFound,
    LastActiveAdmin,
}
