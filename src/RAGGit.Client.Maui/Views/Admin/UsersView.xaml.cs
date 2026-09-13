namespace RAGGit.Client.Maui.Views.Admin;

public partial class UsersView : ContentPage
{
    public UsersView()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is ViewModels.AdminUsersViewModel vm)
        {
            await vm.LoadUsersCommand.ExecuteAsync(null);
        }
    }
}
