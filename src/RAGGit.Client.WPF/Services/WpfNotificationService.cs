using System;
using RAGGit.Client.Core.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="INotificationService"/> backed by WPF-UI's <see cref="ISnackbarService"/>
/// (its presenter is already wired in the shell's MainWindow).
/// </summary>
public sealed class WpfNotificationService : INotificationService
{
    private static readonly TimeSpan DismissAfter = TimeSpan.FromSeconds(5);

    private readonly ISnackbarService _snackbarService;

    public WpfNotificationService(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }

    public void Show(string title, string message, NotificationKind kind)
    {
        _snackbarService.Show(title, message, ToAppearance(kind), null, DismissAfter);
    }

    private static ControlAppearance ToAppearance(NotificationKind kind) =>
        kind switch
        {
            NotificationKind.Success => ControlAppearance.Success,
            NotificationKind.Danger => ControlAppearance.Danger,
            _ => ControlAppearance.Secondary,
        };
}
