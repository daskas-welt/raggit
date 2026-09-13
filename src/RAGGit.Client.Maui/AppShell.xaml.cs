#if MAUI
using Microsoft.Extensions.DependencyInjection;
#endif

namespace RAGGit.Client.Maui;

public partial class AppShell : Shell
{
#if MAUI
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = services.GetRequiredService<ClientSession>();
    }
#else
    public AppShell()
    {
        InitializeComponent();
    }
#endif
}
