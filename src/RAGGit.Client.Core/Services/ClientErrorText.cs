using System;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Single point for client-facing workstation error text (010-winui-client).
/// API clients already prefix transport failures once ("cannot reach AI
/// workstation: …"); ViewModels must reuse that text as-is instead of
/// prepending the prefix a second time. These helpers add the prefix only
/// when it is absent, so messages never read "cannot reach AI workstation:
/// cannot reach AI workstation: …".
/// </summary>
public static class ClientErrorText
{
    public static string CannotReach(string? message) =>
        WithPrefix(message, "cannot reach AI workstation");

    public static string Unavailable(string? message) =>
        WithPrefix(message, "AI workstation unavailable");

    private static string WithPrefix(string? message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return prefix;
        }

        if (message.Contains(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{prefix}: {message}";
    }
}
