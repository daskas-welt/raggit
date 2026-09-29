using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="ISecureStorage"/> backed by DPAPI-protected JSON in LocalAppData.
/// </summary>
public sealed class WpfSecureStorage : ISecureStorage
{
    private const string FileName = "secure.dat";

    public Task<string?> GetAsync(string key)
    {
        var values = ReadValues();
        values.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value)
    {
        var values = ReadValues();
        values[key] = value;
        WriteValues(values);
        return Task.CompletedTask;
    }

    public void Remove(string key)
    {
        var values = ReadValues();
        values.Remove(key);
        WriteValues(values);
    }

    public void RemoveAll() => WriteValues(new());

    private static string StoragePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RAGGit"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    private static Dictionary<string, string> ReadValues()
    {
        var path = StoragePath();
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var encrypted = File.ReadAllBytes(path);
            var decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.CurrentUser
            );
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void WriteValues(Dictionary<string, string> values)
    {
        try
        {
            var json = JsonSerializer.Serialize(values);
            var bytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StoragePath(), encrypted);
        }
        catch (Exception)
        {
            // Secure storage is best-effort; never crash.
        }
    }
}
