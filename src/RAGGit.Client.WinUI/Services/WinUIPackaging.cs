using System;
using System.IO;
using System.Text.Json;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// Unpackaged (Debug F5) vs packaged (MSIX) environment detection.
/// <see cref="Windows.Storage.ApplicationData.Current"/> and
/// <see cref="Windows.ApplicationModel.Package.Current"/> throw without
/// package identity, so unpackaged runs fall back to plain LocalAppData paths.
/// </summary>
internal static class WinUIPackaging
{
    private static bool? _isPackaged;

    public static bool IsPackaged
    {
        get
        {
            if (_isPackaged is null)
            {
                try
                {
                    _ = Windows.ApplicationModel.Package.Current;
                    _isPackaged = true;
                }
                catch (Exception)
                {
                    _isPackaged = false;
                }
            }

            return _isPackaged.Value;
        }
    }

    /// <summary>
    /// Writable app-local directory in both environments.
    /// </summary>
    public static string AppDataDir()
    {
        if (IsPackaged)
        {
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RAGGit"
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ReadJsonSetting(string fileName, string key, string fallback)
    {
        try
        {
            var path = Path.Combine(AppDataDir(), fileName);
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

    public static void WriteJsonSetting(string fileName, string key, string value)
    {
        try
        {
            var path = Path.Combine(AppDataDir(), fileName);
            System.Collections.Generic.Dictionary<string, string> map = new();
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
