using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    public QueryPage()
    {
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
