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
    private readonly ClientSession _session;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _errorMessage;

    public LibraryViewModel(DocumentsApiClient apiClient, ClientSession session)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        IsAdmin = _session.IsAdmin;
    }

    /// <summary>
    /// Refresh IsAdmin from current session (call after role discovery).
    /// </summary>
    public void RefreshRole() => IsAdmin = _session.IsAdmin;

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
        catch (HttpRequestException ex) when (ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "model unavailable offline";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
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
    private async Task RetryAsync()
    {
        await LoadDocumentsAsync();
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
        catch (HttpRequestException ex) when (ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Forbidden: you do not have permission to delete documents (Admin only).";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "model unavailable offline";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = ex.Message;
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to delete {document.Filename}: {exception.Message}";
        }
    }
}
