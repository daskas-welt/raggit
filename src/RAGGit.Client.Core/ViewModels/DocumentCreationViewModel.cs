using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>One explicit format choice shown by the Create Document page.</summary>
public sealed record DocumentFormatOption(DocumentOutputFormat Format, string Label);

/// <summary>
/// 034: own the Create Document page's description, explicit format choice,
/// generation lifecycle and save/open actions. No generated content is retained
/// beyond the returned bytes and the employee's chosen path.
/// </summary>
public sealed partial class DocumentCreationViewModel : ObservableObject
{
    private readonly DocumentCreationApiClient _apiClient;
    private readonly IFileSaveService _fileSaveService;
    private CancellationTokenSource? _cts;

    public DocumentCreationViewModel(
        DocumentCreationApiClient apiClient,
        IFileSaveService fileSaveService
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _fileSaveService =
            fileSaveService ?? throw new ArgumentNullException(nameof(fileSaveService));
    }

    public int MaxDescriptionLength => DocumentCreationRequest.MaxDescriptionLength;

    public IReadOnlyList<DocumentFormatOption> Formats { get; } =
    [
        new(DocumentOutputFormat.Docx, "Word (DOCX)"),
        new(DocumentOutputFormat.Xlsx, "Excel (XLSX)"),
        new(DocumentOutputFormat.Pdf, "PDF"),
        new(DocumentOutputFormat.Txt, "Text (TXT)"),
    ];

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DocumentOutputFormat? _selectedFormat;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _savedPath;

    [ObservableProperty]
    private bool _hasSavedFile;

    [ObservableProperty]
    private bool _hasStatusMessage;

    partial void OnDescriptionChanged(string value) =>
        CreateDocumentCommand.NotifyCanExecuteChanged();

    partial void OnSelectedFormatChanged(DocumentOutputFormat? value) =>
        CreateDocumentCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
    {
        CreateDocumentCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusMessageChanged(string? value) =>
        HasStatusMessage = !string.IsNullOrWhiteSpace(value);

    partial void OnSavedPathChanged(string? value)
    {
        HasSavedFile = !string.IsNullOrWhiteSpace(value);
        OpenDocumentCommand.NotifyCanExecuteChanged();
    }

    private bool CanCreateDocument() =>
        !string.IsNullOrWhiteSpace(Description) && SelectedFormat.HasValue && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCreateDocument))]
    private async Task CreateDocumentAsync()
    {
        if (SelectedFormat is not { } format)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var cancellationToken = cts.Token;

        IsBusy = true;
        StatusMessage = null;
        SavedPath = null;

        try
        {
            var file = await _apiClient.CreateAsync(Description, format, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var path = await _fileSaveService.SaveAsync(
                file.FileName,
                file.Bytes,
                file.ContentType,
                cancellationToken
            );

            if (path is not null)
            {
                SavedPath = path;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = null;
        }
        catch (HttpRequestException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (Exception)
        {
            StatusMessage = "The document could not be saved. No file was written.";
        }
        finally
        {
            IsBusy = false;
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
            }

            cts.Dispose();
        }
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private bool CanOpenDocument() => !string.IsNullOrWhiteSpace(SavedPath);

    [RelayCommand(CanExecute = nameof(CanOpenDocument))]
    private async Task OpenDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(SavedPath))
        {
            return;
        }

        try
        {
            await _fileSaveService.OpenAsync(SavedPath);
        }
        catch (Exception)
        {
            StatusMessage = "The saved file could not be opened.";
        }
    }
}
