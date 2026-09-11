using System;
using System.IO;
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
    private readonly IFilePicker _filePicker;
    private readonly ClientSession? _session;

    private Stream? _selectedFileStream;
    private string? _selectedContentType;

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

    partial void OnIsUploadingChanged(bool value)
    {
        IsBusy = value;
        IsUploadEnabled = !value;
    }

    public UploadViewModel(DocumentsApiClient apiClient, IFilePicker filePicker, ClientSession? session = null)
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

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        if (_selectedFileStream is null || string.IsNullOrWhiteSpace(SelectedFileName) || string.IsNullOrWhiteSpace(_selectedContentType))
        {
            StatusMessage = "No file selected.";
            return;
        }

        IsUploading = true;
        StatusMessage = "Uploading...";
        UploadProgress = 0;

        try
        {
            var progress = new Progress<double>(p => UploadProgress = p);
            var document = await _apiClient.UploadAsync(
                _selectedFileStream,
                SelectedFileName,
                _selectedContentType,
                progress);

            StatusMessage = $"Uploaded {document.Filename} ({document.Status}).";
        }
        catch (UnsupportedDocumentTypeException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Forbidden: you do not have permission to upload documents (Admin only).";
        }
        catch (Exception exception)
        {
            if (exception is HttpRequestException || exception is TaskCanceledException)
                StatusMessage = $"cannot reach AI workstation: {exception.Message}";
            else
                StatusMessage = $"Upload failed: {exception.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }

    private bool CanUpload => !IsUploading && !string.IsNullOrWhiteSpace(SelectedFileName);
}
