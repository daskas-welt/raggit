using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the library list view.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly DocumentsApiClient _apiClient;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _errorMessage;

    public LibraryViewModel(DocumentsApiClient apiClient, string role = "Employee")
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        IsAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task LoadDocumentsAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var documents = await _apiClient.GetDocumentsAsync();
            Documents = new ObservableCollection<Document>(documents);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to load library: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        // Navigation is handled by the view (Shell/MVVM messenger).
        // This command is a seam for view-specific navigation logic.
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            await _apiClient.DeleteAsync(document.Id);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to delete {document.Filename}: {exception.Message}";
        }
    }
}
