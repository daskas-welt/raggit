using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// ViewModel for the per-person query detail view (005-per-person-history US2).
/// Loads one owned query (full answer + ordered citations) via
/// <see cref="QueryHistoryApiClient.GetDetailAsync"/>; non-owned ids surface
/// the 404 as an error message instead of another person's data. It also saves
/// the reopened answer as a document (033 US3).
/// </summary>
public sealed partial class QueryDetailViewModel : ObservableObject
{
    private readonly QueryHistoryApiClient _apiClient;
    private readonly DocumentApiClient _documentApiClient;
    private readonly IFileSaveService _fileSaveService;

    [ObservableProperty]
    private QueryDetail? _detail;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isDocumentBusy;

    [ObservableProperty]
    private string? _documentStatus;

    [ObservableProperty]
    private string? _savedPath;

    /// <summary>True when the reopened answer can be rendered as a document.</summary>
    public bool CanSave =>
        Detail is not null
        && Detail.Citations.Count > 0
        && !string.Equals(
            Detail.Answer.Trim(),
            "no relevant content found",
            StringComparison.OrdinalIgnoreCase
        );

    partial void OnDetailChanged(QueryDetail? value) => OnPropertyChanged(nameof(CanSave));

    public QueryDetailViewModel(
        QueryHistoryApiClient apiClient,
        DocumentApiClient documentApiClient,
        IFileSaveService fileSaveService
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _documentApiClient =
            documentApiClient ?? throw new ArgumentNullException(nameof(documentApiClient));
        _fileSaveService =
            fileSaveService ?? throw new ArgumentNullException(nameof(fileSaveService));
    }

    [RelayCommand]
    private async Task LoadDetailAsync(Guid id)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            Detail = await _apiClient.GetDetailAsync(id);
        }
        catch (HttpRequestException ex)
        {
            Detail = null;
            ErrorMessage = ex.Message;
        }
        catch (Exception exception)
        {
            Detail = null;
            ErrorMessage = $"Failed to load query detail: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveDocumentAsync(DocumentOutputFormat format)
    {
        if (Detail is null || Detail.Citations.Count == 0 || IsDocumentBusy)
        {
            return;
        }

        if (
            string.Equals(
                Detail.Answer.Trim(),
                "no relevant content found",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        try
        {
            IsDocumentBusy = true;
            DocumentStatus = null;
            var file = await _documentApiClient.GetAsync(Detail.Id, format, CancellationToken.None);
            var path = await _fileSaveService.SaveAsync(
                file.FileName,
                file.Bytes,
                file.ContentType,
                CancellationToken.None
            );

            if (path is not null)
            {
                SavedPath = path;
                DocumentStatus = $"Saved {System.IO.Path.GetFileName(path)}.";
            }
        }
        catch (HttpRequestException exception)
        {
            DocumentStatus = exception.Message;
        }
        catch (Exception)
        {
            DocumentStatus = "Document generation failed. No file was saved.";
        }
        finally
        {
            IsDocumentBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDocumentAsync()
    {
        if (SavedPath is not { } path)
        {
            return;
        }

        try
        {
            await _fileSaveService.OpenAsync(path);
        }
        catch (Exception)
        {
            DocumentStatus = "Could not open the saved document.";
        }
    }
}
