using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Core.Abstractions.Repositories;

/// <summary>
/// Repository for the <see cref="User"/> aggregate root, per the
/// MS persistence-layer design (one repository per aggregate root;
/// interfaces in the domain, implementations in persistence).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates non-access account fields. Role and active state must be changed through
    /// <see cref="TryPatchAsync"/> so stale snapshots cannot bypass the last-admin guard.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the mutable profile fields atomically and refuses removal of the final active Admin.
    /// </summary>
    Task<UserPatchResult> TryPatchAsync(
        Guid id,
        UserRole? role,
        bool? isActive,
        string? displayName,
        CancellationToken cancellationToken = default
    );
}
