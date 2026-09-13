using System;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;

namespace RAGGit.Workstation.Api.Cli;

/// <summary>
/// Local operator CLI verbs. Runs against the workstation database directly so the
/// server does not need to be up and no plaintext secret is ever stored in config.
/// </summary>
public sealed class OperatorCli
{
    public async Task<int> RunAsync(
        string[] args,
        UserStore store,
        CancellationToken cancellationToken = default
    )
    {
        if (args.Length < 2 || !string.Equals(args[0], "user", StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync("Usage: raggit user add ...");
            return 1;
        }

        if (!string.Equals(args[1], "add", StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync("Unknown user sub-command.");
            return 1;
        }

        var parsed = ParseAddArgs(args);
        if (parsed is null)
        {
            await Console.Error.WriteLineAsync(
                "Usage: raggit user add --username <name> --display-name <name> --role Admin|Employee [--password <pwd> | --password-stdin]"
            );
            return 1;
        }

        if (!Enum.TryParse<UserRole>(parsed.Role, true, out var role))
        {
            await Console.Error.WriteLineAsync($"Invalid role '{parsed.Role}'.");
            return 3;
        }

        var password = parsed.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            await Console.Error.WriteLineAsync("Password is required.");
            return 1;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = parsed.Username,
            DisplayName = parsed.DisplayName,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(password),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };

        try
        {
            await store.CreateAsync(user, cancellationToken);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync($"Username '{parsed.Username}' already exists.");
            return 2;
        }

        Console.WriteLine($"Created {role} '{parsed.Username}' ({user.Id}).");
        return 0;
    }

    private static AddArgs? ParseAddArgs(string[] args)
    {
        string? username = null;
        string? displayName = null;
        string? role = null;
        string? password = null;
        bool passwordStdin = false;

        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--username", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return null;
                username = args[++i];
            }
            else if (string.Equals(arg, "--display-name", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return null;
                displayName = args[++i];
            }
            else if (string.Equals(arg, "--role", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return null;
                role = args[++i];
            }
            else if (string.Equals(arg, "--password", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return null;
                password = args[++i];
            }
            else if (string.Equals(arg, "--password-stdin", StringComparison.OrdinalIgnoreCase))
            {
                passwordStdin = true;
            }
            else
            {
                return null;
            }
        }

        if (passwordStdin && string.IsNullOrWhiteSpace(password))
        {
            password = ReadPasswordFromStdin();
        }

        if (
            string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(role)
            || string.IsNullOrWhiteSpace(password)
        )
        {
            return null;
        }

        return new AddArgs(username, displayName, role, password);
    }

    private static string? ReadPasswordFromStdin()
    {
        var input = Console.In.ReadLine();
        return input;
    }

    private sealed record AddArgs(
        string Username,
        string DisplayName,
        string Role,
        string Password
    );
}
