using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Workstation.Api.Auth;

/// <summary>
/// Account lockout policy: after a configured number of consecutive failed sign-ins,
/// the account is locked until a configured duration passes. Persists state via <see cref="IUserRepository"/>.
/// </summary>
public sealed class LockoutPolicy
{
    private readonly IUserRepository _store;
    private readonly LockoutPolicyOptions _options;

    public LockoutPolicy(IUserRepository store, IOptions<LockoutPolicyOptions> options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Records a failed sign-in attempt and persists the updated user.
    /// </summary>
    public async Task RecordFailureAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.FailedAccessCount++;
        if (user.FailedAccessCount >= _options.Threshold)
        {
            user.LockoutUntil = DateTime.UtcNow.AddMinutes(_options.Minutes);
        }

        await _store.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// Resets failure counters on a successful sign-in and persists the updated user.
    /// </summary>
    public async Task RecordSuccessAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.FailedAccessCount = 0;
        user.LockoutUntil = null;
        await _store.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// Seconds until the current lockout expires. Returns 0 if not locked.
    /// </summary>
    public int RetryAfterSeconds(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsLockedOut)
            return 0;

        return (int)(user.LockoutUntil!.Value - DateTime.UtcNow).TotalSeconds + 1;
    }
}
