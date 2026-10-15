using CommunityToolkit.Mvvm.ComponentModel;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>
/// One transient reasoning row shown while a question is in flight (032).
/// The workstation renders <see cref="Text"/>; this type only carries it to the
/// view. It is never persisted, never attached to a <see cref="ChatMessage"/>,
/// and never stored in the conversation (FR-014).
/// </summary>
public sealed partial class ReasoningStep : ObservableObject
{
    /// <summary>Language-independent identity of the work being described.</summary>
    public QueryStage Stage { get; init; }

    /// <summary>The workstation's plain-language sentence, shown verbatim.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>True only for the newest row, which shows the activity indicator.</summary>
    [ObservableProperty]
    private bool _isCurrent;
}
