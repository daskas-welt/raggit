using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RAGGit.Client.Maui;

public sealed class ClientSession : INotifyPropertyChanged
{
    private string _role = string.Empty;

    public string WorkstationUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IdentityType { get; set; } = "ApiKey";
    public string? Username { get; set; }
    public string? DisplayName { get; set; }

    public string Role
    {
        get => _role;
        set
        {
            if (_role == value)
                return;
            _role = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(WorkstationUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
