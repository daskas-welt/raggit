using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Models;
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
    private readonly ILibraryPreferences _preferences;
    private readonly ILauncherService _launcher;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _pageSize;

    [ObservableProperty]
    private int _pageNumber = 1;

    public LibraryViewModel(
        DocumentsApiClient apiClient,
        ClientSession session,
        ILauncherService launcher,
        ILibraryPreferences? preferences = null
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        IsAdmin = _session.IsAdmin;
        _preferences = preferences ?? new InMemoryLibraryPreferences();
        _pageSize = _preferences.GetPageSize();
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <summary>
    /// Refresh IsAdmin from current session (call after role discovery).
    /// </summary>
    public void RefreshRole() => IsAdmin = _session.IsAdmin;

    /// <summary>Full-list count; the page slice below derives from it.</summary>
    public int TotalCount => Documents.Count;

    /// <summary>Always ≥ 1, even for an empty library.</summary>
    public int TotalPages => LibraryPage.Create(Documents, PageNumber, PageSize).TotalPages;

    /// <summary>Current slice in server order; empty only when the library is empty.</summary>
    public IReadOnlyList<Document> PageItems => CurrentPage.Items;

    /// <summary>"Showing X–Y of Z entries" (or the zero state).</summary>
    public string StatusText => CurrentPage.StatusText;

    public bool HasPrevious => CurrentPage.HasPrevious;

    public bool HasNext => CurrentPage.HasNext;

    /// <summary>Footer tokens: first/last, nearby window, collapsed ellipses.</summary>
    public IReadOnlyList<PageToken> VisibleSequence => PageSequence.For(PageNumber, TotalPages);

    /// <summary>Selectable page sizes for the footer Picker.</summary>
    public int[] PageSizeOptions => LibraryPageSizes.Valid;

    private LibraryPage CurrentPage => LibraryPage.Create(Documents, PageNumber, PageSize);

    partial void OnPageSizeChanged(int value)
    {
        PageNumber = 1;
        RefreshPaging();
    }

    partial void OnPageNumberChanged(int value) => RefreshPaging();

    partial void OnDocumentsChanged(ObservableCollection<Document> value)
    {
        PageNumber = LibraryPage.Create(value, PageNumber, PageSize).PageNumber;
        RefreshPaging();
    }

    private void RefreshPaging()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageItems));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(HasNext));
        OnPropertyChanged(nameof(VisibleSequence));
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        PageNumber = Math.Clamp(page, 1, TotalPages);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (HasNext)
        {
            PageNumber++;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (HasPrevious)
        {
            PageNumber--;
        }
    }

    [RelayCommand]
    private void FirstPage() => PageNumber = 1;

    [RelayCommand]
    private void LastPage() => PageNumber = TotalPages;

    [RelayCommand]
    private void ChangePageSize(int size)
    {
        var valid = LibraryPageSizes.IsValid(size) ? size : LibraryPageSizes.Default;
        _preferences.SetPageSize(valid);
        PageSize = valid;
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
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "model unavailable offline",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = "model unavailable offline";
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "cannot reach AI workstation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ClientErrorText.CannotReach(ex.Message);
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "AI workstation unavailable",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
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

    /// <summary>Inline row action (from the "⋯" button). The view confirms before executing.</summary>
    [RelayCommand]
    private async Task RowActionAsync(Document? document)
    {
        if (document is null)
            return;

        await DeleteDocumentAsync(document);
        var deleteError = ErrorMessage;
        await LoadDocumentsAsync();
        // LoadDocumentsAsync clears ErrorMessage on entry: restore the delete
        // failure (if any) so a failed delete is never reported as success.
        if (ErrorMessage is null)
            ErrorMessage = deleteError;
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            await _apiClient.DeleteAsync(document.Id);
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            )
        {
            ErrorMessage =
                $"Forbidden: you do not have permission to delete documents (Admin only).";
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "model unavailable offline",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = "model unavailable offline";
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "cannot reach AI workstation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ClientErrorText.CannotReach(ex.Message);
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "AI workstation unavailable",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ex.Message;
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to delete {document.Filename}: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadAndOpenAsync(Document? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            var (bytes, filename) = await _apiClient.GetContentAsync(document.Id);
            await _launcher.OpenAsync(filename, bytes, document.Mime.GetContentType());
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains("original unavailable", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Original unavailable for this document.";
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "cannot reach AI workstation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ClientErrorText.CannotReach(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Failed to download {document.Filename}: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to download {document.Filename}: {exception.Message}";
        }
    }
}
