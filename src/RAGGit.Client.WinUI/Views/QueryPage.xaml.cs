using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    public QueryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
    }
}
