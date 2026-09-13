using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace RAGGit.Core.Auth;

/// <summary>
/// PBKDF2-SHA256 password hashing with a self-describing format.
/// </summary>
public static class PasswordHasher
{
    public const int DefaultIterations = 310_000;
    public const int SaltBytes = 16;
    public const int KeyBytes = 32;

    private const string Prefix = "PBKDF2-SHA256";

    /// <summary>
    /// Hashes a password. Defaults to <see cref="DefaultIterations"/> iterations.
    /// </summary>
    public static string HashPassword(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyBytes
        );

        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against a self-describing hash in constant time.
    /// </summary>
    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
            return false;

        var parts = hashedPassword.Split('$');
        if (parts.Length != 4)
            return false;
        if (!string.Equals(parts[0], Prefix, StringComparison.Ordinal))
            return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;
        if (!TryConvertFromBase64(parts[2], out var salt) || salt.Length != SaltBytes)
            return false;
        if (
            !TryConvertFromBase64(parts[3], out var expectedHash)
            || expectedHash.Length != KeyBytes
        )
            return false;

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeyBytes
        );

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool TryConvertFromBase64(string input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            bytes = Convert.FromBase64String(input);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
