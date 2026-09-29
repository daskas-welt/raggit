using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="IDialogService"/> backed by WPF-UI's <see cref="IContentDialogService"/>.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly IContentDialogService _contentDialogService;

    public WpfDialogService(IContentDialogService contentDialogService)
    {
        _contentDialogService = contentDialogService;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
        };

        var result = await _contentDialogService.ShowAsync(dialog, default);
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };

        await _contentDialogService.ShowAsync(dialog, default);
    }
}
