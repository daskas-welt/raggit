using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;

namespace RAGGit.Workstation.Api.Auth;

/// <summary>
/// Loads or creates the workstation JWT signing key at the configured path.
/// </summary>
public static class AuthKeyLoader
{
    public const int KeyBytes = 32;

    public static byte[] LoadOrCreateKey(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (TryReadKey(fullPath, out var existing))
        {
            return existing;
        }

        // Publish atomically via a uniquely named temp file so a concurrent
        // starter (second worker process, or parallel test hosts) never reads a
        // half-written key and only one creation wins. The loser falls through
        // and reads the winner's key.
        var key = RandomNumberGenerator.GetBytes(KeyBytes);
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(tempPath, key);
            File.Move(tempPath, fullPath, overwrite: false);
            RestrictAccessToCurrentUser(fullPath);
            return key;
        }
        catch (IOException)
        {
            // Another writer created the key between our existence check and
            // the move. Read theirs below.
        }
        finally
        {
            TryDelete(tempPath);
        }

        // Lost the race. The winner may still be finishing its File.Move and
        // the best-effort ACL hardening (which briefly opens the key), so retry
        // the read instead of treating a transient lock as a hard failure.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (TryReadKey(fullPath, out var raced))
            {
                return raced;
            }

            Thread.Sleep(10);
        }

        throw new IOException(
            $"Unable to load or atomically create the JWT signing key at '{fullPath}'."
        );
    }

    private static bool TryReadKey(string path, out byte[] key)
    {
        try
        {
            if (!File.Exists(path))
            {
                key = Array.Empty<byte>();
                return false;
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == KeyBytes)
            {
                key = bytes;
                return true;
            }

            // A truncated key would silently weaken/break JWT signing; treat it
            // as absent so the caller creates a fresh one.
            key = Array.Empty<byte>();
            return false;
        }
        catch (IOException)
        {
            key = Array.Empty<byte>();
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private static void RestrictAccessToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var currentUser = WindowsIdentity.GetCurrent().Name;
            var fileSecurity = new FileSecurity(path, AccessControlSections.Access);
            fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            fileSecurity.AddAccessRule(
                new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.Read | FileSystemRights.Write,
                    AccessControlType.Allow
                )
            );
            new FileInfo(path).SetAccessControl(fileSecurity);
        }
        catch
        {
            // ACL hardening is best-effort on Windows; do not fail startup.
        }
    }
}
