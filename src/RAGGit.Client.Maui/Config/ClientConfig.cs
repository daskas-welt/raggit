using System;
using System.Collections.Generic;
using System.IO;

namespace RAGGit.Client.Maui.Config;

public sealed record ClientConfigResult(bool IsValid, string? WorkstationUrl, string? ApiKey, string? Error, bool HttpAttempted = false);

public static class ClientConfigResolver
{
    public static ClientConfigResult Resolve(IDictionary<string, string?> config)
    {
        config.TryGetValue("Workstation:Url", out var url);
        var apiKey = ResolveApiKey(config);

        if (string.IsNullOrWhiteSpace(url))
            return new ClientConfigResult(false, null, null, "Missing Workstation:Url", HttpAttempted: false);
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ClientConfigResult(false, null, null, "Missing ApiKey (Workstation:ApiKey or Api:AdminKey/Api:EmployeeKey)", HttpAttempted: false);

        // Validate URL shape without attempting HTTP
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            return new ClientConfigResult(false, null, null, "Invalid Workstation:Url", HttpAttempted: false);

        return new ClientConfigResult(true, url, apiKey, null, HttpAttempted: false);
    }

    public static string? ResolveApiKey(IDictionary<string, string?> config)
    {
        if (config.TryGetValue("Workstation:ApiKey", out var wk) && !string.IsNullOrWhiteSpace(wk))
            return wk;
        if (config.TryGetValue("Api:AdminKey", out var ak) && !string.IsNullOrWhiteSpace(ak))
            return ak;
        if (config.TryGetValue("Api:EmployeeKey", out var ek) && !string.IsNullOrWhiteSpace(ek))
            return ek;
        return null;
    }

    public static bool HasHardcodedUrlOrKey(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
            return false;
        var disallowed = new[] { "http://localhost:5001", "http://ai-workstation", "dev-admin-key", "dev-employee-key" };
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            // Skip this resolver itself and generated files
            if (file.EndsWith("ClientConfig.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var text = File.ReadAllText(file);
            foreach (var lit in disallowed)
            {
                // Allow literals inside comments? We just disallow raw string literals that are hard-coded URLs/keys
                // Simple check: if file contains lit outside of appsettings.json reference, flag
                if (text.Contains(lit, StringComparison.Ordinal))
                {
                    // If hard-coded in a string literal for default config, it's a violation
                    // Quick heuristic: ignore if it's inside a comment line starting with //
                    var lines = text.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//")) continue;
                        if (line.Contains(lit, StringComparison.Ordinal))
                            return true;
                    }
                }
            }
        }
        return false;
    }
}
