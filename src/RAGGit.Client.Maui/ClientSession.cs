namespace RAGGit.Client.Maui;

public sealed class ClientSession
{
    public string WorkstationUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IdentityType { get; set; } = "ApiKey";
    public string Role { get; set; } = string.Empty;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsValid => !string.IsNullOrWhiteSpace(WorkstationUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}
