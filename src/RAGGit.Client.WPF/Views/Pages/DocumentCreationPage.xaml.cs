using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

/// <summary>034: compose a new document from a description and save it locally.</summary>
public partial class DocumentCreationPage : Page
{
    public DocumentCreationViewModel ViewModel { get; }

    public DocumentCreationPage()
    {
        ViewModel = App.Services.GetRequiredService<DocumentCreationViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CancelCommand.CanExecute(null))
        {
            ViewModel.CancelCommand.Execute(null);
        }
    }
}
