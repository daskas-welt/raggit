using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

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

        if (!File.Exists(fullPath))
        {
            var key = RandomNumberGenerator.GetBytes(KeyBytes);
            File.WriteAllBytes(fullPath, key);
            RestrictAccessToCurrentUser(fullPath);
        }

        return File.ReadAllBytes(fullPath);
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
