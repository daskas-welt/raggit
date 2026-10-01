using System;
using System.IO;
using System.Text.Json;
using RAGGit.Client.Core.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="ILibraryPreferences"/> backed by a JSON file under LocalAppData.
/// </summary>
public sealed class WpfLibraryPreferences : ILibraryPreferences
{
    private const string FileName = "prefs.json";
    private const string Key = "RAGGit.LibraryPageSize";

    public int GetPageSize()
    {
        var raw = ReadSetting(Key, string.Empty);
        return int.TryParse(raw, out var parsed) && LibraryPageSizes.IsValid(parsed)
            ? parsed
            : LibraryPageSizes.Default;
    }

    public void SetPageSize(int size) => WriteSetting(Key, size.ToString());

    private static string AppDataDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RAGGit"
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ReadSetting(string key, string fallback)
    {
        try
        {
            var path = Path.Combine(AppDataDir(), FileName);
            if (!File.Exists(path))
            {
                return fallback;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (
                doc.RootElement.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String
            )
            {
                return value.GetString() ?? fallback;
            }
        }
        catch (Exception)
        {
            // Corrupt cache must never crash the app; fall back.
        }

        return fallback;
    }

    private static void WriteSetting(string key, string value)
    {
        try
        {
            var path = Path.Combine(AppDataDir(), FileName);
            Dictionary<string, string> map = new();
            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            map[prop.Name] = prop.Value.GetString() ?? string.Empty;
                        }
                    }
                }
                catch (Exception)
                {
                    // Overwrite corrupt cache below.
                }
            }

            map[key] = value;
            File.WriteAllText(path, JsonSerializer.Serialize(map));
        }
        catch (Exception)
        {
            // Preferences are best-effort; never crash.
        }
    }
}
