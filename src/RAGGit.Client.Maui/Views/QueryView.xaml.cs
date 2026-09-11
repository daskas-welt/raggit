using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.Maui.Views;

public partial class QueryView : ContentPage
{
    public QueryView()
    {
        InitializeComponent();
    }

    private void OnSuggestionTapped(object sender, EventArgs e)
    {
        // Forward Syncfusion suggestion chip (e.g., "Summarize citations") to ViewModel AskCommand
        if (BindingContext is QueryViewModel vm && sender is Syncfusion.Maui.AIAssistView.SfAIAssistView assist)
        {
            // Placeholder: SfAIAssistView SuggestionItemTapped args carry Text; wire to QueryText for retry
        }
    }
}
