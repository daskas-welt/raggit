using System;
using System.Windows.Controls;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Dialogs;

/// <summary>
/// Add-person form shown as in-window dialog content (feature 028): the
/// caller hosts it in a <c>ContentDialog</c> via the existing
/// <c>IContentDialogService</c> host and closes it on <see cref="RequestClose"/>.
/// ViewModel and behavior are unchanged from the former modal window.
/// </summary>
public partial class CreatePersonDialog : UserControl
{
    public AdminUsersViewModel ViewModel { get; }

    /// <summary>Raised when the form wants its host dialog closed.</summary>
    public event EventHandler? RequestClose;

    public CreatePersonDialog(AdminUsersViewModel viewModel)
    {
        ViewModel = viewModel;
        ViewModel.ClearStatus();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) => NewUsernameBox.Focus();
    }

    private async void OnCreateClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        await ViewModel.CreateUserCommand.ExecuteAsync(null);
        if (ViewModel.StatusSeverity == "Success")
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCancelClicked(object sender, System.Windows.RoutedEventArgs e) =>
        RequestClose?.Invoke(this, EventArgs.Empty);
}
