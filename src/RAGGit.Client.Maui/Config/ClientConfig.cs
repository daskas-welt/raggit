using System;

namespace RAGGit.Client.Maui.Config;

public sealed record ClientConfigResult(bool IsValid, string? WorkstationUrl, string? ApiKey, string? Error, bool HttpAttempted = false);

public static class ClientConfigResolver
{
    // Stub for T006 red — real binding lands in T010.
    public static ClientConfigResult Resolve(IDictionary<string, string?> config)
    {
        // Stub always returns valid to make red observable
        var url = config.TryGetValue("Workstation:Url", out var u) ? u : null;
        var key = config.TryGetValue("Workstation:ApiKey", out var k) ? k : null;
        return new ClientConfigResult(true, url ?? "http://stub", key ?? "stub-key", null, HttpAttempted: false);
    }

    public static string? ResolveApiKey(IDictionary<string, string?> config)
    {
        // Stub always prefers Workstation:ApiKey even if missing, to make precedence test fail
        if (config.TryGetValue("Workstation:ApiKey", out var wk) && !string.IsNullOrWhiteSpace(wk))
            return wk;
        if (config.TryGetValue("Api:AdminKey", out var ak) && !string.IsNullOrWhiteSpace(ak))
            return ak;
        return null;
    }

    public static bool HasHardcodedUrlOrKey(string sourceRoot)
    {
        return false; // stub
    }
}
