using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the document upload view.
/// </summary>
public sealed partial class UploadViewModel : ObservableObject
{
    private readonly DocumentsApiClient _apiClient;
    private readonly Services.IFilePicker _filePicker;
    private readonly ClientSession? _session;

    private Stream? _selectedFileStream;
    private string? _selectedContentType;
    private CancellationTokenSource? _uploadCts;

    [ObservableProperty]
    private string? _selectedFileName;

    [ObservableProperty]
    private double _uploadProgress;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isUploadEnabled = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _hasFile;

    partial void OnIsUploadingChanged(bool value)
    {
        IsBusy = value;
        IsUploadEnabled = !value;
    }

    partial void OnSelectedFileNameChanged(string? value)
    {
        HasFile = !string.IsNullOrWhiteSpace(value);
        UploadCommand.NotifyCanExecuteChanged();
    }

    public UploadViewModel(
        DocumentsApiClient apiClient,
        Services.IFilePicker filePicker,
        ClientSession? session = null
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _session = session;
        IsAdmin = _session?.IsAdmin ?? true; // fallback true to keep existing behavior until wired
    }

    public void RefreshRole() => IsAdmin = _session?.IsAdmin ?? IsAdmin;

    [RelayCommand]
    private async Task PickFileAsync()
    {
        try
        {
            var picked = await _filePicker.PickAsync();
            if (picked is null)
            {
                return;
            }

            _selectedFileStream?.Dispose();
            _selectedFileStream = picked.Stream;
            _selectedContentType = picked.ContentType;
            SelectedFileName = picked.FileName;
            StatusMessage = null;
            UploadProgress = 0;
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not open file picker: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        if (
            _selectedFileStream is null
            || string.IsNullOrWhiteSpace(SelectedFileName)
            || string.IsNullOrWhiteSpace(_selectedContentType)
        )
        {
            StatusMessage = "No file selected.";
            return;
        }

        IsUploading = true;
        StatusMessage = "Uploading...";
        UploadProgress = 0;

        _uploadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _uploadCts = cts;

        try
        {
            var progress = new Progress<double>(p => UploadProgress = p);
            var document = await _apiClient.UploadAsync(
                _selectedFileStream,
                SelectedFileName,
                _selectedContentType,
                progress,
                cts.Token
            );

            StatusMessage = $"Uploaded {document.Filename} ({document.Status}).";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            StatusMessage = "Upload cancelled.";
        }
        catch (UnsupportedDocumentTypeException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            )
        {
            StatusMessage =
                "Forbidden: you do not have permission to upload documents (Admin only).";
        }
        catch (Exception exception)
        {
            if (exception is HttpRequestException || exception is TaskCanceledException)
                StatusMessage = ClientErrorText.CannotReach(exception.Message);
            else
                StatusMessage = $"Upload failed: {exception.Message}";
        }
        finally
        {
            IsUploading = false;
            if (ReferenceEquals(_uploadCts, cts))
            {
                _uploadCts = null;
            }
            cts.Dispose();
            // Rewind so a second Upload press without re-picking resends the
            // full content instead of 0 bytes (ProgressStream consumes to EOF).
            try
            {
                if (_selectedFileStream?.CanSeek == true)
                {
                    _selectedFileStream.Position = 0;
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is IOException)
            {
                // Stream already gone; next Upload re-picks via CanUpload state.
            }
        }
    }

    [RelayCommand]
    private void CancelUpload()
    {
        try
        {
            _uploadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already finished and disposed — nothing to cancel.
        }
    }

    private bool CanUpload => !IsUploading && !string.IsNullOrWhiteSpace(SelectedFileName);
}
