using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the document upload dialog (015-file-upload-ui).
/// Single-file state is preserved as a compatibility surface
/// (<see cref="SelectedFileName"/>, <see cref="UploadProgress"/>,
/// <see cref="PickFileCommand"/> adds one entry), backed by a multi-file
/// <see cref="Queue"/> uploaded sequentially with per-file progress and an
/// overall completed count. Admin gating and session handling are unchanged.
/// </summary>
public sealed partial class UploadViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Per-file outcome within one dialog open.
    /// </summary>
    public enum UploadItemState
    {
        Queued,
        Uploading,
        Succeeded,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// One queued file: identity, content, per-file progress and outcome.
    /// </summary>
    public sealed class UploadQueueItem : ObservableObject
    {
        private double _progress;
        private UploadItemState _state = UploadItemState.Queued;
        private string? _errorMessage;

        public UploadQueueItem(string fileName, Stream stream, string contentType, long? sizeBytes)
        {
            FileName = fileName;
            Stream = stream;
            ContentType = contentType;
            SizeBytes = sizeBytes;
        }

        public string FileName { get; }

        public Stream Stream { get; }

        public string ContentType { get; }

        public long? SizeBytes { get; }

        /// <summary>
        /// Result status text returned by the server (e.g. Indexing).
        /// </summary>
        public string? ResultStatus { get; internal set; }

        public double Progress
        {
            get => _progress;
            set
            {
                if (SetProperty(ref _progress, value))
                {
                    OnPropertyChanged(nameof(StateLabel));
                    OnPropertyChanged(nameof(IsIndexing));
                }
            }
        }

        public UploadItemState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(StateLabel));
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(IsUploading));
                }
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary>
        /// True while this file is actively uploading. The row progress bar
        /// binds to this so queued/succeeded/failed rows render no track.
        /// </summary>
        public bool IsUploading => State == UploadItemState.Uploading;

        /// <summary>
        /// The file bytes have reached the API, but the API has not returned
        /// yet. The workstation is still extracting, embedding, and writing
        /// the document to the RAG store, so the UI must show indeterminate
        /// indexing rather than a misleading completed progress bar.
        /// </summary>
        public bool IsIndexing => IsUploading && Progress >= 1;

        /// <summary>
        /// Stable unique AutomationId for the row remove button. FileName is
        /// immutable so no change notification is needed.
        /// </summary>
        public string RemoveAutomationId => $"RemoveFileButton_{ItemId:N}";

        public Guid ItemId { get; } = Guid.NewGuid();

        public string StateLabel =>
            State switch
            {
                UploadItemState.Queued => "Queued",
                UploadItemState.Uploading when IsIndexing => "Indexing...",
                UploadItemState.Uploading => $"Uploading {Progress:P0}",
                UploadItemState.Succeeded => "Uploaded",
                UploadItemState.Failed => "Failed",
                UploadItemState.Cancelled => "Cancelled",
                _ => State.ToString(),
            };

        public string FormattedSize =>
            SizeBytes switch
            {
                null => "Unknown size",
                >= 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
                >= 1024 => $"{SizeBytes / 1024.0:F1} KB",
                _ => $"{SizeBytes} bytes",
            };

        public string FileMetaText
        {
            get
            {
                var ext = Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant();
                return string.IsNullOrWhiteSpace(ext) ? FormattedSize : $"{ext} · {FormattedSize}";
            }
        }
    }

    /// <summary>
    /// Intake allow-list (018-allowed-upload-types). Single source of truth
    /// lives in <see cref="DocumentValidation.AllowedExtensions"/> so client
    /// and workstation gates can never drift.
    /// </summary>
    private static System.Collections.Generic.IReadOnlySet<string> AllowedExtensions =>
        DocumentValidation.AllowedExtensions;

    private static long MaxFileSizeBytes => DocumentValidation.MaxFileSizeBytes;

    /// <summary>
    /// Hint shown before selection; kept next to the pick action.
    /// </summary>
    public string SupportedTypesText =>
        $"{DocumentValidation.SupportedTypesLabel} supported · up to 100 MB.";

    private readonly DocumentsApiClient _apiClient;
    private readonly Services.IFilePicker _filePicker;
    private readonly ClientSession? _session;

    private CancellationTokenSource? _uploadCts;

    /// <summary>
    /// The in-flight upload run, if any. The hosting dialog awaits this
    /// during close (via a <c>ContentDialogClosingEventArgs</c> deferral)
    /// after cancelling, so queued streams are only disposed once the HTTP
    /// stack has finished aborting the TLS connection. Disposing them
    /// earlier surfaces as ObjectDisposedException on SslStream.
    /// </summary>
    public Task? UploadTask { get; private set; }

    public ObservableCollection<UploadQueueItem> Queue { get; } = new();

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

    /// <summary>
    /// InfoBar severity for <see cref="StatusMessage"/>:
    /// Informational, Success, Warning, or Error. The WinUI layer maps this
    /// string to InfoBarSeverity so Core stays UI-framework free.
    /// </summary>
    [ObservableProperty]
    private string _statusSeverity = "Informational";

    /// <summary>
    /// Pick-time rejection (unsupported type / oversize). Separate from the
    /// upload outcome so the message is immediate and the start action stays
    /// disabled until at least one valid file is queued.
    /// </summary>
    [ObservableProperty]
    private string? _rejectionMessage;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _hasFile;

    [ObservableProperty]
    private bool _isAllSucceeded;

    /// <summary>
    /// True while an accepted drag hovers the drop target (017).
    /// View-only affordance; drives the highlight visual state.
    /// </summary>
    [ObservableProperty]
    private bool _isDragOver;

    /// <summary>
    /// Drop target (and pick action) locked while uploading per the
    /// Session 2026-09-18 clarification: extra files wait for the next
    /// dialog open.
    /// </summary>
    public bool IsDropEnabled => !IsUploading;

    public string DropHintText =>
        IsUploading
            ? "Uploading — add more files after this run finishes."
            : "Drag files here or use Add files.";

    /// <summary>
    /// Clarifies that dismissing the dialog also stops the active upload.
    /// </summary>
    public string ClosingStopsUploadHint =>
        IsUploading ? "Closing this dialog stops the current upload." : string.Empty;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusError =>
        string.Equals(StatusSeverity, "Error", StringComparison.OrdinalIgnoreCase);

    public bool HasRejection => !string.IsNullOrWhiteSpace(RejectionMessage);

    public bool HasFiles => Queue.Count > 0;

    public bool CanSubmit =>
        IsUploading
        || Queue.Any(i =>
            i.State is UploadItemState.Queued or UploadItemState.Failed or UploadItemState.Cancelled
        );

    public int TotalCount => Queue.Count;

    public int SuccessCount => Queue.Count(i => i.State == UploadItemState.Succeeded);

    public int FailureCount => Queue.Count(i => i.State == UploadItemState.Failed);

    public int CancelledCount => Queue.Count(i => i.State == UploadItemState.Cancelled);

    public int CompletedCount => SuccessCount;

    public string CompletedCountText => $"{CompletedCount} of {TotalCount} done";

    /// <summary>
    /// Single queue header (Option A redesign): count, total size, and
    /// overall progress. This replaces the separate "N files ready" status
    /// message so the InfoBar only carries outcomes and errors.
    /// </summary>
    public string QueueHeaderText
    {
        get
        {
            var files = TotalCount == 1 ? "1 file" : $"{TotalCount} files";
            return $"{files} · {FormatSize(TotalSizeBytes)} · {CompletedCountText}";
        }
    }

    public long TotalSizeBytes
    {
        get
        {
            long total = 0;
            foreach (var item in Queue)
            {
                total += item.SizeBytes ?? 0;
            }

            return total;
        }
    }

    /// <summary>
    /// ContentDialog primary-button slot (Option A redesign): Upload when
    /// idle, Cancel while uploading.
    /// </summary>
    public string PrimaryButtonText => IsUploading ? "Cancel" : "Upload";

    public System.Windows.Input.ICommand PrimaryButtonCommand =>
        IsUploading ? CancelUploadCommand : UploadCommand;

    partial void OnIsUploadingChanged(bool value)
    {
        IsBusy = value;
        IsUploadEnabled = !value;
        OnPropertyChanged(nameof(IsDropEnabled));
        OnPropertyChanged(nameof(DropHintText));
        OnPropertyChanged(nameof(ClosingStopsUploadHint));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(PrimaryButtonCommand));
        OnPropertyChanged(nameof(CanSubmit));
        UploadCommand.NotifyCanExecuteChanged();
        RemoveFileCommand.NotifyCanExecuteChanged();
        PickFileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFileNameChanged(string? value)
    {
        HasFile = !string.IsNullOrWhiteSpace(value);
        UploadCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnStatusSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsStatusError));
    }

    partial void OnRejectionMessageChanged(string? value) =>
        OnPropertyChanged(nameof(HasRejection));

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
        Queue.CollectionChanged += OnQueueCollectionChanged;
    }

    public void RefreshRole() => IsAdmin = _session?.IsAdmin ?? IsAdmin;

    /// <summary>
    /// Releases queued file streams. The VM is transient per dialog open, so
    /// without this the queued streams stay open when the dialog is
    /// discarded. Call after cancelling any in-flight upload.
    /// </summary>
    public void Dispose()
    {
        foreach (var item in Queue)
        {
            try
            {
                item.Stream.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already removed/disposed — the queue entry is what matters.
            }
        }
        _uploadCts?.Dispose();
        _uploadCts = null;
        Queue.CollectionChanged -= OnQueueCollectionChanged;
    }

    private void OnQueueCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var removed in e.OldItems.OfType<UploadQueueItem>())
            {
                removed.PropertyChanged -= OnQueueItemPropertyChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (var added in e.NewItems.OfType<UploadQueueItem>())
            {
                added.PropertyChanged += OnQueueItemPropertyChanged;
            }
        }
        RefreshQueueDerived();
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only State changes affect VM-level derived state (counts, gating).
        // Progress ticks bind straight to the row ProgressBar; refreshing
        // command executability per tick is wasted UI work.
        if (e.PropertyName == nameof(UploadQueueItem.State))
        {
            RefreshQueueDerived();
        }
    }

    private void RefreshQueueDerived()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SuccessCount));
        OnPropertyChanged(nameof(FailureCount));
        OnPropertyChanged(nameof(CancelledCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(CompletedCountText));
        OnPropertyChanged(nameof(QueueHeaderText));
        OnPropertyChanged(nameof(TotalSizeBytes));
        UploadCommand.NotifyCanExecuteChanged();
        RemoveFileCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPick))]
    private async Task PickFileAsync()
    {
        if (IsUploading)
        {
            return;
        }

        try
        {
            var picked = await _filePicker.PickMultipleAsync();
            await AddPickedFilesAsync(picked);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not open file picker: {exception.Message}";
            StatusSeverity = "Error";
        }
    }

    private bool CanPick => !IsUploading;

    /// <summary>
    /// Shared intake seam for picker passes and drop passes (017): validates
    /// every file identically, queues the valid ones, and reports each
    /// rejected file by name. Never throws for per-file validation failures.
    /// </summary>
    public Task AddPickedFilesAsync(
        IEnumerable<PickedFile> files,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        var rejections = new List<string>();
        var added = 0;
        foreach (var picked in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (picked is null)
            {
                continue;
            }

            var fileName = picked.FileName?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                picked.Stream.Dispose();
                rejections.Add("A file has no name. Choose a named document to upload.");
                continue;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                picked.Stream.Dispose();
                rejections.Add(
                    $"'{fileName}' isn't supported. Choose {DocumentValidation.SupportedTypesLabel} files."
                );
                continue;
            }

            long? sizeBytes = picked.Stream.CanSeek ? picked.Stream.Length : null;
            if (sizeBytes is 0 || sizeBytes > MaxFileSizeBytes)
            {
                picked.Stream.Dispose();
                rejections.Add(
                    sizeBytes is 0
                        ? $"'{fileName}' is empty. Choose a file with content."
                        : $"'{fileName}' is too large ({FormatSize(sizeBytes)}). Files must be 100 MB or smaller."
                );
                continue;
            }

            var contentType = string.IsNullOrWhiteSpace(picked.ContentType)
                ? ContentTypeFor(extension)
                : picked.ContentType;

            Queue.Add(new UploadQueueItem(fileName, picked.Stream, contentType, sizeBytes));
            SelectedFileName = fileName;
            UploadProgress = 0;
            IsAllSucceeded = false;
            added++;
        }

        if (added > 0)
        {
            // The queue header carries count/size/progress; the InfoBar is
            // reserved for outcomes and errors.
            StatusMessage = null;
            StatusSeverity = "Informational";
        }

        if (rejections.Count > 0)
        {
            RejectionMessage = string.Join(" ", rejections);
        }
        else if (added > 0)
        {
            RejectionMessage = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reports drop entries that are not real files (folders, shortcuts,
    /// virtual items) without attempting resolution (clarified 2026-09-18).
    /// Merges into the pick-time rejection surface.
    /// </summary>
    public void ReportRejectedNames(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var messages = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n =>
                $"'{n.Trim()}' isn't a file that can be uploaded. Drop individual supported files instead."
            )
            .ToList();
        if (messages.Count > 0)
        {
            MergeRejection(string.Join(" ", messages));
        }
    }

    /// <summary>
    /// Reports a drop-handling failure (e.g. unreadable dropped content).
    /// Merges into the pick-time rejection surface.
    /// </summary>
    public void ReportDropError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        MergeRejection(message.Trim());
    }

    private void MergeRejection(string message)
    {
        RejectionMessage = string.IsNullOrWhiteSpace(RejectionMessage)
            ? message
            : RejectionMessage + " " + message;
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void RemoveFile(UploadQueueItem? item)
    {
        if (item is null || IsUploading || !Queue.Contains(item))
        {
            return;
        }

        Queue.Remove(item);
        try
        {
            item.Stream.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already gone — the queue entry is what matters.
        }

        IsAllSucceeded = false;
        if (Queue.Count == 0)
        {
            SelectedFileName = null;
            UploadProgress = 0;
            if (string.IsNullOrWhiteSpace(RejectionMessage))
            {
                StatusMessage = null;
                StatusSeverity = "Informational";
            }
        }
        else
        {
            SelectedFileName = Queue[^1].FileName;
            StatusMessage = null;
            StatusSeverity = "Informational";
        }
    }

    private bool CanRemove(UploadQueueItem? item) =>
        !IsUploading && item is not null && Queue.Contains(item);

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        var run = UploadAsyncCore();
        UploadTask = run;
        try
        {
            await run;
        }
        finally
        {
            if (ReferenceEquals(UploadTask, run))
            {
                UploadTask = null;
            }
        }
    }

    private async Task UploadAsyncCore()
    {
        var pending = Queue.Where(i => i.State == UploadItemState.Queued).ToList();
        if (pending.Count == 0)
        {
            // Retry without re-picking: rewind and re-queue only retryable
            // outcomes (Failed/Cancelled). Succeeded items are left alone so
            // a retry never uploads the same file twice.
            var retryable = Queue
                .Where(i => i.State is UploadItemState.Failed or UploadItemState.Cancelled)
                .ToList();
            if (retryable.Count == 0)
            {
                if (Queue.Count == 0)
                {
                    StatusMessage = "No file selected.";
                    StatusSeverity = "Warning";
                }
                // Otherwise everything already succeeded — nothing to do.
                return;
            }

            // Rewind so a retry without re-picking resends the full content
            // instead of zero bytes (ProgressStream consumes to EOF).
            foreach (var item in retryable)
            {
                TryRewind(item.Stream);
                item.ErrorMessage = null;
                item.Progress = 0;
                item.ResultStatus = null;
                item.State = UploadItemState.Queued;
            }
            pending = Queue.Where(i => i.State == UploadItemState.Queued).ToList();
        }

        IsUploading = true;
        IsAllSucceeded = false;
        StatusMessage = null;
        StatusSeverity = "Informational";

        _uploadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _uploadCts = cts;

        RAGGit.Core.Models.Document? lastDocument = null;
        try
        {
            foreach (var item in pending)
            {
                if (cts.IsCancellationRequested)
                {
                    break;
                }

                var current = item;
                current.State = UploadItemState.Uploading;
                UploadProgress = 0;
                var progress = new Progress<double>(p =>
                {
                    current.Progress = p;
                    UploadProgress = p;
                });

                try
                {
                    var document = await _apiClient.UploadAsync(
                        current.Stream,
                        current.FileName,
                        current.ContentType,
                        progress,
                        cts.Token
                    );

                    lastDocument = document;
                    current.Progress = 1;
                    current.ResultStatus = document.Status.ToString();
                    current.ErrorMessage = null;
                    current.State = UploadItemState.Succeeded;
                    UploadProgress = 1;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    current.ErrorMessage = "Upload cancelled.";
                    current.State = UploadItemState.Cancelled;
                    break;
                }
                catch (ObjectDisposedException) when (cts.IsCancellationRequested)
                {
                    // Teardown race: the host disposed queued streams (or the
                    // HTTP stack aborted the TLS connection) after cancel was
                    // requested but before the request finished unwinding.
                    // This is a cancel, not a failure.
                    current.ErrorMessage = "Upload cancelled.";
                    current.State = UploadItemState.Cancelled;
                    break;
                }
                catch (Exception exception)
                {
                    current.ErrorMessage = MapUploadError(exception);
                    current.State = UploadItemState.Failed;
                }
                finally
                {
                    // Rewind so a retry without re-picking resends the full
                    // content instead of 0 bytes (ProgressStream consumes to EOF).
                    TryRewind(current.Stream);
                }
            }

            var total = Queue.Count;
            var succeeded = SuccessCount;
            var failed = FailureCount;
            var cancelled = CancelledCount;

            if (total > 0 && succeeded == total)
            {
                IsAllSucceeded = true;
                StatusMessage =
                    total == 1 && lastDocument is not null
                        ? $"Uploaded {lastDocument.Filename} ({lastDocument.Status})."
                        : $"Uploaded {succeeded} of {total} files.";
                StatusSeverity = "Success";
            }
            else if (cancelled > 0 && succeeded == 0 && failed == 0)
            {
                StatusMessage = "Upload cancelled.";
                StatusSeverity = "Warning";
            }
            else if (succeeded > 0)
            {
                StatusMessage =
                    $"Uploaded {succeeded} of {total} files. {failed} failed, {cancelled} cancelled. "
                    + "Press Upload to retry the rest, or remove files you no longer need.";
                StatusSeverity = "Error";
            }
            else
            {
                var firstError = Queue
                    .Select(i => i.ErrorMessage)
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                StatusMessage =
                    $"{firstError ?? "Upload failed."} Pick another file or press Upload to retry.";
                StatusSeverity = "Error";
            }
        }
        finally
        {
            IsUploading = false;
            if (ReferenceEquals(_uploadCts, cts))
            {
                _uploadCts = null;
            }
            cts.Dispose();
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

    private bool CanUpload =>
        !IsUploading
        && Queue.Any(i =>
            i.State is UploadItemState.Queued or UploadItemState.Failed or UploadItemState.Cancelled
        );

    private static string MapUploadError(Exception exception)
    {
        if (exception is UnsupportedDocumentTypeException unsupported)
        {
            return unsupported.Message;
        }
        if (exception is UploadRejectedException rejected)
        {
            return rejected.Message;
        }
        if (
            exception is HttpRequestException http
            && (
                http.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || http.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return "Forbidden: you do not have permission to upload documents (Admin only).";
        }
        if (exception is HttpRequestException || exception is TaskCanceledException)
        {
            return ClientErrorText.CannotReach(exception.Message);
        }
        return $"Upload failed: {exception.Message}";
    }

    private static void TryRewind(Stream stream)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is IOException)
        {
            // Stream already gone; the next Upload re-picks via CanUpload state.
        }
    }

    private static string ContentTypeFor(string extension) =>
        extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };

    private static string FormatSize(long? sizeBytes) =>
        sizeBytes switch
        {
            null => "unknown size",
            >= 1024 * 1024 => $"{sizeBytes / (1024.0 * 1024):F1} MB",
            >= 1024 => $"{sizeBytes / 1024.0:F1} KB",
            _ => $"{sizeBytes} bytes",
        };
}
